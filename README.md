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

builder.Services.AddDcneIdentity(builder.Configuration);
builder.Services.AddJsonFileIdentityStore("identities.json");

var app = builder.Build();
app.MapDcneIdentityEndpoints();
app.Run();
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
    "roles": ["admin", "user"],
    "customClaims": {}
  }
]
```

### 2. Add the Client

In each API or host project that needs to authenticate requests, install the client package:

```
dotnet add package CoreDesign.Identity.Client
```

Register authentication in `Program.cs`:

```csharp
if (builder.Environment.IsDevelopment())
    builder.Services.AddLocalIdentityAuthentication(builder.Configuration);
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
| `/connect/token` | POST | Issues tokens via the OAuth 2.0 password grant (form-encoded) |
| `/connect/userinfo` | GET | Returns claims for a valid Bearer token |
| `/get-token` | POST | Convenience JSON endpoint for token requests (non-standard) |

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
| `roles` | Each entry in the `roles` array becomes its own claim |
| Custom | Each key in `customClaims` becomes its own claim |

## Development Behavior

`UseLocalBearerTokenInjection` automatically attaches a Bearer token to incoming requests during local development. It activates only in the Development environment and only when all three conditions are met:

- The request has no existing `Authorization` header
- The request originates from localhost
- The request path is not a public endpoint (`/openapi`, `/swagger`, `/scalar`, `/health`, `/`)

Swagger UI, Scalar, and health check endpoints continue to work without authentication, while protected API routes receive a valid token automatically. Token fetches are cached and refreshed 60 seconds before expiry.

## Switching to a Real Provider

When the project is ready to connect to a production identity provider, replace `AddLocalIdentityAuthentication` with your provider's configuration. The client package validates tokens using standard OIDC metadata discovery, so any provider that publishes a `/.well-known/openid-configuration` document is compatible. The `Issuer` and `Audience` values in configuration are the only values that need to change.

## Important Notes

The server package is intended for development and integration testing only. It stores passwords in plaintext, generates an ephemeral RSA signing key on every startup (tokens issued before a restart become invalid), and opens CORS to all origins. Do not deploy it to any environment accessible outside a development machine.
