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

| Email | Password | Permissions |
|---|---|---|
| admin@sampleapi.local | Password1! | weather:read, weather:write |
| user@sampleapi.local | Password1! | weather:read |

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

## Deploying to Azure

There are two deployment paths. Use the `azd` path if your team is already set up with the Azure Developer CLI. Use the GitHub Actions path if you are deploying to pre-provisioned Azure resources manually.

### Option A: Azure Developer CLI (azd)

Aspire generates a deployment manifest that `azd` reads to provision all Azure resources and wire up connection strings automatically. The migration service is deployed as an Azure Container App job and runs before the other services start.

Install the Azure Developer CLI and log in:

```
winget install microsoft.azd
azd auth login
```

Provision infrastructure and deploy all services in one command:

```
azd up
```

`azd` creates the Azure SQL Database, injects the connection string into the migration service, runs migrations and seeding, then deploys Sample.Api and Sample.Blazor. No secrets or connection strings need to be handled manually.

### Option B: GitHub Actions

Use this approach when deploying to Azure resources that you provision and manage separately (for example, an existing Azure SQL Database and Azure Container Apps environment).

#### Required GitHub Secrets

| Secret | Value |
| --- | --- |
| `AZURE_CLIENT_ID` | App registration client ID for OIDC federated authentication |
| `AZURE_TENANT_ID` | Azure Entra tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Target subscription ID |
| `AZURE_SQL_CONNECTION_STRING` | Full ADO.NET connection string for the Azure SQL Database |

The Azure credentials use OIDC federated identity so no client secret needs to be stored in GitHub. Set up the federated credential in your app registration with subject `repo:<org>/<repo>:ref:refs/heads/main`.

#### Workflow

```yaml
name: Deploy to Azure

on:
  push:
    branches: [main]

permissions:
  id-token: write
  contents: read

jobs:
  deploy:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'

      - name: Build
        run: dotnet build --configuration Release

      - name: Test
        run: dotnet test --configuration Release --no-build

      - name: Login to Azure
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Run database migrations and seed
        env:
          ConnectionStrings__sample-db: ${{ secrets.AZURE_SQL_CONNECTION_STRING }}
        run: dotnet run --project src/Sample.Data.MigrationService --configuration Release --no-build

      - name: Deploy Sample.Api
        uses: azure/container-apps-deploy-action@v1
        with:
          appSourcePath: ${{ github.workspace }}
          acrName: <your-acr-name>
          containerAppName: sample-api
          resourceGroup: <your-resource-group>
          dockerfilePath: src/Sample.Api/Dockerfile

      - name: Deploy Sample.Blazor
        uses: azure/container-apps-deploy-action@v1
        with:
          appSourcePath: ${{ github.workspace }}
          acrName: <your-acr-name>
          containerAppName: sample-blazor
          resourceGroup: <your-resource-group>
          dockerfilePath: src/Sample.Blazor/Dockerfile
```

#### How the migration step works

`dotnet run` on `Sample.Data.MigrationService` starts the `MigrationWorker<SampleDbContext>` background service. It connects to the Azure SQL Database using the `ConnectionStrings__sample-db` environment variable (the double underscore is the environment variable form of the `ConnectionStrings:sample-db` configuration key), applies any pending EF Core migrations, seeds reference data from the `SeedData/` folder, then calls `StopApplication()` and exits. The process exits with code 0 on success and a non-zero code on failure, which causes the workflow step to fail and stops the deployment before any containers are updated.

The migration step runs before the deploy steps so the database schema is always consistent with the application version being deployed. If migrations fail, the running application is unaffected.

#### Connection string format

For Azure SQL Database with managed identity (recommended):

```
Server=<server>.database.windows.net;Database=sample-db;Authentication=Active Directory Default;Encrypt=True;
```

For Azure SQL Database with username and password (if managed identity is not available):

```
Server=<server>.database.windows.net;Database=sample-db;User Id=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;
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
    "permissions": [ "weather:read", "weather:write" ],
    "customClaims": {}
  }
]
```

`userId` becomes the `sub` and `oid` claims. `permissions` values are emitted as separate `permissions` claims. `customClaims` accepts arbitrary key-value pairs that are added as additional claims.

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

For the complete endpoint list, all configuration keys, custom store implementation, and advanced registration options, see [CoreDesign.Identity.Server/README.md](../src/CoreDesign.Identity/CoreDesign.Identity.Server/README.md).

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

For the full configuration reference, token injection middleware details, and validation parameters, see [CoreDesign.Identity.Client/README.md](../src/CoreDesign.Identity/CoreDesign.Identity.Client/README.md).

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

**DbContext setup** ties everything together. The `DbContext` inherits from EF Core's `DbContext` and overrides `OnModelCreating` with two lines:

```csharp
public class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    public DbSet<WeatherForecast> WeatherForecasts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(nameof(Schemas.Sample));

        // Load all BaseEntityConfiguration<T> implementations from the assembly and apply them.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SampleDbContext).Assembly);
    }
}
```

`HasDefaultSchema` scopes all tables to a named database schema (defined in `Data/Schemas.cs` as an enum so the string is never hard-coded). `ApplyConfigurationsFromAssembly` is an EF Core built-in that scans the assembly for every class that implements `IEntityTypeConfiguration<T>` and calls `Configure` on each one. Because `BaseEntityConfiguration<T>` implements that interface, every concrete configuration class in the project is discovered and applied automatically. No manual registration is required when a new entity and its configuration class are added.

