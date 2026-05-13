# Release Notes

## CoreDesign.Identity.Server 1.0.7

### Authorization Code with PKCE

The identity server now implements the full Authorization Code with PKCE flow, making it compatible with Blazor Server and other browser-based applications that use ASP.NET Core's built-in OIDC middleware.

Two new endpoints handle the browser login flow:

| Endpoint | Purpose |
| --- | --- |
| `GET /connect/authorize` | Renders the hosted login form for a given OIDC request |
| `POST /connect/authorize` | Processes form submission, validates credentials, issues an authorization code, and redirects back to the client |

The `POST /connect/token` endpoint now also handles the `authorization_code` grant type in addition to the existing `password` grant. Code exchange validates the `code_verifier` against the stored PKCE challenge and consumes the code so it cannot be reused.

New internal types supporting the flow:

- `AuthorizeRequest` — Parses OIDC request parameters from query string or form.
- `AuthorizationCodeTicket` — Stores the full authorization code state including the PKCE challenge and a five-minute expiration.
- `AuthorizationCodeStore` — In-memory store with `Issue()` and `TryConsume()` methods. `TryConsume()` validates the PKCE verifier before returning the ticket.
- `AuthorizeEndpoint` — The full endpoint handler. Renders the login form on GET, validates credentials and issues a code on POST, and redirects to the registered redirect URI on success or re-renders the form with an error on failure.

### Client Store

Browser-based login requires registered clients. A new `IClientStore` interface and a built-in JSON file implementation allow the server to enforce per-client rules at both the authorization and token endpoints.

Register the built-in store:

```csharp
builder.Services.AddJsonFileClientStore("clients.json");
```

Each `ClientRecord` in `clients.json` controls:

| Field | Description |
| --- | --- |
| `clientId` | Unique identifier. Case-sensitive. |
| `clientSecret` | Optional shared secret for confidential clients. |
| `tokenEndpointAuthMethod` | `"none"` for public clients, `"client_secret_post"` for confidential clients. |
| `allowedGrantTypes` | `"authorization_code"` for browser flows, `"password"` for service-to-service. |
| `allowedRedirectUris` | Pre-registered redirect URIs. Exact string match. Required for authorization code clients. |
| `allowedPostLogoutRedirectUris` | Pre-registered post-logout redirect URIs. |
| `allowedScopes` | Scopes this client may request. |
| `requirePkce` | When `true`, `/connect/authorize` rejects requests without a valid `code_challenge`. Always set to `true` for browser-based clients. |

To use a custom backing store (database, in-memory list, etc.) implement `IClientStore` and register it directly:

```csharp
builder.Services.AddSingleton<IClientStore, MyClientStore>();
```

### Standalone Web Host Pattern

A new registration path, `AddIdentityServerWebHost` and `MapIdentityServerWebHost`, sets up the full identity server stack from a single configuration section. It registers both JSON file stores, enables CORS, serves a landing page at `/`, and mounts all OIDC endpoints.

```csharp
builder.Services.AddIdentityServerWebHost(builder.Configuration);
var app = builder.Build();
app.MapIdentityServerWebHost();
```

Configuration lives under `CoreDesign:IdentityWebHost`:

| Key | Default | Description |
| --- | --- | --- |
| `Issuer` | _(required)_ | Placed in `iss` claim and returned by the discovery endpoint. Must match the server's reachable URL. |
| `Audience` | _(required)_ | Placed in `aud` claim of access tokens. Typically the API's base URL. |
| `KeyId` | `coredesign-dev-signing-key` | `kid` header on the JWT and JWKS entry. |
| `TokenLifetimeHours` | `8` | Token validity window in hours. |
| `IdentitiesFilePath` | `identities.json` | Path to the identities file, relative to output directory. |
| `ClientsFilePath` | `clients.json` | Path to the clients file, relative to output directory. |

The existing `AddIdentityServer` and `MapIdentityEndpoints` path remains available for embedding the endpoints in a larger application or for finer-grained control.

### Template Customization

The login form, error banner, and landing page are now fully customizable without modifying the library. A new `TemplateLoader` checks `{ContentRoot}/identity-templates/{filename}` before falling back to the embedded default. No configuration is required.

