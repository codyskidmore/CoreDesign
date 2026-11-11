# CoreDesign.Identity.Server and CoreDesign.Identity.Client

The server is a development OIDC provider that runs inside the solution; the client package validates its tokens in the API and adds permission-based authorization. In production the API validates Entra (or any OIDC provider) tokens instead, and endpoint code does not change.

## Contents

1. How the pieces fit
2. Identity server host
3. identities.json and clients.json
4. API: development authentication
5. Permissions on endpoints
6. API: production with Entra
7. Blazor Server sign-in
8. SPA sign-in (React and similar)
9. Troubleshooting

## 1. How the pieces fit

| Environment | Token issuer | API registration |
|---|---|---|
| Development | CoreDesign.Identity.Server (`https://localhost:5003`) | `AddIdentityClient(configuration)` |
| Everything else | Entra | `AddJwtBearer(...)` + `RolesToPermissionsTransformation` + `AddPermissionAuthorization()` |

Authorization is always "does the token carry a `permissions` claim equal to the string on the endpoint". Development users get permissions from `identities.json`; Entra users get them from app roles, copied into `permissions` claims by a claims transformation.

**The identity server is for development and testing only.** Passwords are plaintext, CORS is open, and the signing key is stored under `%APPDATA%\coredesign-identity\`. Never deploy it.

## 2. Identity server host

A separate web project referencing `CoreDesign.Identity.Server`:

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

Configuration section `CoreDesign:IdentityWebHost`: `Issuer` (must equal the URL the server is reachable at), `Audience` (the API's audience value), `TokenLifetimeHours` (default 8), `IdentitiesFilePath`, `ClientsFilePath`, `KeyId`. Pin the port in the AppHost so the issuer never changes:

```csharp
builder.AddProject<MyApp_Identity_Web>("MyAppIdentityWeb")
    .WithHttpsEndpoint(port: 5003, name: "https", isProxied: false);
```

To embed the endpoints in an existing host instead: `AddIdentityServer(configuration, sectionName: "CoreDesign:Identity")`, `AddJsonFileIdentityStore("identities.json")`, `AddJsonFileClientStore("clients.json")`, then `app.UseIdentityServerCors()` and `app.MapIdentityEndpoints()`. Custom stores: register your own `IIdentityStore` or `IClientStore`.

Endpoints: `/.well-known/openid-configuration`, `/.well-known/jwks.json`, `/connect/authorize` (GET form, POST submit), `/connect/token` (password and authorization_code grants), `/connect/userinfo`, `/auth/login` and `/get-token` (JSON username and password, for SPAs and tools).

The login and landing pages can be restyled by placing `login.html`, `login-error.html` or `landing.html` in an `identity-templates` folder copied to the output directory.

## 3. identities.json and clients.json

Both files must be copied to the output directory. Each identity:

```json
{
  "userId": "11111111-1111-1111-1111-111111111111",
  "username": "admin@myapp.local",
  "password": "Password1!",
  "email": "admin@myapp.local",
  "name": "Admin User",
  "givenName": "Admin",
  "familyName": "User",
  "permissions": [ "orders:read", "orders:write" ],
  "customClaims": { "tenant": "contoso" }
}
```

`userId` becomes both `sub` and `oid`, which is what `ICurrentUserAccessor` reads. Each permission becomes its own `permissions` claim. Create one identity per role you need to test (an admin, a read-only user, a user with no permissions).

Clients: a password-grant client for the API's development token injection, and one `authorization_code` client with `requirePkce: true` per browser app:

```json
{
  "clientId": "myapp-blazor",
  "tokenEndpointAuthMethod": "none",
  "allowedGrantTypes": [ "authorization_code" ],
  "allowedRedirectUris": [ "https://localhost:7070/signin-oidc" ],
  "allowedPostLogoutRedirectUris": [ "https://localhost:7070/signout-callback-oidc", "https://localhost:7070/" ],
  "allowedScopes": [ "openid", "profile", "email", "https://api.myapp.local" ],
  "requirePkce": true
}
```

Redirect URIs are matched exactly, so the browser app needs a fixed port too.

## 4. API: development authentication

```csharp
using CoreDesign.Identity.Client;

if (builder.Environment.IsDevelopment())
    builder.Services.AddIdentityClient(builder.Configuration);   // JWT bearer + permission authorization
