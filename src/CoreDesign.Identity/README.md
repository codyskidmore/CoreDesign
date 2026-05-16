# CoreDesign.Identity

CoreDesign.Identity is a pair of NuGet packages that let development teams drop an OIDC-compatible identity gateway directly into a solution. Teams can authenticate and authorize requests from day one without standing up Keycloak, Okta, Azure AD B2C, or any other external provider. When the project is ready for a real gateway, the client package connects to it through standard OIDC discovery, so nothing in the application code changes.

## Packages

| Package | Purpose |
|---|---|
| `CoreDesign.Identity.Server` | A minimal, self-contained OIDC server that runs inside your solution. Intended for development and testing only. |
| `CoreDesign.Identity.Client` | ASP.NET Core middleware and helpers that configure JWT Bearer authentication against any OIDC provider, with automatic token injection for local development. |

## Quick Start

### 1. Add the Server

Create a host project (a minimal ASP.NET Core app or .NET Aspire resource) and install the server package:

```
dotnet add package CoreDesign.Identity.Server
```

Wire up the services and endpoints in `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddIdentityServer(builder.Configuration);
builder.Services.AddJsonFileIdentityStore("identities.json");
builder.Services.AddJsonFileClientStore("clients.json");

var app = builder.Build();
app.UseIdentityServerCors(); // required for browser-based frontends calling /auth/login
app.MapIdentityEndpoints();
app.Run();
```

Add a `clients.json` file to define the registered Relying Parties (client applications):

```json
[
  {
    "clientId": "myapp-api-dev",
    "tokenEndpointAuthMethod": "none",
    "allowedGrantTypes": [ "password" ],
    "allowedRedirectUris": [],
    "allowedPostLogoutRedirectUris": [],
    "allowedScopes": [ "openid", "profile", "email" ],
    "requirePkce": false
  }
]
```

Add an `identities.json` file to the project, set it to copy to the output directory, and define at least one user:

```json
[
  {
    "userId": "d4e5f6a7-b8c9-0123-def4-567890abcdef",
    "username": "admin@example.local",
    "password": "Password1!",
    "email": "admin@example.local",
    "name": "Admin User",
    "givenName": "Admin",
    "familyName": "User",
    "permissions": ["items:read", "items:write"],
    "customClaims": {}
  }
]
```

### 2. Add the Client

In each API or host project that needs to authenticate requests, install the client package:

```
dotnet add package CoreDesign.Identity.Client
```

Register authentication and map the login endpoint in `Program.cs`:

```csharp
if (builder.Environment.IsDevelopment())
    builder.Services.AddIdentityClient(builder.Configuration);
else
    builder.Services.AddProductionAuthentication(...); // wire your real provider here

// ...

app.UseCors();
app.UseLocalBearerTokenInjection(); // injects a token automatically on localhost in Development
app.UseAuthentication();
app.UseAuthorization();
```

## Configuration

Both packages share a common `CoreDesign:Identity` section. When multiple projects in the solution need the same issuer, audience, or other values, maintain them in one place by following the guidance in [Sharing appsettings Across Projects](SharedAppsettings.md). That document describes how to create a `shared/` folder at the solution root, place the appsettings files there, and link them into each server and client project via the `.csproj` file. Any change to the shared file propagates to every linked project on the next build, eliminating configuration drift across the solution.

### Server Configuration

```json
{
  "CoreDesign": {
    "Identity": {
      "Issuer": "https://localhost:5003",
      "Audience": "https://api.example.local",
      "TokenLifetimeHours": 8,
      "KeyId": "coredesign-dev-signing-key"
    }
  }
}
```

| Key | Default | Description |
|---|---|---|
| `Issuer` | required | Value placed in the `iss` claim of every token |
| `Audience` | required | Value placed in the `aud` claim of every token |
| `TokenLifetimeHours` | `8` | How long a token remains valid |
| `KeyId` | `coredesign-dev-signing-key` | The `kid` header on the signed JWT |

### Client Configuration

```json
{
  "IdentityApi": {
    "BaseUrl": "https://localhost:5003",
    "ClientId": "myapp-api-dev",
    "Username": "admin@example.local",
    "Password": "Password1!"
  },
  "CoreDesign": {
    "Identity": {
      "Issuer": "https://localhost:5003",
      "Audience": "https://api.example.local"
    }
  }
}
```

The `IdentityApi` section is used only in Development by `UseLocalBearerTokenInjection`. The `CoreDesign:Identity` section is required in all environments to validate incoming tokens, making it the natural candidate for the shared appsettings file described in [SharedAppsettings.md](SharedAppsettings.md).

## Endpoints

The server package registers five endpoints:

| Endpoint | Method | Description |
|---|---|---|
| `/.well-known/openid-configuration` | GET | OIDC discovery document |
| `/.well-known/jwks.json` | GET | RSA public signing key in JWKS format |
| `/connect/token` | POST | Issues tokens via the OAuth 2.0 password grant (form-encoded). Requires a registered `client_id`. |
| `/connect/userinfo` | GET | Returns claims for a valid Bearer token |
| `/get-token` | POST | Convenience JSON endpoint for tooling (Postman, Scalar, curl). No `client_id` required. |
| `/auth/login` | POST | Frontend login endpoint — accepts JSON credentials, returns a token. No `client_id` required. |