To override a template, place the file in an `identity-templates` folder and mark it to copy to output:

```xml
<None Update="identity-templates\login.html">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

**`login.html`** supports these placeholders (HTML-encoded at render time):

| Placeholder | Value |
| --- | --- |
| `{{response_type}}` | OIDC `response_type` parameter |
| `{{client_id}}` | OIDC `client_id` parameter |
| `{{redirect_uri}}` | OIDC `redirect_uri` parameter |
| `{{scope}}` | OIDC `scope` parameter |
| `{{state}}` | OIDC `state` parameter |
| `{{nonce}}` | OIDC `nonce` parameter |
| `{{code_challenge}}` | PKCE `code_challenge` |
| `{{code_challenge_method}}` | PKCE method, e.g. `S256` |
| `{{error_alert}}` | Fully rendered error banner HTML; empty string when there is no error |

**`login-error.html`** is a separate template for the error banner so it can be restyled independently of the form. It supports one placeholder:

| Placeholder | Value |
| --- | --- |
| `{{error_message}}` | HTML-encoded error text, e.g. `Invalid username or password` |

**`landing.html`** is the page served at `/` by the standalone web host. It has no dynamic placeholders.

All three default templates define colors as CSS custom properties (`--id-*` variables). Light restyling can be done by injecting a `<style>` block that overrides those variables rather than replacing the entire template.

### Separate Access Tokens and ID Tokens

The token endpoint now issues two distinct JWTs per successful authentication:

- **Access token** — audience is the API resource (`CoreDesign:Identity:Audience`). Presented to the API on every request.
- **ID token** — audience is the `client_id`. Contains the `nonce` from the authorization request. Consumed by the client application only.

APIs can validate access tokens without knowing any `client_id` values. The `nonce` claim appears only in the ID token, which is correct per the OIDC specification.

### Persistent RSA Signing Key

The RSA signing key is now persisted across restarts. On first startup the server generates an RSA key and writes it to `%APPDATA%\coredesign-identity\{keyId}.pem`. Subsequent startups load the same key from disk. This prevents token validation failures after a server restart in development scenarios.

Key generation and loading is race-condition safe. If two processes attempt to create the key simultaneously, the first writer wins and the second loads the existing file.

### OIDC Discovery Updates

The discovery document at `/.well-known/openid-configuration` has been updated:

- `response_types_supported` now returns `["code"]` only. The previous value included `"token"` and `"id_token"`, which are implicit flow response types not supported by this server.
- `grant_types_supported` now returns `["authorization_code", "password"]`.
- `code_challenge_methods_supported` is a new field returning `["S256"]`.

### UserInfo Logging

The `GET /connect/userinfo` endpoint now logs bearer token validation failures at Warning level, including the request IP address. This makes it easier to diagnose misconfigured clients during development.

### Dependency Update

`System.IdentityModel.Tokens.Jwt` upgraded from 8.8.0 to 8.18.0.

### Bug Fixes

- Fixed a 403 error that occurred during the browser-based authorization code exchange.
- Fixed the error alert box not rendering correctly after a failed login attempt in the hosted login form.

### Breaking Changes

- `response_types_supported` in the OIDC discovery document no longer includes `"token"` or `"id_token"`. Clients that rely on the discovery document to confirm implicit flow support will see a change.
- The authorization endpoint now validates clients against the client store. A `client_id` that is not registered in `clients.json` (or a custom `IClientStore`) is rejected with an error page.

---

## CoreDesign.Identity.Client 1.0.7

### Dual Configuration Section Support

The client now reads issuer and audience from both `CoreDesign:Identity` and `CoreDesign:IdentityWebHost` configuration sections. `CoreDesign:IdentityWebHost` takes precedence when both are present. This makes client configuration symmetric with the server's standalone web host pattern: a single `CoreDesign:IdentityWebHost` block configures both sides without duplication.

### Dependency Updates

- `Microsoft.AspNetCore.Authentication.JwtBearer` upgraded from 10.0.6 to 10.0.7.
- `Microsoft.Extensions.Http` upgraded from 10.0.0 to 10.0.7.

---

## CoreDesign.Logging 1.0.3

New package.

`CoreDesign.Logging` provides a `DispatchProxy`-based middleware that wraps any service interface and automatically produces structured log output for every method call, return value, and exception. Service implementation classes require no changes.

### Registration

Replace the standard `AddTransient` (or `AddScoped`) call with `AddWithLogging`:

```csharp
services.AddWithLogging<IWeatherForecastService, WeatherForecastService>();
```

The DI container resolves `IWeatherForecastService` as a proxy-wrapped instance. The concrete class is unaware of the proxy.

Pass a different lifetime when needed:

```csharp
services.AddWithLogging<IMyService, MyService>(ServiceLifetime.Scoped);
```

### Log Levels

| Situation | Level |
| --- | --- |
| Method called | Information (method name and serialized parameters) |
| Method returned successfully | Information (method name and serialized return value) |
| Method returned `NotFoundMessage` or `BadRequestMessage` | Warning |
| Method threw an exception | Error (exception and method name) |

Both synchronous and `Task`/`Task<T>` methods are fully supported.

### Sensitive Data Control

Two attributes control what is written to logs for methods that handle passwords, tokens, or other sensitive information.

`[Redact]` on a parameter replaces that argument with `"[REDACTED]"` in the log output. The real value is passed to the implementation unchanged:

```csharp
public interface IAuthService
{
    Task<LoginResult> LoginAsync(string username, [Redact] string password);
}
```

`[Suppress]` on a method skips all logging for that method. No invocation, result, or exception entries are written:

```csharp
public interface ITokenService
{
    [Suppress]
    Task<string> IssueTokenAsync(string userId);
}
```

Use `[Suppress]` when the method name or parameter shape itself would be too revealing, or when call volume is high enough that per-call logging creates more noise than value.

---

## CoreDesign.Data 1.0.1

No new features. Documentation updated with more complete setup guidance covering entity definition, entity configuration, `DbContext` registration, repository registration, and usage examples for both read and write operations.

---

## Sample Application

### Renamed from SampleApi to Sample

The sample solution and all project namespaces have been renamed from `SampleApi.*` to `Sample.*` to reflect the expanded scope of the reference project. The solution file is `Sample/Sample.slnx`.

### New: Sample.Identity.Web

A dedicated standalone identity server built on the new `AddIdentityServerWebHost` pattern. It hosts the OIDC login form for Blazor browser-based flows and reads users from a shared `identities.json` file. The server is pinned to HTTPS port 5003 for a stable issuer URL.

Two accounts are pre-configured:

| Email | Password | Roles |
| --- | --- | --- |
| admin@sampleapi.local | Password1! | DevAdmin, DevAppUsers |
| user@sampleapi.local | Password1! | DevAppUsers |

### New: Sample.Blazor

An ASP.NET Core Blazor Server application that authenticates via OpenID Connect (Authorization Code with PKCE) and calls Sample.Api to display weather forecast data. Key design points:

**Authentication provider selection.** The `Blazor:AuthProvider` configuration key selects between `"Local"` (Sample.Identity.Web) and `"AzureEntra"` (Azure Entra ID) at startup, using the `IAuthProviderConfigurator` abstraction. Both options implement the same interface so the rest of the application is unaware of which provider is active.

**Authority resolution for local development.** `LocalOidcAuthConfigurator` discovers the identity server URL through a priority chain:

1. `Blazor:Oidc:Authority` (explicit override)
2. `services:SampleIdentityWeb:https:0` (Aspire service discovery, normalized format)
3. `services__SampleIdentityWeb__https__0` (Aspire raw environment variable)
4. Connection string named `"SampleIdentityWeb"`
5. `IdentityApi:BaseUrl` (fallback for non-Aspire environments)

**Token forwarding.** `BearerTokenHandler` (a `DelegatingHandler`) reads the access token from the authenticated user's cookie and attaches it to every outbound request made by the `SampleClient` typed HTTP client. The API receives a standard Bearer token and does not need to know it originated from a Blazor cookie.

**Protected pages.** `RedirectToLogin.razor` replaces Razor's built-in `AuthorizeRouteView` behavior: unauthenticated users are redirected to `/account/login`, which triggers the OIDC challenge and redirects the browser to the identity server's login form.

**Pages:**
- `Home.razor` — Displays the authenticated user's claims and the active authentication provider name.
- `WeatherForecasts.razor` — Calls Sample.Api and displays the weather forecast data.

The Blazor app is pinned to HTTPS port 7070 so the redirect URI registered in `clients.json` remains stable across Aspire restarts.

### Updated: Sample.Api

- `Infrastructure/Configuration.cs` reorganized into focused setup methods (`AddIdentityAuthentication`, `AddAzureEntraAuthentication`, `AddDatabase`, `AddCache`, etc.).
- API is pinned to a fixed HTTPS port for consistent testing.

### Updated: Sample.Aspire.AppHost

`AppHostExtensions.cs` extracts the Aspire wiring into named helper methods:

- `AddSqlDatabase()` — SQL Server container with a persistent named volume.
- `AddIdentityWeb()` — Sample.Identity.Web at port 5003.
- `AddIdentityApi()` — Sample.Identity.Api at port 5001 with Scalar UI.
- `AddSampleApi()` — Main API with database and identity dependencies.
- `AddMigrationService()` — Migration service that runs before the API.
- `AddSampleBlazor()` — Blazor app at port 7070 with identity and API dependencies.

All services run on fixed HTTPS ports to ensure the OIDC issuer URL and redirect URIs remain stable.

### Shared Configuration Files

Client and identity store JSON files are now in a single `Sample/src/Shared/` folder, shared by all service projects. This ensures both identity server instances (Sample.Identity.Web and Sample.Identity.Api) read from the same user and client records.

`clients.json` pre-registers two clients:

| Client ID | Grant Type | PKCE | Purpose |
| --- | --- | --- | --- |
| `sample-api-dev` | `password` | No | Direct API testing via Scalar or Postman |
| `sample-blazor` | `authorization_code` | Yes (S256) | Blazor browser login |

### Bug Fixes

- Fixed a Blazor page layout issue where the navigation sidebar overlapped the main content area.
- Fixed all services to use HTTPS/encrypted ports consistently.

---

## Tests

### New Test Coverage for CoreDesign.Identity.Server

**`AuthorizeEndpointTests`** (new file)

- Login form renders on GET with no credentials.
- OIDC parameters (`client_id`, `redirect_uri`, `scope`, `state`, `nonce`, `code_challenge`, `code_challenge_method`) are embedded as hidden fields in the form.
- Valid credentials produce a redirect to the registered redirect URI with `code` and `state` query parameters.
- Invalid credentials return HTTP 401 and re-render the form with an error message.
- Missing or invalid `response_type` is rejected.
- Unregistered `client_id` is rejected.
- Unregistered `redirect_uri` is rejected.
- Missing `openid` scope is rejected.
- Missing `code_challenge` is rejected when the client has `requirePkce: true`.
- Non-S256 `code_challenge_method` is rejected.

**`TokenEndpointTests`** (expanded)

- Authorization code exchange with valid code and PKCE verifier returns tokens.
- Invalid PKCE verifier is rejected.
- Access token audience equals the API resource identifier from `IdentityOptions.Audience`.
- ID token audience equals the `client_id`.
- `nonce` from the authorization request appears in the ID token and not in the access token.
- Mismatched `redirect_uri` is rejected.
- A consumed authorization code cannot be reused.
- Missing `code` is rejected.
- Missing `code_verifier` when PKCE is required is rejected.

**`OidcDiscoveryEndpointTests`** (updated)

- `response_types_supported` contains `"code"` and does not contain `"token"`.
- `grant_types_supported` contains both `"authorization_code"` and `"password"`.
- `code_challenge_methods_supported` contains `"S256"`.

**`UserInfoEndpointTests`** (new file)

- Valid bearer token returns expected claims.
- Invalid or expired token returns HTTP 401.
- Claims mapping is validated for `sub`, `email`, `name`, and `roles`.
