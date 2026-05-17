# Sample.Blazor

An ASP.NET Core Blazor Server application that demonstrates browser-based OIDC authentication against a development identity server and forwards the resulting access token to a downstream REST API.

## How it works

The application authenticates users via OpenID Connect Authorization Code flow with PKCE. On the first visit to any protected page, the `RedirectToLogin` component redirects the browser to the identity server's login form. After the user authenticates, the identity server redirects back to `/signin-oidc` with an authorization code. ASP.NET Core's OIDC middleware exchanges the code for tokens, stores them in an encrypted auth cookie (`SaveTokens = true`), and redirects the user to the original page. Subsequent API calls attach the access token from the cookie as a `Bearer` header via `BearerTokenHandler`.

## Project Structure

```
Sample.Blazor/
├── Components/
│   ├── Pages/
│   │   ├── Home.razor          -- Claims display, shows active auth provider name
│   │   └── WeatherForecasts.razor -- Fetches data from Sample.Api
│   ├── Layout/
│   │   ├── MainLayout.razor    -- Top bar with Sign in / Sign out links
│   │   └── NavMenu.razor
│   ├── App.razor
│   ├── RedirectToLogin.razor   -- Redirects unauthenticated users to /account/login
│   └── Routes.razor            -- Wires RedirectToLogin as NotAuthorized fallback
├── Infrastructure/
│   ├── Auth/
│   │   ├── IAuthProviderConfigurator.cs
│   │   ├── LocalOidcAuthConfigurator.cs
│   │   └── AzureEntraAuthConfigurator.cs
│   ├── App.cs                  -- Middleware pipeline and login/logout endpoints
│   └── Configuration.cs        -- DI registration and auth provider selection
└── Services/
    ├── BearerTokenHandler.cs   -- Attaches access token to outbound HTTP requests
    ├── SampleClient.cs         -- Typed HTTP client for Sample.Api
    └── WeatherForecastResponse.cs
```

## Auth Provider Selection

`IAuthProviderConfigurator` is a small interface that abstracts over the authentication provider. Two implementations ship with the project:

| Implementation | Config value | Provider |
| --- | --- | --- |
| `LocalOidcAuthConfigurator` | `"Local"` (default) | Sample.Identity.Web via OIDC |
| `AzureEntraAuthConfigurator` | `"AzureEntra"` | Azure Entra ID via Microsoft.Identity.Web |

The active provider is chosen at startup in `Configuration.cs` based on the `Blazor:AuthProvider` key. The selected configurator is registered as a singleton in DI so Razor components can inject `IAuthProviderConfigurator` to read `ProviderName` for display (see `Home.razor`).

Both implementations configure the same cookie paths (`/account/login`, `/account/logout`, `/account/access-denied`) so all components share one login and logout route regardless of which provider is active. `SupportsFederatedLogout` controls whether sign-out also calls the OIDC end-session endpoint (true for Azure Entra, false for the local server).

## LocalOidcAuthConfigurator

Configures cookie plus OpenID Connect authentication against Sample.Identity.Web. It resolves the identity server URL from several sources in order of preference:

1. `Blazor:Oidc:Authority` (explicit override in appsettings)
2. `services:SampleIdentityWeb:https:0` (Aspire service discovery, IConfiguration normalized form)
3. `services__SampleIdentityWeb__https__0` (Aspire service discovery, raw environment variable form)
4. Connection string `"SampleIdentityWeb"`
5. `IdentityApi:BaseUrl` (developer fallback for running outside Aspire)

When running under Aspire the AppHost pins Sample.Identity.Web to port 5003 (`WithHttpsEndpoint(port: 5003, isProxied: false)`) so the injected URL always matches the `Issuer` in appsettings.

The OIDC client ID and requested scopes are read from configuration:

```json
"Blazor": {
  "AuthProvider": "Local",
  "Oidc": {
    "ClientId": "sample-blazor",
    "Scopes": [ "openid", "profile", "email", "https://api.sampleapi.local" ]
  }
}
```