## Frontend Login Flow

In development the frontend authenticates directly against `CoreDesign.Identity.Server`. In production it authenticates against Azure Entra (or any other OIDC provider). The API code is identical in both cases: every request carries `Authorization: Bearer <token>` and the API validates it using standard JWT Bearer middleware.

### Development: direct credential exchange

```
Frontend                 Identity Server              API
   |                           |                        |
   |-- POST /auth/login ------>|                        |
   |   { username, password }  |                        |
   |                           |-- validates            |
   |                           |-- builds RS256 JWT     |
   |<-- 200 { access_token } --|                        |
   |                           |                        |
   |-- GET /api/orders -------------------------------->|
   |   Authorization: Bearer <token>                    |
   |                           |    validates JWT       |
   |                           |    (JWKS cached)       |
   |<-- 200 [ ... ] ------------------------------------'
```

### Production: Azure Entra (MSAL)

```
Frontend                 Azure Entra                  API
   |                           |                        |
   |-- loginPopup() ---------->|                        |
   |<-- authorization code ----|                        |
   |-- acquireTokenSilent() -->|                        |
   |<-- access_token ----------|                        |
   |                           |                        |
   |-- GET /api/orders -------------------------------->|
   |   Authorization: Bearer <token>                    |
   |                           |    validates JWT       |
   |<-- 200 [ ... ] ------------------------------------'
```

The API receives a Bearer token in both flows. Its validation logic, authorization policies, and permission checks do not change.

### Step 1: obtain a token

**Development** — POST credentials as JSON to `POST /auth/login` on the identity server:

```js
const response = await fetch("https://localhost:5003/auth/login", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ username, password })
});

if (!response.ok) { /* show login error */ }
const { access_token, expires_in } = await response.json();
```

**Production** — use `@azure/msal-browser` to acquire a token via the authorization code flow with PKCE. Refer to the [Azure Entra guide](README.AzureEntra.md) for app registration and scope configuration.

### Step 2: store the token

Store `access_token` in memory or `sessionStorage`. Avoid `localStorage` for tokens that grant API access.

```js
sessionStorage.setItem("access_token", access_token);
```

### Step 3: attach the token to API requests

Include the token in the `Authorization` header on every protected request:

```js
const token = sessionStorage.getItem("access_token");

const response = await fetch("/api/orders", {
  headers: { Authorization: `Bearer ${token}` }
});
```

This code is identical regardless of whether the token came from the dev identity server or Azure Entra.

### Step 4: handle expiry

The token lifetime is controlled by `TokenLifetimeHours` in the server configuration (default: 8 hours). When the API returns `401 Unauthorized`, redirect the user to login and clear the stored token.

```js
if (response.status === 401) {
  sessionStorage.removeItem("access_token");
  redirectToLogin();
}
```

### What changes when switching to Entra

| Concern | Development | Production (Entra) |
|---|---|---|
| Token acquisition | `POST /auth/login` on the identity server | MSAL `loginPopup` or `loginRedirect` |
| API calls | `Authorization: Bearer <token>` | `Authorization: Bearer <token>` |
| API validation config | `CoreDesign:Identity:Issuer` and `Audience` | `AzureAd:TenantId` and `Audience` |
| API code and policies | unchanged | unchanged |
| Token claims (`permissions`, `email`, `oid`) | set in `identities.json` | Entra App Roles mapped to `permissions` claims via claims transformation |

## Token Claims

Every issued token includes the following claims:

| Claim | Source |
|---|---|
| `sub` | `userId` from the identity record |
| `email` | `email` from the identity record |
| `name` | `name` from the identity record |
| `given_name` | `givenName` from the identity record |
| `family_name` | `familyName` from the identity record |
| `oid` | `userId` from the identity record |
| `permissions` | Each entry in the `permissions` array becomes its own claim |
| Custom | Each key in `customClaims` becomes its own claim |

## Development Behavior

`UseLocalBearerTokenInjection` automatically attaches a Bearer token to incoming requests during local development. It activates only in the Development environment and only when all three conditions are met:

- The request has no existing `Authorization` header
- The request originates from localhost
- The request path is not a public endpoint (`/openapi`, `/swagger`, `/scalar`, `/health`, `/`)

Swagger UI, Scalar, and health check endpoints continue to work without authentication, while protected API routes receive a valid token automatically. Token fetches are cached and refreshed 60 seconds before expiry.

## Switching to a Real Provider

When the project is ready to connect to a production identity provider, replace `AddIdentityClient` with your provider's configuration. The client package validates tokens using standard OIDC metadata discovery, so any provider that publishes a `/.well-known/openid-configuration` document is compatible. The `Issuer` and `Audience` values in configuration are the only values that need to change.

## Important Notes

The server package is intended for development and integration testing only. It stores passwords in plaintext, generates an ephemeral RSA signing key on every startup (tokens issued before a restart become invalid), and opens CORS to all origins. Do not deploy it to any environment accessible outside a development machine.

## Feedback

Feedback on these packages is welcome. If you run into a missing feature, an unexpected behavior, or something that required more effort than it should have, open an issue at [github.com/codyskidmore/CoreDesign/issues](https://github.com/codyskidmore/CoreDesign/issues) or tag [@codyskidmore](https://github.com/codyskidmore). Suggestions about missing features and priority input are especially appreciated.
