# CoreDesign.Identity.Server

A lightweight, self-contained OIDC identity server library for ASP.NET Core. It provides a complete set of OIDC-compatible endpoints (discovery, JWKS, token issuance, and userinfo) as minimal API routes that can be mounted on any ASP.NET Core application in a few lines of code.

Intended for **development and testing only**. Passwords are stored in plaintext, the RSA signing key is generated fresh on each startup (ephemeral), and CORS is left open. Do not use in production.

## What it provides

| Endpoint | Purpose |
| --- | --- |
| `GET /.well-known/openid-configuration` | OIDC discovery document |
| `GET /.well-known/jwks.json` | Public signing key (JWKS) |
| `POST /connect/token` | Token issuance via password grant (`application/x-www-form-urlencoded`) |
| `GET /connect/userinfo` | Returns claims for a valid bearer token |
| `POST /get-token` | Convenience JSON token endpoint (non-standard) |

Tokens are RS256-signed JWTs containing `sub`, `email`, `name`, `given_name`, `family_name`, `oid`, `roles`, and any custom claims defined on the identity record.

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

builder.Services.AddDcneIdentity(builder.Configuration);
builder.Services.AddJsonFileIdentityStore("identities.json");

var app = builder.Build();

app.MapDcneIdentityEndpoints();

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

### 3. Create an identities file

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

## Custom identity stores

`AddJsonFileIdentityStore` is a convenience wrapper around `IIdentityStore`. To use a different backing source (database, in-memory list, etc.) implement the interface and register it directly:

```csharp
public class MyIdentityStore : IIdentityStore
{
    public Task<IdentityRecord?> FindByCredentialsAsync(string username, string password) { ... }
    public Task<IdentityRecord?> FindByIdAsync(string userId) { ... }
}

builder.Services.AddSingleton<IIdentityStore, MyIdentityStore>();
```

## Advanced registration

`AddDcneIdentity` accepts an optional `Action<IdentityOptions>` to override individual values after configuration binding:

```csharp
builder.Services.AddDcneIdentity(builder.Configuration, configure: opts =>
{
    opts.TokenLifetimeHours = 1;
});
```

The `sectionName` parameter controls which configuration section is bound (default: `"CoreDesign:Identity"`):

```csharp
builder.Services.AddDcneIdentity(builder.Configuration, sectionName: "MyApp:Auth");
```
