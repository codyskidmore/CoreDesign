# Sample

Sample is a .NET 10 microservices solution that demonstrates how to build a REST API using [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/), Entity Framework Core, and the **CoreDesign** family of packages. It serves as a working reference for setting up CoreDesign.Identity.Server, CoreDesign.Identity.Client, and CoreDesign.Data in a real application.

## Projects

### Sample.Aspire.AppHost

The Aspire application host that orchestrates all services for local development. It provisions a SQL Server Docker container with a persistent volume, starts the Identity API and main API, and runs the migration service to prepare the database. Run this project to start the full local environment.

### Sample.Aspire.ServiceDefaults

A shared class library referenced by all service projects. It provides common OpenTelemetry observability configuration, HTTP resilience policies, and service discovery so that each service gets consistent infrastructure behavior without duplicating setup code.

### Sample.Api

The main ASP.NET Core REST API. It exposes weather forecast CRUD endpoints protected by JWT bearer authentication. In development it validates tokens issued by the local Identity server. In production it validates tokens from Azure Entra ID. It uses CoreDesign.Data for repository-pattern data access and CoreDesign.Identity.Client for authentication setup.

### Sample.Identity.Web

A lightweight OAuth2/OIDC identity server built on CoreDesign.Identity.Server. It hosts the login form for Blazor OIDC flows and reads users from a local `identities.json` file. Two accounts are pre-configured:

| Email | Password | Roles |
|---|---|---|
| admin@sampleapi.local | Password1! | DevAdmin, DevAppUsers |
| user@sampleapi.local | Password1! | DevAppUsers |

### Sample.Identity.Api

A second instance of CoreDesign.Identity.Server configured for direct token issuance. It exposes the same user store and a Scalar UI so that tokens can be obtained without going through the browser-based login flow, which is useful when testing Sample.Api directly.

### Sample.Blazor

An ASP.NET Core Blazor Server application that authenticates via OpenID Connect (Authorization Code with PKCE) and calls Sample.Api to display weather forecast data. The auth provider is selected at runtime by `Blazor:AuthProvider` in configuration: `"Local"` targets Sample.Identity.Web, `"AzureEntra"` targets Azure Entra ID. Unauthenticated requests to any protected page are redirected to the identity server's login form by the `RedirectToLogin` component. After sign-in the access token is forwarded to Sample.Api on every API call by `BearerTokenHandler`.

### Sample.Data.MigrationService

A hosted service that runs on startup to ensure the database exists, applies any pending EF Core migrations, and seeds initial data from JSON files in its `SeedData` folder. It is wired into the Aspire AppHost so migrations run automatically before the API receives traffic.

### Sample.Api.Tests

xUnit unit tests for the API service layer. Tests use Moq for repository mocking and Bogus for generating realistic fake data. Coverage includes all CRUD operations with success, not-found, and failure scenarios.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (required for the SQL Server container)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling)

Install the Aspire workload if you have not already:

```
dotnet workload install aspire
```

## Setup

### 1. Add user secrets

The SQL Server container password is supplied via .NET user secrets so it is never stored in source control. Run the following commands from the repository root.

Initialize user secrets for the AppHost project:

```
dotnet user-secrets init --project src/Sample.Aspire.AppHost
```

Set the SQL Server password:

```
dotnet user-secrets set "Parameters:SqlPassword" "my-secret-password" --project src/Sample.Aspire.AppHost
```

### 2. Trust the developer SSL certificate

All services run over HTTPS locally. If you see SSL/certificate errors when the app starts, run:

```
dotnet dev-certs https --trust
```

Restart your browser after running this command.

## Running the Application

Start the Aspire AppHost project:

```
dotnet run --project src/Sample.Aspire.AppHost
```

Aspire will print a dashboard URL to the console (typically `https://localhost:17002`). Open the dashboard to see all running services, logs, and traces.

The services will be available at addresses shown in the dashboard. Sample.Identity.Api exposes a Scalar UI at `/scalar/v1` for obtaining tokens and testing endpoints directly.

## Running Tests

```
dotnet test
```

## CoreDesign Packages

This solution is a working example of how to integrate the CoreDesign library suite. The three packages used are described below.

### CoreDesign.Identity.Server

**Used in**: Sample.Identity.Web, Sample.Identity.Api

