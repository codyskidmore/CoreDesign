# CoreDesign.Identity.Client

An ASP.NET Core library for **API servers** that need to validate tokens issued by `CoreDesign.Identity.Server`. It configures JWT Bearer authentication, provides a development-only middleware that auto-injects bearer tokens on local requests, and includes an OpenAPI document transformer that advertises the Bearer security scheme.

For Blazor or other browser-based apps, use the OIDC middleware directly (see the server library's README, "Blazor app integration").

## What it provides

| Type | Description |
| --- | --- |
| `IdentityClientExtensions.AddIdentityClient` | Registers JWT Bearer auth, the `IdentityApiClient` HTTP client, and permission-based authorization |
| `IdentityClientExtensions.UseLocalBearerTokenInjection` | Mounts the bearer token injection middleware (development only) |
| `PermissionAuthorizationExtensions.AddPermissionAuthorization` | Registers permission-based authorization as a standalone call, for auth providers that do not go through `AddIdentityClient` |
| `PermissionAuthorizationPolicyProvider` | Dynamically creates an authorization policy for any permission string passed to `RequireAuthorization()` |
| `PermissionAuthorizationHandler` | Checks the `permissions` claim in the bearer token against the required permission |
| `IdentityApiClient` | Fetches and caches access tokens from the identity server |
| `BearerSecurityTransformer` | OpenAPI transformer that adds a Bearer security scheme to authenticated operations |

## Setup

### 1. Register services

Call `AddIdentityClient` during service registration, passing the app's `IConfiguration`. This is typically done conditionally so that production environments use a different auth provider (such as Azure Entra). Permission-based authorization is registered automatically:

```csharp
if (builder.Environment.IsDevelopment())
    builder.Services.AddIdentityClient(builder.Configuration);
else
{
    builder.Services.AddProductionAuthentication(...);
    builder.Services.AddPermissionAuthorization();
}
```

Endpoints declare their required permission by passing a permission string directly to `RequireAuthorization()`:

```csharp
app.MapGet("/items", Handler.HandleAsync)
    .RequireAuthorization("items:read");

app.MapPost("/items", Handler.HandleAsync)
    .RequireAuthorization("items:write");
```

No policy registration is needed. The `PermissionAuthorizationPolicyProvider` creates the policy on demand the first time a given permission string is encountered. The `permissions` claim in the bearer token is checked against the required value. Adding a new permission to a new endpoint requires no changes to the authorization setup.

### 2. Add middleware

Call `UseLocalBearerTokenInjection` in the middleware pipeline after `UseCors` and before `UseAuthentication`. The middleware only activates in the Development environment and only injects tokens on requests that have no existing `Authorization` header and originate from localhost:

```csharp
app.UseCors();
app.UseLocalBearerTokenInjection();
app.UseAuthentication();
app.UseAuthorization();
```

### 3. Add configuration

Two configuration sections are required.

**`IdentityApi`** configures the HTTP client used to fetch tokens:

```json
{
  "IdentityApi": {
    "BaseUrl": "https://localhost:5003",
    "Username": "admin@example.local",
    "Password": "Password1!"
  }
}
```

**`CoreDesign:Identity`** (or `CoreDesign:IdentityWebHost`) provides the issuer and audience used to validate incoming JWTs. These must match the values configured in `CoreDesign.Identity.Server`. The client reads from both section names, with `CoreDesign:IdentityWebHost` taking precedence when present:

```json
{
  "CoreDesign": {
    "IdentityWebHost": {
      "Issuer": "https://localhost:5003",
      "Audience": "https://api.example.local"
    }
  }
}
```

| Key | Description |
| --- | --- |
| `IdentityApi:BaseUrl` | Base URL of the running identity server |
| `IdentityApi:Username` | Credential used by the token injection middleware to obtain a token |
| `IdentityApi:Password` | Credential used by the token injection middleware to obtain a token |
| `CoreDesign:IdentityWebHost:Issuer` (or `CoreDesign:Identity:Issuer`) | Expected `iss` claim on incoming tokens |
| `CoreDesign:IdentityWebHost:Audience` (or `CoreDesign:Identity:Audience`) | Expected `aud` claim on incoming tokens |

### 4. Add the OpenAPI security transformer (optional)

Register `BearerSecurityTransformer` with the OpenAPI document to annotate authenticated endpoints with the Bearer security requirement and enable the Authorize button in Scalar and Swagger UI. Anonymous endpoints are excluded automatically:

```csharp
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer<BearerSecurityTransformer>());
```

## Token injection middleware

`BearerTokenInjectionMiddleware` runs only when:

- The environment is `Development`
- The request has no `Authorization` header
- The request originates from localhost (`127.0.0.1`, `::1`, or `::ffff:127.x`)
- The path is not a public endpoint (`/openapi`, `/swagger`, `/scalar`, `/health`, `/`)

When all conditions are met it calls `IdentityApiClient.GetAccessTokenAsync()`, which fetches a token from `/connect/token` on the identity server and caches it until 60 seconds before expiry. The token is set as the `Authorization: Bearer` header on the current request before passing it to the next middleware.

If token acquisition fails (for example, the identity server is not running) the middleware logs a warning and continues without a token.

## Token validation

JWT Bearer validation is configured with the following parameters:

| Parameter | Value |
| --- | --- |
| Issuer validation | Enabled, matched against the configured issuer |
| Audience validation | Enabled, matched against the configured audience |
| Lifetime validation | Enabled |
| Signing key discovery | Automatic via `/.well-known/openid-configuration` on the identity server |
| Name claim type | `email` |
| Inbound claim mapping | Disabled (`MapInboundClaims = false`) |

## Feedback

Feedback on this package is welcome. If you run into a missing feature, an unexpected behavior, or something that required more effort than it should have, open an issue at [github.com/codyskidmore/CoreDesign/issues](https://github.com/codyskidmore/CoreDesign/issues) or tag [@codyskidmore](https://github.com/codyskidmore). Suggestions about missing features and priority input are especially appreciated.