```

Configuration read by the client: `CoreDesign:IdentityWebHost:Issuer` and `Audience` (or `CoreDesign:Identity:*`; IdentityWebHost wins), and `IdentityApi:BaseUrl`, `IdentityApi:ClientId`, `IdentityApi:Username`, `IdentityApi:Password` for token injection.

Middleware order:

```csharp
app.UseCors();
app.UseLocalBearerTokenInjection();
app.UseAuthentication();
app.UseAuthorization();
```

`UseLocalBearerTokenInjection` adds a bearer token for the configured user to requests that are local, in Development, have no `Authorization` header, and are not public paths (`/openapi`, `/scalar`, `/swagger`, `/health`, `/`). That makes Scalar and curl work without signing in. To test as a different user, send your own token (from `/get-token`) and the middleware steps aside.

Token validation uses `MapInboundClaims = false` and `email` as the name claim, so claims keep their JWT names (`oid`, `email`, `permissions`).

OpenAPI: `builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecurityTransformer>());` adds the Bearer scheme to every operation except `AllowAnonymous` ones. It requires `Microsoft.OpenApi` 3.x.

## 5. Permissions on endpoints

```csharp
public static class Permissions
{
    public const string OrdersRead  = "orders:read";
    public const string OrdersWrite = "orders:write";
}

group.MapGet(Paths.Orders.GetAll, handler).RequireAuthorization(Permissions.OrdersRead);
group.MapPost(Paths.Orders.Create, handler).RequireAuthorization(Permissions.OrdersWrite);
```

No policy registration is needed; `PermissionAuthorizationPolicyProvider` builds a policy for any string on first use. Keep the strings in one class: they must match `identities.json` and the Entra app role values exactly, and a typo fails as a 403 at runtime, not at build time. Mark public endpoints `.AllowAnonymous()`.

The current user's ID in an endpoint is the `oid` claim:

```csharp
public static Guid GetUserId(this HttpContext context) =>
    Guid.TryParse(context.User.FindFirst("oid")?.Value, out var id)
        ? id
        : throw new InvalidOperationException("User ID (oid) claim not found or invalid");
```

## 6. API: production with Entra

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"{instance}/{tenantId}/v2.0";
        options.Audience = audience;            // api://<client-id>
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, NameClaimType = "email"
        };
    });
builder.Services.AddScoped<IClaimsTransformation, RolesToPermissionsTransformation>();
builder.Services.AddPermissionAuthorization();
```

```csharp
public class RolesToPermissionsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var role in principal.FindAll("roles").ToList())
            identity.AddClaim(new Claim("permissions", role.Value));
        return Task.FromResult(principal);
    }
}
```

Entra setup: an app registration for the API, Application ID URI `api://<client-id>` (the audience), a scope such as `access_as_user`, one app role per permission string with the **Value** equal to the string, users or groups assigned to roles under Enterprise applications, and the `email` optional claim if the API needs it. Each client app gets its own registration with API permission to that scope.

## 7. Blazor Server sign-in

```csharp
builder.Services.AddAuthentication(o =>
    {
        o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(o => { o.LoginPath = "/account/login"; o.LogoutPath = "/account/logout"; })
    .AddOpenIdConnect(o =>
    {
        o.Authority = authority;                 // https://localhost:5003 in development
        o.ClientId = "myapp-blazor";
        o.ResponseType = "code";
        o.UsePkce = true;
        o.SaveTokens = true;
        o.GetClaimsFromUserInfoEndpoint = true;
        o.Scope.Clear();
        o.Scope.Add("openid"); o.Scope.Add("profile"); o.Scope.Add("email"); o.Scope.Add("https://api.myapp.local");
    });
```

Map `/account/login` (challenge with a `RedirectUri`, `AllowAnonymous`) and `/account/logout` (sign out of both schemes). Forward the saved access token to the API with a `DelegatingHandler` that reads `HttpContext.GetTokenAsync("access_token")` and sets the `Authorization` header, registered with `AddHttpClient<ApiClient>(...).AddHttpMessageHandler<BearerTokenHandler>()`. In production, point the same OIDC setup at Entra; the API calls do not change.

## 8. SPA sign-in (React and similar)

Register an `authorization_code` client with PKCE and the SPA's exact redirect URI (for Vite, `http://localhost:5173/`), add the SPA origin to `Cors:AllowedOrigins` in the API, and use any standard OIDC client library against the identity server's discovery document. For quick prototypes, `POST /auth/login` with JSON credentials returns an access token directly. In production, use MSAL against Entra with the API scope.

## 9. Troubleshooting

| Symptom | Likely cause |
|---|---|
| 401 on every request in Development | Issuer or audience mismatch: `CoreDesign:IdentityWebHost:Issuer` must equal the identity server URL, and `Audience` must match on both sides |
| 401 after restarting the identity server on a new port | Port not pinned in the AppHost |
| 403 on an endpoint | The user's `permissions` lack the exact string; check `identities.json`, the Entra role Value, or that `RolesToPermissionsTransformation` is registered |
| Token injection does nothing | Not Development, request not from localhost, an `Authorization` header already present, or `UseLocalBearerTokenInjection` placed after `UseAuthentication` |
| Browser login fails with a redirect error | Redirect URI in `clients.json` does not exactly match the app's URL and port |
| IDX20804 metadata error in production | The API cannot reach the Entra metadata endpoint, or `Instance` and `TenantId` do not form a valid authority |