The `sample-blazor` client must be registered in `clients.json` with `allowedRedirectUris` pointing to `https://localhost:7070/signin-oidc`. The AppHost pins the Blazor app to port 7070 so this URI stays stable across restarts.

## AzureEntraAuthConfigurator

Delegates to `AddMicrosoftIdentityWebAppAuthentication` from `Microsoft.Identity.Web`. Required keys in appsettings (supply secrets via user secrets or a key vault):

```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "TenantId": "<your-tenant-id>",
  "ClientId": "<your-client-id>",
  "CallbackPath": "/signin-oidc"
}
```

`AzureAd:ClientSecret` must be stored in user secrets, never in appsettings files.

## RedirectToLogin

`RedirectToLogin.razor` is a component that redirects unauthenticated users to `/account/login?returnUrl=<encoded-current-url>` using `NavigationManager`. It is set as the `NotAuthorized` content of `AuthorizeRouteView` in `Routes.razor`. This means any page decorated with `@attribute [Authorize]` will automatically redirect instead of showing a generic "not authorized" message.

## Login and Logout Endpoints

`App.cs` registers two minimal API routes:

| Route | Behavior |
| --- | --- |
| `GET /account/login` | Issues an OIDC challenge that redirects the browser to the identity server login form. Accepts `returnUrl` as a query parameter. |
| `GET /account/logout` | Signs out of the local cookie. If `SupportsFederatedLogout` is true, also signs out of the OIDC session at the identity server. |

## BearerTokenHandler

`BearerTokenHandler` is a `DelegatingHandler` that reads the `access_token` stored in the auth cookie (by `SaveTokens = true` in the OIDC options) and attaches it as `Authorization: Bearer <token>` on every outbound request. It is registered as a message handler on the `SampleClient` typed HTTP client:

```csharp
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient<SampleClient>(client =>
        client.BaseAddress = new Uri("https://SampleApi"))
    .AddHttpMessageHandler<BearerTokenHandler>();
```

The base address `https://SampleApi` uses Aspire service discovery so the Blazor app finds Sample.Api automatically without hard-coding a port.

## Home Page and Claims Display

`Home.razor` reads the authenticated user's identity from the cascading `Task<AuthenticationState>` parameter and renders a table of all JWT claims. `AddCascadingAuthenticationState()` is registered in `Configuration.cs`, which makes the auth state available to every component in the tree without each component having to inject `IHttpContextAccessor` individually.

The page also displays the active auth provider name (injected via `IAuthProviderConfigurator`) so it is immediately obvious whether the app is running against the local identity server or Azure Entra.

## Interactive Rendering and Authentication State

`WeatherForecasts.razor` uses `new InteractiveServerRenderMode(prerender: false)` rather than plain `InteractiveServer`. Prerendering runs on the server before the SignalR circuit is established, at which point the authenticated user's identity is not yet available to the component. Disabling prerender ensures the component executes only during the interactive phase, when the full auth context is present and API calls can carry the correct bearer token.

## Configuration reference

`appsettings.json` (shared from `src/Shared/`):

| Key | Purpose |
| --- | --- |
| `Blazor:AuthProvider` | `"Local"` or `"AzureEntra"` |
| `Blazor:Oidc:Authority` | Override the OIDC authority URL (optional when running under Aspire) |
| `Blazor:Oidc:ClientId` | OIDC client ID (defaults to `"sample-blazor"`) |
| `Blazor:Oidc:Scopes` | Requested scopes array |
| `AzureAd:TenantId` | Azure Entra tenant ID (required when `AuthProvider` is `"AzureEntra"`) |
| `AzureAd:ClientId` | Azure Entra app registration client ID |
| `AzureAd:ClientSecret` | Azure Entra client secret (supply via user secrets) |

## Full documentation

See `src/CoreDesign.Identity/CoreDesign.Identity.Server/README.md` for complete library documentation including all OIDC endpoints, the clients.json and identities.json schemas, and template customization details.
