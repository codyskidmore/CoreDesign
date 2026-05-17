# Sample.Identity.Web

A standalone development identity provider built on `CoreDesign.Identity.Server`. It hosts the full OIDC endpoint suite including the browser login form (`/connect/authorize`), so that a Blazor app can authenticate against it using Authorization Code with PKCE.

This project is for **local development only**. It is orchestrated by the Aspire AppHost and pinned to port 5003 so the `Issuer` URL in `appsettings.Development.json` stays stable across restarts.

## How it works

`Program.cs` is three lines:

```csharp
builder.Services.AddIdentityServerWebHost(builder.Configuration);
// ...
app.MapIdentityServerWebHost();
```

`AddIdentityServerWebHost` reads `CoreDesign:IdentityWebHost` from configuration, generates or loads a persistent RSA signing key from `%APPDATA%\coredesign-identity\`, and registers the JSON file stores. `MapIdentityServerWebHost` enables CORS, mounts all OIDC endpoints, and adds a landing page at `/`.

## Configuration

Configuration comes from the shared `appsettings.Development.json` (linked into the project). The relevant section:

```json
{
  "CoreDesign": {
    "IdentityWebHost": {
      "Issuer": "https://localhost:5003",
      "Audience": "https://api.sampleapi.local",
      "TokenLifetimeHours": 8,
      "IdentitiesFilePath": "identities.json",
      "ClientsFilePath": "clients.json"
    }
  }
}
```

The `Issuer` must match the URL at which this project runs (port 5003). The `Audience` must match the value configured in the API project's JWT validation settings.

## Users

`identities.json` defines the available login accounts. Two accounts are pre-configured:

| Username | Password | Permissions |
| --- | --- | --- |
| admin@sampleapi.local | Password1! | weather:read, weather:write |
| user@sampleapi.local | Password1! | weather:read |

The file lives in `src/Shared/` and is linked into the project via an MSBuild `<Content>` item. Each record carries the full claim set:

```json
{
  "userId": "11111111-1111-1111-1111-111111111111",
  "username": "admin@sampleapi.local",
  "password": "Password1!",
  "email": "admin@sampleapi.local",
  "name": "Admin User",
  "givenName": "Admin",
  "familyName": "User",
  "permissions": [ "weather:read", "weather:write" ],
  "customClaims": {}
}
```

`userId` becomes the `sub` and `oid` JWT claims. `customClaims` accepts arbitrary key-value pairs for additional claims.

## Clients

`clients.json` registers the applications that are allowed to obtain tokens. Two clients are pre-configured:

| Client ID | Grant | Purpose |
| --- | --- | --- |
| `sample-blazor` | `authorization_code` (PKCE required) | Blazor UI browser login |
| `sample-api-dev` | `password` | Service-to-service token injection |

The `sample-blazor` client must list the Blazor app's redirect URI exactly. The Blazor app is pinned to port 7070 by the AppHost, so the entry is `https://localhost:7070/signin-oidc`.

The `clients.json` file also lives in `src/Shared/` and is linked into the project via an MSBuild `<Content>` item. See the root `README.md` for the full clients.json field reference.

## Template customization

The login form, error banner, and landing page are rendered from HTML templates embedded in the library. Override any of them by placing replacement files in an `identity-templates` folder at this project's content root and setting `CopyToOutputDirectory` to `PreserveNewest`:

| Override file | Replaces |
| --- | --- |
| `identity-templates/login.html` | The credential entry form at `GET /connect/authorize` |
| `identity-templates/login-error.html` | The error banner shown after a failed login |
| `identity-templates/landing.html` | The status page at `/` |

The source templates in `CoreDesign.Identity.Server/Templates/` are the recommended starting point. Both `login.html` and `login-error.html` support `{{placeholder}}` token substitution. The library checks the override folder first on every request, so changes take effect immediately without a restart.

See `src/CoreDesign.Identity/CoreDesign.Identity.Server/README.md` for the full placeholder reference and theming guidance.

## Running standalone

The AppHost starts this project automatically. To run it in isolation:

```
dotnet run --project src/Sample.Identity.Web
```

Then browse to `https://localhost:5003` for the landing page, or `https://localhost:5003/.well-known/openid-configuration` for the discovery document.

## Full documentation

See `src/CoreDesign.Identity/CoreDesign.Identity.Server/README.md` for complete library documentation including all endpoints, configuration options, and Blazor integration details.