Provides a ready-made identity server that issues JWTs for development. Sample.Identity.Web uses the standalone web host pattern, which registers all OIDC endpoints and a landing page with two calls in `Program.cs`:

```csharp
builder.Services.AddIdentityServerWebHost(builder.Configuration);
// ...
app.MapIdentityServerWebHost();
```

`AddIdentityServerWebHost` reads the `CoreDesign:IdentityWebHost` section from configuration, generates or loads a persistent RSA signing key from `%APPDATA%\coredesign-identity\`, and registers the JSON file stores. `MapIdentityServerWebHost` enables CORS, mounts all OIDC endpoints, and serves a landing page at `/`.

Configuration lives under `CoreDesign:IdentityWebHost` in `appsettings.json`:

```json
"CoreDesign": {
  "IdentityWebHost": {
    "Issuer": "https://localhost:5003",
    "Audience": "https://api.sampleapi.local",
    "TokenLifetimeHours": 8,
    "IdentitiesFilePath": "identities.json",
    "ClientsFilePath": "clients.json"
  }
}
```

The `identities.json` and `clients.json` files live in `src/Shared/` and are linked into both identity projects via MSBuild `<Content>` items (see [Shared App Settings](#shared-app-settings)).

#### identities.json

Each record defines one login account. The full field set:

```json
[
  {
    "userId": "11111111-1111-1111-1111-111111111111",
    "username": "admin@sampleapi.local",
    "password": "Password1!",
    "email": "admin@sampleapi.local",
    "name": "Admin User",
    "givenName": "Admin",
    "familyName": "User",
    "roles": [ "DevAdmin", "DevAppUsers" ],
    "customClaims": {}
  }
]
```

`userId` becomes the `sub` and `oid` claims. `roles` values are emitted as separate `roles` claims. `customClaims` accepts arbitrary key-value pairs that are added as additional claims.

#### clients.json

Each record registers one client application. Two clients are pre-configured:

| Client ID | Grant | Purpose |
| --- | --- | --- |
| `sample-blazor` | `authorization_code` (PKCE required) | Blazor UI browser login |
| `sample-api-dev` | `password` | Service-to-service token injection |

The `sample-blazor` client must list the Blazor app's redirect URI exactly. The AppHost pins the Blazor app to port 7070, so the registered URI is `https://localhost:7070/signin-oidc`.

#### Template customization

The login form, error banner, and landing page are rendered from HTML templates embedded in the library. Any of these can be replaced without modifying the library by placing override files in an `identity-templates` folder at the host project's content root.

| Override file | Replaces |
| --- | --- |
| `identity-templates/login.html` | The credential entry form rendered at `GET /connect/authorize` |
| `identity-templates/login-error.html` | The error banner injected into `{{error_alert}}` after a failed login |
| `identity-templates/landing.html` | The status page served at `/` |

The source templates in `CoreDesign.Identity.Server/Templates/` are the recommended starting point. Copy the file you want to change, adjust the markup or CSS, then add it to the project with `CopyToOutputDirectory` set to `PreserveNewest`. The library checks the override folder first on every request with no restart or configuration change required.

Both `login.html` and `login-error.html` support `{{placeholder}}` token substitution. See `src/CoreDesign.Identity/CoreDesign.Identity.Server/README.md` for the full placeholder reference and theming guidance.

### CoreDesign.Identity.Client

**Used in**: Sample.Api

Configures JWT bearer authentication on the consuming API and provides a development middleware that automatically injects bearer tokens from configuration so you can test protected endpoints without manually supplying tokens.

In `Configuration.cs`, authentication is switched by environment:

```csharp
if (builder.Environment.IsDevelopment())
    builder.Services.AddIdentityClient(builder.Configuration);
else
    builder.AddAzureEntraAuthentication();
```

In `App.cs`, the development token injection middleware is added before the auth middleware:

```csharp
app.UseLocalBearerTokenInjection();
app.UseAuthentication();
app.UseAuthorization();
```

The identity server base URL, client ID, and default credentials are read from `appsettings.Development.json` under `IdentityApi`:

```json
"IdentityApi": {
  "BaseUrl": "https://localhost:5003",
  "ClientId": "sample-api-dev",
  "Username": "admin@sampleapi.local",
  "Password": "Password1!"
}
```

`ClientId` must match an entry in the identity server's `clients.json`.

