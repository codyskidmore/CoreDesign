# CoreDesign.Identity.Server

A lightweight, self-contained OIDC identity server library for ASP.NET Core. It provides a complete set of OIDC-compatible endpoints (discovery, JWKS, token issuance, and userinfo) as minimal API routes that can be mounted on any ASP.NET Core application in a few lines of code.

Intended for **development and testing only**. Passwords are stored in plaintext, the RSA signing key is generated fresh on each startup (ephemeral), and CORS is left open. Do not use in production.

## What it provides

| Endpoint | Purpose |
| --- | --- |
| `GET /.well-known/openid-configuration` | OIDC discovery document |
| `GET /.well-known/jwks.json` | Public signing key (JWKS) |
| `POST /connect/token` | Token issuance via password grant (`application/x-www-form-urlencoded`). Requires a registered `client_id`. |
| `GET /connect/userinfo` | Returns claims for a valid bearer token |
| `POST /get-token` | Convenience JSON token endpoint for tooling (non-standard, no `client_id` required) |
| `POST /auth/login` | Frontend login endpoint — accepts JSON credentials, returns a token (no `client_id` required) |

Tokens are RS256-signed JWTs containing `sub`, `email`, `preferred_username`, `name`, `given_name`, `family_name`, `oid`, `roles`, and any custom claims defined on the identity record.

## Frontend login

`POST /auth/login` is the entry point for browser-based frontends. It accepts JSON credentials and returns a signed JWT that the frontend stores and attaches to API requests as `Authorization: Bearer <token>`.

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

The identity server must have CORS enabled for browsers to reach this endpoint. Call `UseIdentityServerCors()` in the server host's `Program.cs` (see [Setup](#setup)).

## Generating a token manually

`POST /get-token` accepts the same JSON credentials and returns the same response as `/auth/login`. Use it for tooling such as Scalar, Postman, or curl.

**Request**

```http
POST /get-token
Content-Type: application/json

{
  "username": "admin@example.local",
  "password": "Password1!"
}
```

**Response**

```json
{
  "access_token": "<jwt>",
  "id_token": "<jwt>",
  "token_type": "Bearer",
  "expires_in": 28800,
  "scope": "openid profile email"
}
```

Use the `access_token` value as the `Authorization: Bearer <token>` header on subsequent requests.

If the credentials are invalid the endpoint returns `400 Bad Request`:

```json
{
  "error": "invalid_grant",
  "error_description": "Invalid username or password"
}
```

This endpoint is a non-standard convenience route. For standard OAuth 2.0 password grant (form-encoded), use `POST /connect/token` instead.

## Setup

### 1. Create the host project

Add a new ASP.NET Core web project and reference `CoreDesign.Identity.Server`. Set `ContentRootPath` to `AppContext.BaseDirectory` so configuration files are resolved from the build output directory regardless of how the host is launched (required when running under .NET Aspire or `dotnet run` from a directory other than the output folder).

```csharp
using CoreDesign.Identity.Server;

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

### 2. Add configuration

Add a `CoreDesign:Identity` section to `appsettings.json` (or an environment-specific override):

```json
{
  "CoreDesign": {
    "Identity": {
      "Issuer": "https://identity.example.local",
      "Audience": "https://api.example.local",
      "TokenLifetimeHours": 8
    }
  }
}
```

| Key | Default | Description |
| --- | --- | --- |
| `Issuer` | _(empty, required)_ | Value placed in the `iss` claim and returned by the discovery endpoint |
| `Audience` | _(empty, required)_ | Value placed in the `aud` claim |
| `KeyId` | `coredesign-dev-signing-key` | `kid` header value on the JWT and JWKS entry |
| `TokenLifetimeHours` | `8` | Token validity window |

### 3. Create a clients file

Add a `clients.json` file to the project and set it to copy to the output directory:

```xml
<None Update="clients.json">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

The file is a JSON array of registered client (Relying Party) records:

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

| Field | Type | Description |
| --- | --- | --- |
| `clientId` | string | Unique identifier for the client. Case-sensitive. Must be included in every `/connect/token` request as `client_id`. |
| `clientSecret` | string or null | Optional shared secret. Null for public clients (SPAs, mobile apps, server-side token injection using ROPC). |
| `tokenEndpointAuthMethod` | string | `"none"` for public clients, `"client_secret_post"` for confidential clients. |
| `allowedGrantTypes` | string[] | Grant types this client may use. Currently `"password"` is the only supported server-side grant. |
| `allowedRedirectUris` | string[] | Pre-registered redirect URIs. Required for Authorization Code Flow (Milestone 4). |
| `allowedPostLogoutRedirectUris` | string[] | Pre-registered post-logout redirect URIs. Required for End Session (Milestone 6). |
| `allowedScopes` | string[] | Scopes this client is permitted to request. |
| `requirePkce` | bool | When true, `/connect/authorize` will reject requests without a valid `code_challenge`. Set false for ROPC-only clients. |

### 4. Create an identities file

Add an `identities.json` file to the project and set it to copy to the output directory:

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
    "roles": ["Admin", "AppUser"],
    "customClaims": {}
  }
]
```

| Field | Type | Description |
| --- | --- | --- |
| `userId` | string (GUID) | Value used for the `sub` and `oid` claims |
| `username` | string | Login username (case-insensitive comparison) |
| `password` | string | Plaintext password |
| `email` | string | `email` claim |
| `name` | string | `name` claim |
| `givenName` | string | `given_name` claim |
| `familyName` | string | `family_name` claim |
| `roles` | string[] | Each value is emitted as a separate `roles` claim |
| `customClaims` | object | Arbitrary key-value pairs added as additional claims |

## Custom stores

### Custom identity stores

`AddJsonFileIdentityStore` is a convenience wrapper around `IIdentityStore`. To use a different backing source (database, in-memory list, etc.) implement the interface and register it directly:

```csharp
public class MyIdentityStore : IIdentityStore
{
    public Task<IdentityRecord?> FindByCredentialsAsync(string username, string password) { ... }
    public Task<IdentityRecord?> FindByIdAsync(string userId) { ... }
}

builder.Services.AddSingleton<IIdentityStore, MyIdentityStore>();
```

### Custom client stores

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

The `sectionName` parameter controls which configuration section is bound (default: `"CoreDesign:Identity"`):

```csharp
builder.Services.AddIdentityServer(builder.Configuration, sectionName: "MyApp:Auth");
```