**IReadRepository\<TContext, TEntity\>** and **ICudRepository\<TContext, TEntity\>** are registered in the DI container and injected into services:

```csharp
services.AddTransient<IReadRepository<SampleDbContext, WeatherForecast>,
                      ReadRepository<SampleDbContext, WeatherForecast>>();
services.AddTransient<ICudRepository<SampleDbContext, WeatherForecast>,
                      CudRepository<SampleDbContext, WeatherForecast>>();
```

Services then declare their repository dependencies in the constructor and call the async CRUD methods. The repository interfaces are also easy to mock in unit tests using Moq. The test project covers all handler operations with success, not-found, and failure scenarios.

**MigrationWorker\<TContext\>** is a `BackgroundService` in CoreDesign.Data that handles the full database bootstrap sequence: ensure the database exists, apply pending migrations, seed reference data from JSON files, then shut down the host. `Sample.Data.MigrationService` registers it directly in `Program.cs` with no subclass required:

```csharp
builder.AddMigrationWorker<SampleDbContext>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(MigrationWorker<SampleDbContext>.ActivitySourceName));
```

The worker scans the `SeedData/` directory for `*.json` files and seeds each one automatically. Each filename (without the `.json` extension) must be the **fully qualified type name** of a `BaseEntity` subclass in the `SampleDbContext` assembly. For example, the `WeatherForecast` entity in `Sample.Api.WeatherForecasts.Models` is seeded from a file named `Sample.Api.WeatherForecasts.Models.WeatherForecast.json`. A filename that cannot be resolved to a known entity type is skipped with a warning.

To use a different seed directory, pass it as the second argument:

```csharp
builder.AddMigrationWorker<SampleDbContext>("ReferenceData");
```

`SeedEntitiesAsync<T>` is a protected helper on the base class that inserts records not already present in the database, identified by `BaseEntity.Id`. It calls `IgnoreQueryFilters()` so soft-deleted rows are counted as existing and no duplicate-key errors occur on re-runs. Both the ensure-database and migrate steps wrap their calls in `CreateExecutionStrategy()` for automatic transient-error retry.

For the full repository API, query options, soft-delete behaviour, value converter reference, and custom seed logic guide, see [CoreDesign.Data/README.md](../src/CoreDesign.Data/README.md).

## Logging

### Serilog Setup

The application uses [Serilog](https://serilog.net/) for structured logging. `SerilogExtensions.UseApplicationSerilog` in `Infrastructure/Serilog.cs` configures Serilog on the host so it reads its sinks and minimum levels from `appsettings.json`, enriches every log event with the ambient log context, and forwards events to Application Insights as trace telemetry.

`Program.cs` creates a lightweight bootstrap logger before the host is built so that any startup failures are still captured:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();
```

### Service Logging

Handler classes in this project contain no log statements. All invocation logging is handled by generated logging decorators from `CoreDesign.Logging`.

Placing `[LoggingDecorator]` on a handler interface instructs the Roslyn source generator to emit a decorator class at compile time. The decorator wraps the concrete handler and logs:

- The method name and each parameter before invocation (Information)
- The return value after successful completion (Information)
- A warning when a `OneOf` arm indicates a not-found, bad-request, or other error outcome
- The exception and method name if the call throws (Error)

Both synchronous and `Task`/`Task<T>` methods are fully supported. `ValueTask` and `ValueTask<T>` are also supported. Generic interfaces are supported — the decorator carries the type parameters and constraint clauses of the interface. Properties and indexers are implemented as pass-throughs with no logging.

In Sample.Api, each handler interface is marked with `[LoggingDecorator]`:

```csharp
[LoggingDecorator]
public interface ICreateForecastHandler
{
    Task<OneOf<WeatherForecast, BadRequestMessage>> CreateAsync(
        Request request, Guid userId, CancellationToken ct);
}
```

All generated decorators are registered in one call in `ModuleConfig.cs`:

```csharp
services.DecorateWithLogging();
```

`DecorateWithLogging()` is generated alongside the decorator classes. Adding a new handler interface marked with `[LoggingDecorator]` is sufficient to get consistent, structured log output automatically on the next build.

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

Log output size is controlled through Serilog's destructuring configuration in `appsettings.json` rather than per-method attributes. The `Serilog:Destructure` section sets a maximum depth, maximum string length, and maximum collection count applied uniformly across all handlers and all sinks. The depth limit is the most critical setting: without it, deeply nested object graphs such as EF Core entities with navigation properties will cause Serilog to stream a massive payload and hang. See the `appsettings.json` file for the current values.

For the full attribute reference, generator details, and design rationale, see [CoreDesign.Logging/README.md](../src/CoreDesign.Logging/README.md).

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

## Feedback

Feedback on this sample and the CoreDesign packages is welcome and genuinely respected. If the sample does not demonstrate something you needed to see, or if a pattern here led you down the wrong path, that is worth raising.

Especially useful to hear about:

- Scenarios the sample should cover but does not
- Steps in the setup or deployment that were unclear or incomplete
- Features in the CoreDesign packages that would make this kind of application easier to build

Open an issue at [github.com/codyskidmore/CoreDesign/issues](https://github.com/codyskidmore/CoreDesign/issues) or tag [@codyskidmore](https://github.com/codyskidmore) in an existing issue or discussion. A plain description of what you ran into or what you wish existed is all that is needed.