`BearerSecurityTransformer` is registered in `Configuration.cs` to annotate the OpenAPI document with the Bearer security scheme. It automatically excludes any endpoint marked `AllowAnonymous()` so the OpenAPI and Scalar routes appear without a lock icon while all protected endpoints show the Authorize button correctly.

### Sample.Blazor OIDC Authentication

Sample.Blazor uses a pluggable auth provider pattern so the same application can authenticate against either the local identity server or Azure Entra without changing source code. The active provider is selected at startup by the `Blazor:AuthProvider` configuration key.

#### IAuthProviderConfigurator

`IAuthProviderConfigurator` (in `Infrastructure/Auth/`) is an interface with two implementations:

| Implementation | Selected when | Provider |
| --- | --- | --- |
| `LocalOidcAuthConfigurator` | `Blazor:AuthProvider` is `"Local"` (default) | Sample.Identity.Web via Authorization Code with PKCE |
| `AzureEntraAuthConfigurator` | `Blazor:AuthProvider` is `"AzureEntra"` | Azure Entra ID via Microsoft.Identity.Web |

Both implementations set the same cookie paths (`/account/login`, `/account/logout`, `/account/access-denied`) so the rest of the application is unaware of which provider is active. `SupportsFederatedLogout` is checked at sign-out time: the local provider only clears the cookie, while the Azure Entra provider also calls the OIDC end-session endpoint.

The active configurator is registered as a singleton in DI so Razor components can inject `IAuthProviderConfigurator` to read `ProviderName` for display purposes (see `Components/Pages/Home.razor`).

#### Authority resolution in LocalOidcAuthConfigurator

When running under Aspire, `LocalOidcAuthConfigurator` reads the identity server URL from Aspire service discovery. It tries these configuration keys in order, using the first one found:

1. `Blazor:Oidc:Authority` (explicit override in appsettings)
2. `services:SampleIdentityWeb:https:0` (Aspire-injected, IConfiguration normalized form)
3. `services__SampleIdentityWeb__https__0` (Aspire-injected raw env var form)
4. Connection string `"SampleIdentityWeb"`
5. `IdentityApi:BaseUrl` (appsettings fallback for standalone runs)

The AppHost pins Sample.Identity.Web to port 5003 with `WithHttpsEndpoint(port: 5003, isProxied: false)` so the Aspire-injected URL always matches the `Issuer` declared in `appsettings.json`.

#### RedirectToLogin

`Components/RedirectToLogin.razor` redirects unauthenticated users to `/account/login?returnUrl=...` using `NavigationManager`. It is wired into `Routes.razor` as the `NotAuthorized` content of `AuthorizeRouteView`, so any protected page triggers a redirect automatically without per-page login logic.

#### BearerTokenHandler

`Services/BearerTokenHandler.cs` is a delegating handler that attaches the OIDC access token (stored in the auth cookie by `SaveTokens = true`) as a `Bearer` header on every outbound HTTP request to Sample.Api. It is registered as a message handler on the `SampleClient` typed HTTP client in `Configuration.cs`.

#### Blazor authentication configuration

```json
"Blazor": {
  "AuthProvider": "Local",
  "Oidc": {
    "ClientId": "sample-blazor",
    "Scopes": [ "openid", "profile", "email", "https://api.sampleapi.local" ]
  }
}
```

Change `AuthProvider` to `"AzureEntra"` and supply `AzureAd:TenantId`, `AzureAd:ClientId`, and `AzureAd:ClientSecret` (via user secrets) to switch providers. No code changes are needed.

### CoreDesign.Data

**Used in**: Sample.Api, Sample.Data.MigrationService, Sample.Api.Tests

Provides base classes and generic repository interfaces that remove boilerplate from data access.

**BaseEntity** gives every domain model a ULID primary key and standard audit fields:

```csharp
public class WeatherForecast : BaseEntity
{
    public required string Location { get; set; }
    public DateOnly Date { get; set; }
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }
}
```

**BaseEntityConfiguration\<T\>** is the EF Core `IEntityTypeConfiguration` base that applies common column conventions. Extend it for each entity:

```csharp
public class WeatherForecastConfig : BaseEntityConfiguration<WeatherForecast>
{
    public override void Configure(EntityTypeBuilder<WeatherForecast> builder)
    {
        base.Configure(builder);
        builder.ToTable("WeatherForecasts");
        // additional configuration
    }
}
```

