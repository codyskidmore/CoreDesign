# CoreDesign.Identity.Server

A lightweight, self-contained OIDC identity server library for ASP.NET Core. It provides a complete set of OIDC-compatible endpoints (discovery, JWKS, authorization, token issuance, and userinfo) as minimal API routes that can be mounted on any ASP.NET Core application in a few lines of code.

Intended for **development and testing only**. Passwords are stored in plaintext, the RSA signing key is persisted to `%APPDATA%\coredesign-identity\` across restarts, and CORS is left open. Do not use in production.

## What it provides

| Endpoint | Purpose |
| --- | --- |
| `GET /.well-known/openid-configuration` | OIDC discovery document |
| `GET /.well-known/jwks.json` | Public signing key (JWKS) |
| `GET /connect/authorize` | Renders the login form for browser-based OIDC flows |
| `POST /connect/authorize` | Processes login form submission, issues authorization code |
| `POST /connect/token` | Token issuance via password grant or authorization code grant (`application/x-www-form-urlencoded`) |
| `GET /connect/userinfo` | Returns claims for a valid bearer token |
| `POST /get-token` | Convenience JSON token endpoint for tooling (non-standard, no `client_id` required) |
| `POST /auth/login` | Direct JSON login endpoint for non-OIDC frontends (no `client_id` required) |

Tokens are RS256-signed JWTs containing `sub`, `email`, `preferred_username`, `name`, `given_name`, `family_name`, `oid`, `roles`, and any custom claims defined on the identity record.

## Login flows

### Browser login: Authorization Code with PKCE

This is the standard flow for Blazor and other browser-based apps. The browser redirects to `/connect/authorize`, the user enters credentials in the hosted login form, and the server redirects back with a short-lived authorization code. The client then exchanges the code for tokens at `/connect/token`.

**Step 1 — browser redirects to the authorization endpoint**

```
GET /connect/authorize
  ?response_type=code
  &client_id=my-blazor-app
  &redirect_uri=https%3A%2F%2Flocalhost%3A7070%2Fsignin-oidc
  &scope=openid%20profile%20email
  &state={opaque-value}
  &code_challenge={S256-hash-of-verifier}
  &code_challenge_method=S256
```

The server returns an HTML login form. When the user submits it, the server validates the credentials and redirects back:

```
302 https://localhost:7070/signin-oidc?code={code}&state={state}
```

If credentials are invalid the form is re-rendered with a `401` status and an error message.

**Step 2 — client exchanges the code for tokens**

```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code
&client_id=my-blazor-app
&code={code}
&redirect_uri=https%3A%2F%2Flocalhost%3A7070%2Fsignin-oidc
&code_verifier={original-verifier}
```

**Response (200)**

```json
{
  "access_token": "<jwt>",
  "id_token": "<jwt>",
  "token_type": "Bearer",
  "expires_in": 28800,
  "scope": "openid profile email"
}
```

The `access_token` audience is the API resource (`CoreDesign:Identity:Audience`). The `id_token` audience is the `client_id`. These are distinct JWTs so that an API can validate the access token without knowing any client IDs.

### Direct JSON login

`POST /auth/login` is a non-OIDC shortcut for frontends that manage their own token storage rather than going through an authorization server redirect. It accepts JSON credentials and returns a signed JWT directly.

**Request**

```http
POST /auth/login
Content-Type: application/json

{
  "username": "alice@example.local",
  "password": "Password1!"
}
```

**Response (200)**

```json
{
  "access_token": "<jwt>",
  "id_token": "<jwt>",
  "token_type": "Bearer",
  "expires_in": 28800,
  "scope": "openid profile email"
}
```

**Response (401)** on invalid credentials.

The identity server must have CORS enabled for browser clients to reach this endpoint. Call `UseIdentityServerCors()` before `MapIdentityEndpoints()` (see [Setup](#setup)).

### Tooling token generation

`POST /get-token` accepts the same JSON credentials as `/auth/login` and returns the same response. Use it for tooling such as Scalar, Postman, or curl when you need a quick token without going through the browser flow.

If the credentials are invalid the endpoint returns `400 Bad Request`:

```json
{
  "error": "invalid_grant",
  "error_description": "Invalid username or password"
}
```

## Setup

There are two hosting patterns depending on whether you want a fully self-contained web host or need to embed the identity endpoints in a larger application.

### Option A: Standalone web host (recommended)

Use `AddIdentityServerWebHost` and `MapIdentityServerWebHost`. This reads all configuration from a single `CoreDesign:IdentityWebHost` section, registers the JSON file stores, enables CORS, and adds a simple landing page at `/`.

```csharp
using CoreDesign.Identity.Server;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddIdentityServerWebHost(builder.Configuration);