**IReadRepository\<TContext, TEntity\>** and **ICudRepository\<TContext, TEntity\>** are registered in the DI container and injected into services:

```csharp
services.AddTransient<IReadRepository<SampleDbContext, WeatherForecast>,
                      ReadRepository<SampleDbContext, WeatherForecast>>();
services.AddTransient<ICudRepository<SampleDbContext, WeatherForecast>,
                      CudRepository<SampleDbContext, WeatherForecast>>();
```

Services then declare their repository dependencies in the constructor and call the async CRUD methods. The repository interfaces are also easy to mock in unit tests using Moq, as shown in `WeatherForecastServiceTests.cs`.

## Logging

### Serilog Setup

The application uses [Serilog](https://serilog.net/) for structured logging. `SerilogExtensions.UseApplicationSerilog` in `Infrastructure/Serilog.cs` configures Serilog on the host so it reads its sinks and minimum levels from `appsettings.json`, enriches every log event with the ambient log context, and forwards events to Application Insights as trace telemetry.

`Program.cs` creates a lightweight bootstrap logger before the host is built so that any startup failures are still captured:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();
```

### Service Logging Middleware

Service classes in this project contain no log statements. All invocation logging is handled centrally by a `DispatchProxy`-based logging middleware in `CoreDesign.Logging`.

`LoggingMiddleware<T>` wraps any interface and intercepts every method call. On each call it logs:

- The method name and serialized parameters before invocation (Information)
- The serialized return value after successful completion (Information)
- A warning when the return value indicates a not-found or bad-request outcome
- The exception and method name if the call throws (Error)

Both synchronous and asynchronous methods are fully supported. For `Task<T>` returns the middleware awaits the result before deciding which log level to use.

Register a service with the middleware using the `AddWithLogging` extension in place of the standard `AddTransient`/`AddScoped` registration:

```csharp
services.AddWithLogging<IWeatherForecastService, WeatherForecastService>();
```

The DI container resolves the interface as the middleware-wrapped version. The concrete service class remains a plain implementation with no logging code. Every operation gets a consistent, structured log record automatically without scattering log calls across the codebase.

Two attributes on the interface give fine-grained control when needed:

| Attribute | Target | Effect |
|---|---|---|
| `[Redact]` | Parameter | Logs `[REDACTED]` in place of the actual value |
| `[Suppress]` | Method | Suppresses all log output for that method |

```csharp
Task<LoginResult> LoginAsync(string username, [Redact] string password);

[Suppress]
Task<string> IssueTokenAsync(string userId);
```

## Project Configuration

### Shared App Settings

`appsettings.json` and `appsettings.Development.json` live in `src/Shared/` and are the single source of truth for configuration that applies across services. Instead of duplicating or copying these files, each project links them directly using MSBuild `<Content>` items in the `.csproj` file:

```xml
<ItemGroup>
    <Content Include="..\Shared\appsettings.json">
        <Link>appsettings.json</Link>
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
    <Content Include="..\Shared\appsettings.Development.json">
        <Link>appsettings.Development.json</Link>
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
</ItemGroup>
```

The `Include` path points to the physical file in `src/Shared/`. The `<Link>` element gives the file a virtual name within the project so it appears as `appsettings.json` at the project root in Solution Explorer and is treated by the runtime exactly like a local file. `PreserveNewest` ensures the file is copied to the build output directory on each build without overwriting a newer copy.

The `identities.json` and `clients.json` files for the identity server also live in `src/Shared/` and are linked the same way into both identity projects.

The following projects use this linking pattern:

| Project | Links shared settings |
|---|---|
| Sample.Api | Yes |
| Sample.Identity.Api | Yes |
| Sample.Identity.Web | Yes |
| Sample.Blazor | Yes |
| Sample.Data.MigrationService | Yes |
| Sample.Aspire.AppHost | Yes |
| Sample.Api.Tests | No (unit tests use mocked repositories and do not load app settings) |

To change a setting that applies to all services, edit the file in `src/Shared/`. The change is immediately reflected in every linked project on the next build without touching any individual project file.

Environment-specific overrides follow the standard `appsettings.{Environment}.json` convention. Secrets (passwords, connection string credentials) are always supplied via user secrets or environment variables and never committed to source control.