var app = builder.Build();

app.MapIdentityServerWebHost();

app.Run();
```

Add a `CoreDesign:IdentityWebHost` section to `appsettings.json`:

```json
{
  "CoreDesign": {
    "IdentityWebHost": {
      "Issuer": "https://localhost:5003",
      "Audience": "https://api.example.local",
      "TokenLifetimeHours": 8,
      "IdentitiesFilePath": "identities.json",
      "ClientsFilePath": "clients.json"
    }
  }
}
```

| Key | Default | Description |
| --- | --- | --- |
| `Issuer` | _(required)_ | Value placed in the `iss` claim and returned by the discovery endpoint. Must match the URL at which the identity server is reachable. |
| `Audience` | _(required)_ | Value placed in the `aud` claim of access tokens. Typically your API's base URL. |
| `KeyId` | `coredesign-dev-signing-key` | `kid` header on the JWT and JWKS entry. Change this if you need multiple signing keys. |
| `TokenLifetimeHours` | `8` | Token validity window in hours. |
| `IdentitiesFilePath` | `identities.json` | Path to the identities file, relative to the output directory. |
| `ClientsFilePath` | `clients.json` | Path to the clients file, relative to the output directory. |

### Option B: Custom host

Use `AddIdentityServer` and `MapIdentityEndpoints` when you need to embed the identity endpoints in an existing application or want finer control over registration. In this case you register stores separately.

```csharp
using CoreDesign.Identity.Server;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddIdentityServer(builder.Configuration, sectionName: "CoreDesign:Identity");
builder.Services.AddJsonFileIdentityStore("identities.json");
builder.Services.AddJsonFileClientStore("clients.json");

var app = builder.Build();

app.UseIdentityServerCors(); // required for browser clients
app.MapIdentityEndpoints();

app.Run();
```

The `CoreDesign:Identity` section uses the same keys as `CoreDesign:IdentityWebHost` except `IdentitiesFilePath` and `ClientsFilePath`, which are passed directly to `AddJsonFileIdentityStore` and `AddJsonFileClientStore`.

## Clients file

Add a `clients.json` file and set it to copy to the output directory:

```xml
<None Update="clients.json">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

The file is a JSON array of registered client records. A typical setup includes one client for browser-based login (authorization code with PKCE) and one for service-to-service calls (password grant):

```json
[
  {
    "clientId": "my-blazor-app",
    "tokenEndpointAuthMethod": "none",
    "allowedGrantTypes": [ "authorization_code" ],
    "allowedRedirectUris": [ "https://localhost:7070/signin-oidc" ],
    "allowedPostLogoutRedirectUris": [
      "https://localhost:7070/signout-callback-oidc",
      "https://localhost:7070/"
    ],
    "allowedScopes": [ "openid", "profile", "email" ],
    "requirePkce": true
  },
  {
    "clientId": "my-api-dev",
    "tokenEndpointAuthMethod": "none",
    "allowedGrantTypes": [ "password" ],
    "allowedScopes": [ "openid", "profile", "email" ],
    "requirePkce": false
  }
]
```

| Field | Type | Description |
| --- | --- | --- |
| `clientId` | string | Unique identifier for the client. Case-sensitive. Must be sent as `client_id` in every `/connect/token` request. |
| `clientSecret` | string or null | Optional shared secret. Null for public clients (SPAs, CLI tools, PKCE flows). |
| `tokenEndpointAuthMethod` | string | `"none"` for public clients, `"client_secret_post"` for confidential clients. |
| `allowedGrantTypes` | string[] | Grant types this client may use: `"authorization_code"` for browser flows, `"password"` for service-to-service. |
| `allowedRedirectUris` | string[] | Pre-registered redirect URIs. Required for authorization code clients. Exact string match. |
| `allowedPostLogoutRedirectUris` | string[] | Pre-registered post-logout redirect URIs. |
| `allowedScopes` | string[] | Scopes this client is permitted to request. |
| `requirePkce` | bool | When true, `/connect/authorize` rejects requests without a valid `code_challenge`. Always set to true for browser-based clients. |

## Identities file

Add an `identities.json` file and set it to copy to the output directory:

```xml
<None Update="identities.json">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

The file is a JSON array of identity records:

```json
[
  {
    "userId": "11111111-1111-1111-1111-111111111111",
    "username": "admin@example.local",
    "password": "Password1!",
    "email": "admin@example.local",
    "name": "Admin User",
    "givenName": "Admin",
    "familyName": "User",
    "roles": [ "Admin", "AppUser" ],
    "customClaims": {}
  }
]
```

| Field | Type | Description |
| --- | --- | --- |
| `userId` | string (GUID) | Value used for the `sub` and `oid` claims |
| `username` | string | Login username. Comparison is case-insensitive. |
| `password` | string | Plaintext password. Comparison is case-sensitive. |
| `email` | string | `email` claim |
| `name` | string | `name` claim |
| `givenName` | string | `given_name` claim |
| `familyName` | string | `family_name` claim |
| `roles` | string[] | Each value emitted as a separate `roles` claim |
| `customClaims` | object | Arbitrary key-value pairs added as additional claims |

## Blazor app integration

A Blazor Server app authenticates against this identity server using ASP.NET Core's built-in OIDC middleware. The app gets an auth cookie containing the access token, which it then attaches to outbound API requests.

### 1. Register authentication

```csharp
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = "https://localhost:5003"; // identity server URL
        options.ClientId = "my-blazor-app";
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.RequireHttpsMetadata = false; // dev only
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
    });
```

### 2. Map login and logout endpoints

```csharp
app.MapGet("/account/login", (string? returnUrl, HttpContext ctx) =>
    ctx.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme,
        new AuthenticationProperties { RedirectUri = returnUrl ?? "/" }))
    .AllowAnonymous();

app.MapGet("/account/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
});
```

### 3. Forward the access token to downstream APIs

```csharp
public class BearerTokenHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await accessor.HttpContext!
            .GetTokenAsync("access_token");
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}

// Registration:
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient<MyApiClient>(c => c.BaseAddress = new Uri("https://my-api"))
    .AddHttpMessageHandler<BearerTokenHandler>();
```

### 4. Register the Blazor client in clients.json

The `redirect_uri` must exactly match what the OIDC middleware sends. For a local Blazor app on port 7070:

```json
{
  "clientId": "my-blazor-app",
  "tokenEndpointAuthMethod": "none",
  "allowedGrantTypes": [ "authorization_code" ],
  "allowedRedirectUris": [ "https://localhost:7070/signin-oidc" ],
  "allowedPostLogoutRedirectUris": [
    "https://localhost:7070/signout-callback-oidc",
    "https://localhost:7070/"
  ],
  "allowedScopes": [ "openid", "profile", "email" ],
  "requirePkce": true
}
```

### Authority and port stability

The `Issuer` in `appsettings.json` must match the URL at which the identity server is actually reachable, including port. If you run under .NET Aspire, pin the identity server to a fixed port so the Blazor app always finds it at the same address:

```csharp
// In AppHost
builder.AddProject<SampleApi_Identity_Web>("SampleApiIdentityApi")
    .WithHttpsEndpoint(port: 5003, name: "https", isProxied: false);

// In Blazor app setup, read the authority from Aspire service discovery:
var authority =
    configuration["services:SampleApiIdentityApi:https:0"]  // Aspire-injected
    ?? configuration["IdentityApi:BaseUrl"]                 // appsettings fallback
    ?? throw new InvalidOperationException("OIDC authority not configured");
```

## Custom stores

### Custom identity store

`AddJsonFileIdentityStore` is a convenience wrapper around `IIdentityStore`. To use a different backing source (database, in-memory list, etc.) implement the interface and register it directly:

```csharp
public class MyIdentityStore : IIdentityStore
{
    public Task<IdentityRecord?> FindByCredentialsAsync(string username, string password) { ... }
    public Task<IdentityRecord?> FindByIdAsync(string userId) { ... }
}

builder.Services.AddSingleton<IIdentityStore, MyIdentityStore>();
```

### Custom client store

`AddJsonFileClientStore` is a convenience wrapper around `IClientStore`. For a different backing source implement the interface and register it directly:

```csharp
public class MyClientStore : IClientStore
{
    public Task<ClientRecord?> FindByClientIdAsync(string clientId) { ... }
}

builder.Services.AddSingleton<IClientStore, MyClientStore>();
```

## Advanced registration

`AddIdentityServer` accepts an optional `Action<IdentityOptions>` to override individual values after configuration binding:

```csharp
builder.Services.AddIdentityServer(builder.Configuration, configure: opts =>
{
    opts.TokenLifetimeHours = 1;
});
```

The `sectionName` parameter controls which configuration section is bound:

```csharp
builder.Services.AddIdentityServer(builder.Configuration, sectionName: "MyApp:Auth");
```
