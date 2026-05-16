# CoreDesign User Guide

CoreDesign is a collection of reusable .NET 10 libraries for data access, shared infrastructure, logging, and identity. Each package is independent and can be referenced on its own. This guide consolidates all documentation across the repository into a single reference.

## Table of Contents

1. [Libraries Overview](#1-libraries-overview)
2. [CoreDesign.Shared](#2-coredesignshared)
3. [CoreDesign.Data](#3-coredesigndata)
4. [CoreDesign.Logging](#4-coredesignlogging)
5. [CoreDesign.Identity](#5-coredesignidentity)
   - [CoreDesign.Identity.Server](#51-coredesignidentityserver)
   - [CoreDesign.Identity.Client](#52-coredesignidentityclient)
6. [Azure Entra (Production Authentication)](#6-azure-entra-production-authentication)
7. [Shared appsettings Configuration](#7-shared-appsettings-configuration)
8. [Local NuGet Development](#8-local-nuget-development)
9. [Sample Application](#9-sample-application)
   - [Project Overview](#91-project-overview)
   - [Prerequisites and Setup](#92-prerequisites-and-setup)
   - [Sample.Identity.Web](#93-sampleidentityweb)
   - [Sample.Api](#94-sampleapi)
   - [Sample.Blazor](#95-sampleblazor)
   - [Sample.Data.MigrationService](#96-sampledatamigrationservice)
   - [Deployment](#97-deployment)
10. [Vertical Slice Architecture Reference](#10-vertical-slice-architecture-reference)
11. [Release Notes](#11-release-notes)
12. [Feedback](#12-feedback)

---

## 1. Libraries Overview

| Package | Location | Purpose |
|---|---|---|
| `CoreDesign.Shared` | `src/CoreDesign.Shared/` | Shared infrastructure, error result types, and utility extension methods |
| `CoreDesign.Data` | `src/CoreDesign.Data/` | Generic EF Core data access layer with repository pattern and migration worker |
| `CoreDesign.Logging` | `src/CoreDesign.Logging/` | DispatchProxy-based logging middleware that instruments service interfaces automatically |
| `CoreDesign.Identity.Server` | `src/CoreDesign.Identity/CoreDesign.Identity.Server/` | Self-contained OIDC identity server for development and testing |
| `CoreDesign.Identity.Client` | `src/CoreDesign.Identity/CoreDesign.Identity.Client/` | ASP.NET Core JWT Bearer authentication client and token injection middleware |

---

## 2. CoreDesign.Shared

Shared infrastructure, error result types, and utility extension methods for .NET projects. Intended to be referenced by both host/API projects and Aspire AppHost projects.

### Requirements

- .NET 10.0
- Microsoft.EntityFrameworkCore 10.x
- Microsoft.Extensions.Configuration.Abstractions 10.x
- Microsoft.Extensions.Configuration.Json 10.x
- Microsoft.Extensions.DependencyInjection 10.x
- Microsoft.Extensions.Options 10.x
- Aspire.Hosting.AppHost 13.x
- Ulid 1.4.x

### Installation

```bash
dotnet add package CoreDesign.Shared
```

Or add directly to the `.csproj`:

```xml
<ItemGroup>
    <PackageReference Include="CoreDesign.Shared" Version="*" />
</ItemGroup>
```

### What Is Included

#### DatabaseOptions

A record that holds the database connection settings read from configuration:

| Property | Description |
|---|---|
| `HostName` | Container or server host name (the Aspire resource name) |
| `DatabaseName` | Name of the database |
| `HostPort` | Port the host is listening on |
| `ConnectionStringName` | Named connection string key |

#### Configuration Extension Methods

| Method | Target | Description |
|---|---|---|
| `AddDatabaseConfiguration` | `IHostApplicationBuilder` | Binds the `DatabaseOptions` section from `appsettings.json` into the options system |
| `AddAppSettings` | `IHostApplicationBuilder` | Loads `appsettings.json` and the environment-specific `appsettings.{Environment}.json` |
| `AddAppSettings` | `IDistributedApplicationBuilder` | Same as above, for use in Aspire AppHost projects |

#### Error Result Records

Lightweight result records for use with discriminated unions (e.g., `OneOf`):

| Type | Use case |
|---|---|
| `NotFoundMessage` | Resource could not be found |
| `BadRequestMessage` | The request was invalid |
| `ErrorMessage` | A general error occurred |
| `InvalidOperationMessage` | The operation is not valid in the current state |

Each record carries a single `string Message` property.

#### ObjectExtensionMethods

| Method | Description |
|---|---|
| `ToJson<T>()` | Serializes any object to a JSON string, ignoring circular references |
| `SaveToJsonFile<T>(filePath)` | Serializes an object and writes it to a file at the given path |
| `DeepClone<T>(options?)` | Deep-clones an object via JSON round-trip; returns `null` if the source is `null` |

Note: `DeepClone` requires the type to be serializable by `System.Text.Json`. Types with non-default constructors may need a `[JsonConstructor]` attribute.

#### StringExtensionMethods

| Method | Description |
|---|---|
| `LoadObjectFromJsonFile<T>()` | Reads a JSON file at the given path and deserializes it to `T` |
| `To<T>()` | Converts a string to any type `T` using `TypeDescriptor`, throwing `ArgumentException` if the conversion is not supported or fails |
| `PadRightWithZeros(totalWidth)` | Pads a string to `totalWidth` characters using `'0'` on the right; returns all zeros if input is null or empty |

### Setup

Add the `DatabaseOptions` section to `appsettings.json`:

```json
{
  "DatabaseOptions": {
    "HostName": "my-sql-server",
    "DatabaseName": "MyDatabase",
    "HostPort": 1433,
    "ConnectionStringName": "MyDatabase"
  }
}
```

Register configuration in the host:

```csharp
builder.AddAppSettings();
builder.AddDatabaseConfiguration();
```

Then resolve `DatabaseOptions` wherever needed:

```csharp
var dbOptions = builder.Configuration
    .GetSection(nameof(DatabaseOptions))
    .Get<DatabaseOptions>();
```

### Usage Examples

**Error results with OneOf**

```csharp
public async Task<OneOf<Widget, NotFoundMessage>> GetAsync(Ulid id, CancellationToken ct)
{
    var widget = await repository.GetAsync(w => w.Id == id, null, ct);
    if (widget is null)
        return new NotFoundMessage($"Widget {id} not found.");
    return widget;
}
```

**Deep clone**

```csharp
var copy = original.DeepClone();
```

**JSON file round-trip**

```csharp
myObject.SaveToJsonFile("output.json");
var loaded = "output.json".LoadObjectFromJsonFile<MyObject>();
```

**String conversion**

```csharp
var number = "42".To<int>();
var date = "2026-01-15".To<DateOnly>();
```

---

## 3. CoreDesign.Data

A generic, reusable Entity Framework Core data access layer providing base entity infrastructure, repository abstractions, and a migration worker base class for .NET projects.

### Requirements

- .NET 10.0
- Microsoft.EntityFrameworkCore 10.x
- Microsoft.EntityFrameworkCore.SqlServer 10.x
- Microsoft.Extensions.Hosting.Abstractions 10.x
- Ulid 1.4.x

### Installation

```bash
dotnet add package CoreDesign.Data
```

### What Is Included

**Infrastructure**

- `BaseEntity` — Base class all entities must inherit from. Provides `Id` (Ulid), `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, and `IsDeleted` audit fields.
- `BaseEntityConfiguration<T>` — EF Core `IEntityTypeConfiguration<T>` base that wires up primary key, index, soft-delete query filter, and required audit field constraints.
- `BaseEntityExtensionMethods` — Extension methods `InitializeAuditFields` and `UpdateAuditFields` for setting audit fields on insert and update.
- `ValueConverters` — Provides `GetUlidConverter()` (Ulid to string) and `GetEnumConverter<TEnum>()` (enum to string) for use in entity configurations.
- `MigrationWorker<TContext>` — Concrete `BackgroundService` that ensures the database exists, applies pending EF Core migrations, seeds from JSON files, then stops the host. No subclassing is required for convention-based JSON seeding.

**Interfaces**

- `IReadRepository<TContext, T>` — Read-only repository interface with `GetAllAsync`, `GetAllAttachedAsync`, `GetAsync`, and `GetAttachedAsync`.
- `ICudRepository<TContext, T>` — Create/Update/Delete repository interface with `InsertAsync`, `InsertRangeAsync`, `UpdateAsync`, `UpdateRangeAsync`, `DeleteAsync`, and `DeleteRangeAsync`.

**Repositories**

- `ReadRepository<TContext, T>` — Concrete read repository. All queries use `AsNoTracking()` by default. Supports optional `where` expressions, `orderBy`, and strongly typed `includes`.
- `CudRepository<TContext, T>` — Concrete CUD repository. Soft-deletes by setting `IsDeleted = true` rather than removing rows.

### Setup

**1. Define entities**

All entities must inherit from `BaseEntity`:

```csharp
public class Widget : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}
```

**2. Configure entities**

Inherit from `BaseEntityConfiguration<T>` and call `base.Configure(builder)`:

```csharp
public class WidgetConfiguration : BaseEntityConfiguration<Widget>
{
    public override void Configure(EntityTypeBuilder<Widget> builder)
    {
        base.Configure(builder);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
    }
}
```

**3. Register the DbContext**

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

`ApplyConfigurationsFromAssembly` scans the assembly for every class that implements `IEntityTypeConfiguration<T>` and calls `Configure` on each one automatically. No manual registration is needed when new entity configuration classes are added.

**4. Register repositories**

```csharp
services.AddTransient<IReadRepository<AppDbContext, Widget>, ReadRepository<AppDbContext, Widget>>();
services.AddTransient<ICudRepository<AppDbContext, Widget>, CudRepository<AppDbContext, Widget>>();
```

### Usage Examples

**Querying**

```csharp
// All widgets, no tracking
var all = await readRepository.GetAllAsync();

// Filtered and ordered, with related entities included
var active = await readRepository.GetAllAsync(
    whereExpression: w => w.Name.StartsWith("A"),
    orderBy: q => q.OrderBy(w => w.Name),
    includes: q => q.Include(w => w.Parts).ThenInclude(p => p.Supplier));

// Single entity
var widget = await readRepository.GetAsync(w => w.Id == id);
```

**Writing**

```csharp
// Insert
var widget = new Widget { Name = "Sprocket" };
await cudRepository.InsertAsync(widget, userId, cancellationToken);

// Update
widget.Name = "Updated Sprocket";
await cudRepository.UpdateAsync(widget, userId, cancellationToken);

// Soft delete (sets IsDeleted = true, row is not removed)
await cudRepository.DeleteAsync(id, userId, cancellationToken);
```

### Notes

- `BaseEntityConfiguration` applies a global query filter (`e => !e.IsDeleted`) to every entity. Soft-deleted rows are automatically excluded from all queries.
- `GetAllAttachedAsync` and `GetAttachedAsync` return tracked entities for use when EF Core change detection is needed without an explicit `Attach` call.
- `InitializeAuditFields` must be called on new entities before insert; `CudRepository` handles this automatically when using `InsertAsync` or `InsertRangeAsync`.

### Migration Worker

`MigrationWorker<TContext>` is a concrete `BackgroundService` that works in any .NET host application, including .NET Aspire migration services. It runs three steps in order when the host starts:

1. **Ensure database** — creates the database if it does not exist.
2. **Migrate** — applies all pending EF Core migrations via `MigrateAsync`.
3. **Seed** — scans the seed directory for `*.json` files and inserts any records that do not yet exist.

When all steps complete, it calls `IHostApplicationLifetime.StopApplication()` and the process exits with code 0. If any step throws, the exception propagates and the process exits with a non-zero code, blocking deployment pipelines from proceeding.

Both the ensure and migrate steps wrap their database calls in `CreateExecutionStrategy()` so transient SQL Server errors are retried automatically.

#### Registration

```csharp
builder.AddMigrationWorker<AppDbContext>();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(MigrationWorker<AppDbContext>.ActivitySourceName));
```

Pass a custom seed directory as the second argument:

```csharp
builder.AddMigrationWorker<AppDbContext>("ReferenceData");
```

The path is relative to the working directory. If the directory does not exist at runtime the worker logs a warning and skips seeding without throwing.

#### Seed File Naming Convention

By default, `MigrationWorker<TContext>` scans a directory named `SeedData` for `*.json` files. Each file must be named after the **fully qualified type name** of the entity it seeds. The worker resolves the filename (without the `.json` extension) to a type in `typeof(TContext).Assembly`.

For an entity `MyApp.Orders.Models.Order` the seed file must be named:

```
MyApp.Orders.Models.Order.json
```

Files whose name cannot be resolved to a `BaseEntity` subclass are skipped with a warning.

#### Custom Seed Logic

Override `SeedAsync` to replace or extend the default directory-scanning behavior:

```csharp
public class AppMigrationWorker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    ILogger<AppMigrationWorker> logger)
    : MigrationWorker<AppDbContext>(serviceProvider, lifetime, logger)
{
    protected override async Task SeedAsync(AppDbContext dbContext, CancellationToken ct)
    {
        await SeedFromDirectoryAsync(dbContext, "SeedData", typeof(AppDbContext).Assembly, ct);
        await SeedEntitiesAsync(dbContext, GetAdminUsers(), ct);
    }
}
```

Register the subclass directly as a hosted service:

```csharp
builder.Services.AddHostedService(sp =>
    new AppMigrationWorker(
        sp,
        sp.GetRequiredService<IHostApplicationLifetime>(),
        sp.GetRequiredService<ILogger<AppMigrationWorker>>()));
```

#### Running Outside Aspire

`MigrationWorker<TContext>` has no dependency on the Aspire AppHost. In a GitHub Actions pipeline, supply the connection string as an environment variable and run the migration project with `dotnet run`:

```yaml
- name: Run migrations
  env:
    ConnectionStrings__my-db: ${{ secrets.AZURE_SQL_CONNECTION_STRING }}
  run: dotnet run --project src/MyApp.MigrationService --configuration Release --no-build
```

---

## 4. CoreDesign.Logging

`CoreDesign.Logging` provides a `DispatchProxy`-based logging middleware that wraps any service interface and automatically logs every method invocation, return value, and exception. Service classes stay free of log statements while still producing structured, consistent log output for every operation.

### Installation

```bash
dotnet add package CoreDesign.Logging
```

### Usage

Replace the standard `AddTransient` (or `AddScoped`) call with `AddWithLogging`:

```csharp
services.AddWithLogging<IWeatherForecastService, WeatherForecastService>();
```

The DI container will resolve `IWeatherForecastService` as a proxy-wrapped instance. The concrete class needs no changes.

Pass a different lifetime when needed:

```csharp
services.AddWithLogging<IMyService, MyService>(ServiceLifetime.Scoped);
```

### Log Levels

| Situation | Level |
|---|---|
| Method called | Information (method name and serialized parameters) |
| Method returned a success result | Information (method name and serialized return value, truncated to 500 chars by default) |
| Method returned `NotFoundMessage` or `BadRequestMessage` | Warning |
| Method threw an exception | Error (exception and method name) |

Both synchronous and `Task`/`Task<T>` methods are fully supported.

### Sensitive Data Control

#### `[Redact]`

Apply `[Redact]` to any parameter on the interface method that should not appear in logs. The middleware replaces that argument with `"[REDACTED]"` while still logging all other parameters normally. The actual value is passed to the implementation unchanged.

```csharp
public interface IAuthService
{
    Task<LoginResult> LoginAsync(string username, [Redact] string password);
}
```

#### `[Suppress]`

Apply `[Suppress]` to a method on the interface to skip all logging for that method. No invocation, result, or exception entries are written.

```csharp
public interface ITokenService
{
    [Suppress]
    Task<string> IssueTokenAsync(string userId);
}
```

Use `[Suppress]` when the method name or parameter shape itself would be too revealing, or when call volume is high enough that per-call logging creates more noise than value.

#### `[TruncateLog]`

Return values are serialized to JSON and truncated at 500 characters by default. Any result longer than this limit is cut and a note is appended:

```
WeatherForecastService.GetAllAsync returned [{"id":"..."}... [truncated, total 3842 chars]
```

Apply `[TruncateLog]` to a method on the interface to override the limit:

```csharp
public interface IWeatherForecastService
{
    [TruncateLog(2000)]
    Task<IReadOnlyList<WeatherForecast>> GetAllAsync(CancellationToken ct);

    [TruncateLog(0)]
    Task<ServiceStatus> GetStatusAsync();
}
```

`[TruncateLog(0)]` disables truncation entirely. Parameters are not truncated; use `[Redact]` to suppress a sensitive parameter.

### Dependencies

- `CoreDesign.Shared` for `NotFoundMessage` and `BadRequestMessage` result types
- `OneOf` for discriminated-union result inspection
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.DependencyInjection.Abstractions`

### LoggingMiddleware vs Serilog and Serilog.Enrichers.Sensitive

These tools operate at fundamentally different layers and are not alternatives to each other.

**Serilog** is a logging pipeline. It handles formatting, enrichment, filtering, and routing entries to sinks (files, Seq, Application Insights, etc.). It has no concept of the service layer and no mechanism to intercept method calls. It is the "how and where" of log output.

**Serilog.Enrichers.Sensitive** works inside that pipeline. It scans already-formed log messages for patterns (regex or property names) and masks matches before they reach a sink. It does not know what triggered the log entry or where in the application it came from.

**LoggingMiddleware** operates one layer up, at the point where application code calls services. It intercepts every method invocation before it happens, which gives it three things the others cannot offer:

- **Structural awareness.** It knows the method name, the parameter names, and the call site. `[Redact]` is declared on the interface alongside the parameter it protects.
- **Intentional suppression.** `[Suppress]` removes a method from logging entirely. A regex enricher cannot suppress a log entry that was already written.
- **Zero instrumentation in service classes.** Services contain no log statements and no awareness of observability at all.

The right mental model is that these are complementary:

| Layer | Tool | Responsibility |
|---|---|---|
| Service boundary | LoggingMiddleware | Intercepts calls, logs method invocations and results |
| Logging pipeline | Serilog | Routes and formats entries to sinks |
| Pipeline safety net | Serilog.Enrichers.Sensitive | Masks sensitive values from code outside your control |

Using all three together gives intentional logging at the service layer, flexible output routing, and a defensive backstop for third-party libraries and ad-hoc log statements that fall outside the proxy.

---

## 5. CoreDesign.Identity

CoreDesign.Identity is a pair of NuGet packages that let development teams drop an OIDC-compatible identity gateway directly into a solution. Teams can authenticate and authorize requests from day one without standing up Keycloak, Okta, Azure AD B2C, or any other external provider. When the project is ready for a real gateway, the client package connects to it through standard OIDC discovery, so nothing in the application code changes.

| Package | Purpose |
|---|---|
| `CoreDesign.Identity.Server` | A minimal, self-contained OIDC server that runs inside your solution. Intended for development and testing only. |
| `CoreDesign.Identity.Client` | ASP.NET Core middleware and helpers that configure JWT Bearer authentication against any OIDC provider, with automatic token injection for local development. |

### 5.1 CoreDesign.Identity.Server

A lightweight, self-contained OIDC identity server library for ASP.NET Core. It provides a complete set of OIDC-compatible endpoints as minimal API routes that can be mounted on any ASP.NET Core application in a few lines of code.

**Intended for development and testing only.** Passwords are stored in plaintext, the RSA signing key is persisted to `%APPDATA%\coredesign-identity\` across restarts, and CORS is left open. Do not use in production.

#### Endpoints

| Endpoint | Purpose |
|---|---|
| `GET /.well-known/openid-configuration` | OIDC discovery document |
| `GET /.well-known/jwks.json` | Public signing key (JWKS) |
| `GET /connect/authorize` | Renders the login form for browser-based OIDC flows |
| `POST /connect/authorize` | Processes login form submission and issues authorization code |
| `POST /connect/token` | Token issuance via password grant or authorization code grant (`application/x-www-form-urlencoded`) |
| `GET /connect/userinfo` | Returns claims for a valid bearer token |
| `POST /get-token` | Convenience JSON token endpoint for tooling (Postman, Scalar, curl) |
| `POST /auth/login` | Direct JSON login endpoint for non-OIDC frontends |

Tokens are RS256-signed JWTs containing `sub`, `email`, `preferred_username`, `name`, `given_name`, `family_name`, `oid`, `permissions`, and any custom claims defined on the identity record.

#### Token Claims

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

The token endpoint issues two distinct JWTs per successful authentication:

- **Access token** — audience is the API resource (`CoreDesign:Identity:Audience`). Presented to the API on every request.
- **ID token** — audience is the `client_id`. Contains the `nonce` from the authorization request. Consumed by the client application only.

#### Setup: Option A (Standalone Web Host, Recommended)

Use `AddIdentityServerWebHost` and `MapIdentityServerWebHost`. This reads all configuration from a single `CoreDesign:IdentityWebHost` section, registers the JSON file stores, enables CORS, and adds a landing page at `/`.

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
|---|---|---|
| `Issuer` | (required) | Value placed in the `iss` claim and returned by the discovery endpoint. Must match the URL at which the identity server is reachable. |
| `Audience` | (required) | Value placed in the `aud` claim of access tokens. Typically the API's base URL. |
| `KeyId` | `coredesign-dev-signing-key` | `kid` header on the JWT and JWKS entry. |
| `TokenLifetimeHours` | `8` | Token validity window in hours. |
| `IdentitiesFilePath` | `identities.json` | Path to the identities file, relative to the output directory. |
| `ClientsFilePath` | `clients.json` | Path to the clients file, relative to the output directory. |

#### Setup: Option B (Custom Host)

Use `AddIdentityServer` and `MapIdentityEndpoints` to embed the identity endpoints in an existing application or for finer control over registration:

```csharp
builder.Services.AddIdentityServer(builder.Configuration, sectionName: "CoreDesign:Identity");
builder.Services.AddJsonFileIdentityStore("identities.json");
builder.Services.AddJsonFileClientStore("clients.json");

var app = builder.Build();

app.UseIdentityServerCors(); // required for browser clients
app.MapIdentityEndpoints();

app.Run();
```

`AddIdentityServer` accepts an optional `Action<IdentityOptions>` to override individual values after configuration binding:

```csharp
builder.Services.AddIdentityServer(builder.Configuration, configure: opts =>
{
    opts.TokenLifetimeHours = 1;
});
```

#### Clients File (`clients.json`)

Add a `clients.json` file and set it to copy to the output directory:

```xml
<None Update="clients.json">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

The file is a JSON array of registered client records:

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
|---|---|---|
| `clientId` | string | Unique identifier for the client. Case-sensitive. Must be sent as `client_id` in every `/connect/token` request. |
| `clientSecret` | string or null | Optional shared secret. Null for public clients (SPAs, CLI tools, PKCE flows). |
| `tokenEndpointAuthMethod` | string | `"none"` for public clients, `"client_secret_post"` for confidential clients. |
| `allowedGrantTypes` | string[] | Grant types this client may use: `"authorization_code"` for browser flows, `"password"` for service-to-service. |
| `allowedRedirectUris` | string[] | Pre-registered redirect URIs. Required for authorization code clients. Exact string match. |
| `allowedPostLogoutRedirectUris` | string[] | Pre-registered post-logout redirect URIs. |
| `allowedScopes` | string[] | Scopes this client is permitted to request. |
| `requirePkce` | bool | When true, `/connect/authorize` rejects requests without a valid `code_challenge`. Always set to true for browser-based clients. |

To use a custom backing store implement `IClientStore` and register it directly:

```csharp
builder.Services.AddSingleton<IClientStore, MyClientStore>();
```

#### Identities File (`identities.json`)

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
    "permissions": [ "items:read", "items:write" ],
    "customClaims": {}
  }
]
```

| Field | Type | Description |
|---|---|---|
| `userId` | string (GUID) | Value used for the `sub` and `oid` claims |
| `username` | string | Login username. Comparison is case-insensitive. |
| `password` | string | Plaintext password. Comparison is case-sensitive. |
| `email` | string | `email` claim |
| `name` | string | `name` claim |
| `givenName` | string | `given_name` claim |
| `familyName` | string | `family_name` claim |
| `permissions` | string[] | Each value emitted as a separate `permissions` claim |
| `customClaims` | object | Arbitrary key-value pairs added as additional claims |

To use a custom backing store implement `IIdentityStore` and register it directly:

```csharp
builder.Services.AddSingleton<IIdentityStore, MyIdentityStore>();
```

#### Login Flows

##### Authorization Code with PKCE (Browser Login)

This is the standard flow for Blazor and other browser-based apps.

**Step 1: Browser redirects to the authorization endpoint**

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

The server returns an HTML login form. On successful submission it redirects back:

```
302 https://localhost:7070/signin-oidc?code={code}&state={state}
```

**Step 2: Client exchanges the code for tokens**

```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code
&client_id=my-blazor-app
&code={code}
&redirect_uri=https%3A%2F%2Flocalhost%3A7070%2Fsignin-oidc
&code_verifier={original-verifier}
```

Response (200):

```json
{
  "access_token": "<jwt>",
  "id_token": "<jwt>",
  "token_type": "Bearer",
  "expires_in": 28800,
  "scope": "openid profile email"
}
```

##### Direct JSON Login

`POST /auth/login` is a non-OIDC shortcut for frontends that manage their own token storage:

```http
POST /auth/login
Content-Type: application/json

{
  "username": "alice@example.local",
  "password": "Password1!"
}
```

##### Tooling Token Generation

`POST /get-token` accepts the same JSON credentials and returns the same response. Use it for Scalar, Postman, or curl.

#### Blazor App Integration

A Blazor Server app authenticates against this identity server using ASP.NET Core's built-in OIDC middleware.

**1. Register authentication**

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
        options.Authority = "https://localhost:5003";
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

**2. Map login and logout endpoints**

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

**3. Forward the access token to downstream APIs**

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

**4. Port stability under Aspire**

Pin the identity server to a fixed port so the Blazor app always finds it at the same address:

```csharp
// In AppHost
builder.AddProject<SampleApi_Identity_Web>("SampleIdentityWeb")
    .WithHttpsEndpoint(port: 5003, name: "https", isProxied: false);

// In Blazor app setup, read the authority from Aspire service discovery:
var authority =
    configuration["services:SampleIdentityWeb:https:0"]
    ?? configuration["IdentityApi:BaseUrl"]
    ?? throw new InvalidOperationException("OIDC authority not configured");
```

#### Template Customization

The login form and landing page are rendered from HTML template files shipped as embedded resources inside the library. Place override files in an `identity-templates` folder at the host project's content root. The library checks this folder first on every request. No configuration or restart is required.

```xml
<None Update="identity-templates\login.html">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

**`login.html` placeholders**

| Placeholder | Value |
|---|---|
| `{{response_type}}` | OIDC `response_type` parameter |
| `{{client_id}}` | OIDC `client_id` parameter |
| `{{redirect_uri}}` | OIDC `redirect_uri` parameter |
| `{{scope}}` | OIDC `scope` parameter |
| `{{state}}` | OIDC `state` parameter |
| `{{nonce}}` | OIDC `nonce` parameter |
| `{{code_challenge}}` | PKCE `code_challenge` |
| `{{code_challenge_method}}` | PKCE method, e.g. `S256` |
| `{{error_alert}}` | Fully rendered error banner HTML; empty string when there is no error |

**`login-error.html`** is a separate template for the error banner:

| Placeholder | Value |
|---|---|
| `{{error_message}}` | HTML-encoded error text |

**`landing.html`** is the page served at `/` by the standalone web host. It has no dynamic placeholders.

All three default templates define colors as CSS custom properties (`--id-*` variables). Light restyling can be done by injecting a `<style>` block that overrides those variables.

#### OIDC Discovery

The discovery document at `/.well-known/openid-configuration` returns:

- `response_types_supported`: `["code"]`
- `grant_types_supported`: `["authorization_code", "password"]`
- `code_challenge_methods_supported`: `["S256"]`

#### Persistent RSA Signing Key

The RSA signing key is persisted across restarts. On first startup the server generates an RSA key and writes it to `%APPDATA%\coredesign-identity\{keyId}.pem`. Subsequent startups load the same key from disk, preventing token validation failures after a server restart.

---

### 5.2 CoreDesign.Identity.Client

An ASP.NET Core library for API servers that need to validate tokens issued by `CoreDesign.Identity.Server`. It configures JWT Bearer authentication, provides a development-only middleware that auto-injects bearer tokens on local requests, and includes an OpenAPI document transformer that advertises the Bearer security scheme.

For Blazor or other browser-based apps, use the OIDC middleware directly (see [Blazor App Integration](#blazor-app-integration) above).

#### What It Provides

| Type | Description |
|---|---|
| `IdentityClientExtensions.AddIdentityClient` | Registers JWT Bearer auth, the `IdentityApiClient` HTTP client, and permission-based authorization |
| `IdentityClientExtensions.UseLocalBearerTokenInjection` | Mounts the bearer token injection middleware (development only) |
| `PermissionAuthorizationExtensions.AddPermissionAuthorization` | Registers permission-based authorization as a standalone call, for auth providers that do not go through `AddIdentityClient` |
| `PermissionAuthorizationPolicyProvider` | Dynamically creates an authorization policy for any permission string passed to `RequireAuthorization()` |
| `PermissionAuthorizationHandler` | Checks the `permissions` claim in the bearer token against the required permission |
| `IdentityApiClient` | Fetches and caches access tokens from the identity server |
| `BearerSecurityTransformer` | OpenAPI transformer that adds a Bearer security scheme to authenticated operations |

#### Setup

**1. Register services**

Call `AddIdentityClient` in development and `AddPermissionAuthorization` explicitly for non-development providers. Permission-based authorization is registered automatically when using `AddIdentityClient`:

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

No policy registration is needed. `PermissionAuthorizationPolicyProvider` creates the policy on demand the first time a given permission string is encountered.

**2. Add middleware**

```csharp
app.UseCors();
app.UseLocalBearerTokenInjection();
app.UseAuthentication();
app.UseAuthorization();
```

**3. Add configuration**

`IdentityApi` configures the HTTP client used to fetch tokens:

```json
{
  "IdentityApi": {
    "BaseUrl": "https://localhost:5003",
    "Username": "admin@example.local",
    "Password": "Password1!"
  }
}
```

`CoreDesign:Identity` (or `CoreDesign:IdentityWebHost`) provides the issuer and audience. The client reads from both section names, with `CoreDesign:IdentityWebHost` taking precedence:

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
|---|---|
| `IdentityApi:BaseUrl` | Base URL of the running identity server |
| `IdentityApi:Username` | Credential used by the token injection middleware to obtain a token |
| `IdentityApi:Password` | Credential used by the token injection middleware to obtain a token |
| `CoreDesign:IdentityWebHost:Issuer` (or `CoreDesign:Identity:Issuer`) | Expected `iss` claim on incoming tokens |
| `CoreDesign:IdentityWebHost:Audience` (or `CoreDesign:Identity:Audience`) | Expected `aud` claim on incoming tokens |

**4. Add the OpenAPI security transformer (optional)**

```csharp
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer<BearerSecurityTransformer>());
```

#### Token Injection Middleware

`BearerTokenInjectionMiddleware` runs only when:

- The environment is `Development`
- The request has no `Authorization` header
- The request originates from localhost (`127.0.0.1`, `::1`, or `::ffff:127.x`)
- The path is not a public endpoint (`/openapi`, `/swagger`, `/scalar`, `/health`, `/`)

When all conditions are met it calls `IdentityApiClient.GetAccessTokenAsync()`, which fetches a token from `/connect/token` and caches it until 60 seconds before expiry. If token acquisition fails the middleware logs a warning and continues without a token.

#### Token Validation Parameters

| Parameter | Value |
|---|---|
| Issuer validation | Enabled, matched against the configured issuer |
| Audience validation | Enabled, matched against the configured audience |
| Lifetime validation | Enabled |
| Signing key discovery | Automatic via `/.well-known/openid-configuration` |
| Name claim type | `email` |
| Inbound claim mapping | Disabled (`MapInboundClaims = false`) |

#### Frontend Login Flow (Overview)

In development the frontend authenticates directly against `CoreDesign.Identity.Server`. In production it authenticates against Azure Entra (or any other OIDC provider). The API code is identical in both cases.

**Development: direct credential exchange**

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
   |<-- 200 [ ... ] ------------------------------------'
```

**What changes when switching to Azure Entra**

| Concern | Development | Production (Entra) |
|---|---|---|
| Token acquisition | `POST /auth/login` on the identity server | MSAL `loginPopup` or `loginRedirect` |
| API calls | `Authorization: Bearer <token>` | `Authorization: Bearer <token>` |
| API validation config | `CoreDesign:Identity:Issuer` and `Audience` | `AzureAd:TenantId` and `Audience` |
| API code and endpoint permissions | unchanged | unchanged |
| Token claims (`permissions`, `email`, `oid`) | set in `identities.json` | Entra App Roles mapped to `permissions` claims via claims transformation |

---

## 6. Azure Entra (Production Authentication)

This section describes how to switch from `CoreDesign.Identity.Server` in development to Azure Entra (formerly Azure Active Directory) in production. The API code treats both as interchangeable JWT Bearer providers.

### Environment Summary

| Environment | Auth provider |
|---|---|
| `Development` | CoreDesign.Identity.Server (local) |
| `AzureDev`, `UAT`, `Production` | Azure Entra |

Authorization uses the same permission strings in every environment. Only the token issuer and the mechanism for assigning permissions to users differs between development and production.

### How the Switch Works

`AddIdentityAuthentication` selects the provider at startup:

```csharp
if (builder.Environment.IsDevelopment())
    builder.Services.AddIdentityClient(builder.Configuration);
else
    builder.AddAzureEntraAuthentication();
```

### Step 1: Create an App Registration

In the Azure portal, go to **Azure Active Directory > App registrations > New registration**.

| Field | Value |
|---|---|
| Name | Something descriptive, e.g. `CoreDesign API (UAT)` |
| Supported account types | Accounts in this organizational directory only |
| Redirect URI | Leave blank (this registration is for the API, not a client) |

Note the **Application (client) ID** and **Directory (tenant) ID** from the Overview page.

### Step 2: Expose an API

Under **Expose an API**, set the Application ID URI. Azure defaults this to `api://<client-id>`. This value becomes the `Audience` in the API's configuration.

Add a scope:

| Field | Value |
|---|---|
| Scope name | `access_as_user` |
| Who can consent | Admins and users |
| Admin consent display name | Access CoreDesign API |

### Step 3: Define App Roles

Under **App roles**, create a role for each permission string the API uses. The **Value** field must exactly match the permission string declared in your application (e.g., in `Permissions.cs`). Use the same roles across all environments.

| Display name | Value | Allowed member types |
|---|---|---|
| Weather Read | `weather:read` | Users/Groups |
| Weather Write | `weather:write` | Users/Groups |

Add one entry for each permission string your API defines. Values are case-sensitive and must match exactly.

### Step 4: Assign Users to Roles

In **Azure Active Directory > Enterprise applications**, find the app registration. Under **Users and groups**, assign each user or group to the appropriate roles. A user assigned to `weather:write` can call write endpoints; a user assigned only to `weather:read` cannot. Users with no role assignment receive no `roles` claim and are denied by the API's fallback authentication policy.

### Step 5: Configure the API

Add the Entra configuration section to `appsettings.json` (supply secrets via user secrets or a key vault):

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<your-client-id>",
    "Audience": "api://<your-client-id>"
  }
}
```

The API resolves the JWT authority as `{Instance}/{TenantId}/v2.0` and validates the `aud` claim against `Audience`.

Entra App Roles are emitted in tokens as `roles` claims. The `PermissionAuthorizationHandler` checks for `permissions` claims, so a claims transformation is required to bridge the two. Register it alongside `AddAzureEntraAuthentication`:

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

Register the transformation in the service container (typically inside `AddAzureEntraAuthentication`):

```csharp
services.AddScoped<IClaimsTransformation, RolesToPermissionsTransformation>();
```

### Step 6: Register a Client Application

Any application that calls the API needs its own App Registration. In the client's registration:

1. Under **Authentication**, add the appropriate platform and redirect URIs.
2. Under **API permissions**, add a permission to the API registration and select the `access_as_user` scope.
3. Grant admin consent if the scope requires it.

The client requests a token from `https://login.microsoftonline.com/{tenant-id}/oauth2/v2.0/token` with the scope set to `api://{api-client-id}/access_as_user`.

### Claim Mapping

The API configures JWT Bearer with `MapInboundClaims = false`. Entra tokens use standard claim names:

| Claim in token | Used as | Note |
|---|---|---|
| `roles` | App Role assignments | Mapped to `permissions` claims via `RolesToPermissionsTransformation` before authorization runs |
| `oid` | Object ID | Present by default |

If users need the `email` claim, ensure **Optional claims** includes `email` in the token configuration for the API's App Registration (under **Token configuration > Add optional claim > Access token > email**).

### Troubleshooting

**401 on all requests**: Verify `AzureAd:TenantId` and `AzureAd:Audience` are set correctly. The audience in the token (`aud` claim) must exactly match the configured value, including the `api://` prefix.

**403 on protected endpoints**: The user's token contains no matching `permissions` claim after transformation. Check that `RolesToPermissionsTransformation` is registered, that the user is assigned to the correct App Role in the Enterprise application, and that the App Role `Value` field exactly matches the permission string used in `RequireAuthorization()`.

**IDX20804 / metadata failure**: The API could not reach the Entra metadata endpoint at startup. Check outbound internet connectivity and confirm `AzureAd:Instance` and `AzureAd:TenantId` form a valid authority URL.

---

## 7. Shared appsettings Configuration

When multiple projects in a solution share configuration (JWT issuer URLs, shared secrets, environment-specific endpoints), keeping separate copies leads to drift. Linking the files ensures every project compiles and runs against the same values.

### Directory Structure

Place shared configuration files in a folder at the solution root. Projects then link to those files rather than owning their own copies.

```
<Solution Folder>/
  shared/
    appsettings.json
    appsettings.Development.json
    appsettings.Production.json
  src/
    MyService.A/
      MyService.A.csproj   (links to shared/)
    MyService.B/
      MyService.B.csproj   (links to shared/)
```

### Linking Files into Each Project

Add an `ItemGroup` to each `.csproj` file that references the shared files using relative paths. The `Link` attribute controls the filename as it appears in the project and in the output directory.

```xml
<ItemGroup>
  <Content Include="..\..\shared\appsettings.json"
           Link="appsettings.json"
           CopyToOutputDirectory="PreserveNewest" />
  <Content Include="..\..\shared\appsettings.Development.json"
           Link="appsettings.Development.json"
           CopyToOutputDirectory="PreserveNewest" />
  <Content Include="..\..\shared\appsettings.Production.json"
           Link="appsettings.Production.json"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

| Attribute | Purpose |
|---|---|
| `Include` | Path to the real file, relative to the `.csproj` |
| `Link` | Virtual name shown in Solution Explorer and used in the output folder |
| `CopyToOutputDirectory` | `PreserveNewest` copies only when the source is newer; `Always` copies on every build |

### How It Works at Runtime

MSBuild resolves the `Include` path at build time and copies the physical file from `shared/` into the project's output directory under the name given by `Link`. ASP.NET Core's configuration system then finds the file exactly as if each project owned its own copy.

### IDE Behavior

Visual Studio and Rider display linked files in Solution Explorer under the project node using the `Link` name. The file is shown with a shortcut arrow to indicate it is not physically located inside the project folder.

### Source Control

Commit only the files in `shared/`. Do not commit the copies that appear in `bin/` output directories.

If a file contains secrets, add `shared/appsettings.Production.json` to `.gitignore` and distribute it through a secrets manager or environment variables instead.

### Project-Specific Overrides

If one project needs a value that differs from the shared default, use a project-local appsettings file:

```csharp
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
```

Keep `appsettings.Local.json` in `.gitignore` so developer-specific overrides are never committed.

---

## 8. Local NuGet Development

A guide for testing package changes without publishing to nuget.org, and switching cleanly between local and published packages.

### Option 1: Local Folder Feed

Pack the project and point NuGet at a local directory:

```powershell
dotnet pack -c Release -o C:\local-nuget
dotnet nuget add source C:\local-nuget --name local
```

Or add it to `nuget.config`:

```xml
<configuration>
  <packageSources>
    <add key="local" value="C:\local-nuget" />
  </packageSources>
</configuration>
```

Then reference it normally in the `.csproj`:

```xml
<PackageReference Include="YourPackage" Version="1.0.0" />
```

### Option 2: Direct Project Reference (Fastest Iteration)

Skip packaging entirely during development:

```xml
<ItemGroup>
  <ProjectReference Include="..\YourLibrary\YourLibrary.csproj" />
</ItemGroup>
```

Switch back to `PackageReference` once the package is stable. This does not test the actual package behavior (assembly loading, transitive dependencies).

### Option 3: NuGet Package Explorer

Install [NuGet Package Explorer](https://github.com/NuGetPackageExplorer/NuGetPackageExplorer) to inspect `.nupkg` file contents before consuming the package anywhere.

### Option 4: Prerelease Versions

Publish a prerelease version to a private feed (GitHub Packages, Azure Artifacts, or MyGet):

```xml
<Version>1.2.3-beta.1</Version>
```

### Switching Back to nuget.org

Update the version in your `.csproj` to a version that exists on nuget.org. To remove the local source:

```powershell
dotnet nuget remove source local
```

### Gotcha: Cached Packages

NuGet caches packages in `%USERPROFILE%\.nuget\packages`. If your local test package and the real release share the same version number, NuGet may use the cached local copy. Force a clean resolution with:

```powershell
dotnet nuget locals all --clear
dotnet restore
```

Use a higher or prerelease version for local test packages (e.g. `1.0.1-local`) to avoid this ambiguity entirely.

---

## 9. Sample Application

Sample is a .NET 10 microservices solution that demonstrates how to build a REST API using .NET Aspire, Entity Framework Core, and the CoreDesign family of packages. It serves as a working reference for setting up CoreDesign.Identity.Server, CoreDesign.Identity.Client, and CoreDesign.Data in a real application.

### 9.1 Project Overview

| Project | Purpose |
|---|---|
| `Sample.Aspire.AppHost` | Aspire application host that orchestrates all services for local development |
| `Sample.Aspire.ServiceDefaults` | Shared class library providing common OpenTelemetry, HTTP resilience, and service discovery configuration |
| `Sample.Api` | Main ASP.NET Core REST API using Vertical Slice Architecture, JWT authentication, and CoreDesign.Data |
| `Sample.Identity.Web` | Standalone OIDC identity server for Blazor browser login flows |
| `Sample.Identity.Api` | Second identity server instance with Scalar UI for direct token issuance and testing |
| `Sample.Blazor` | Blazor Server application authenticating via OIDC and calling Sample.Api |
| `Sample.Data.MigrationService` | Hosted service that runs EF Core migrations and seeds data before the API receives traffic |
| `Sample.Api.Tests` | xUnit unit tests for the API service layer using Moq and Bogus |

**Pre-configured accounts**

| Email | Password | Permissions |
|---|---|---|
| admin@sampleapi.local | Password1! | weather:read, weather:write |
| user@sampleapi.local | Password1! | weather:read |

### 9.2 Prerequisites and Setup

**Prerequisites**

- .NET 10 SDK
- Docker Desktop (required for the SQL Server container)
- .NET Aspire workload

Install the Aspire workload:

```
dotnet workload install aspire
```

**1. Add user secrets**

The SQL Server container password is supplied via .NET user secrets so it is never stored in source control. Run from the repository root:

```
dotnet user-secrets init --project src/Sample.Aspire.AppHost
dotnet user-secrets set "Parameters:SqlPassword" "my-secret-password" --project src/Sample.Aspire.AppHost
```

**2. Trust the developer SSL certificate**

```
dotnet dev-certs https --trust
```

Restart your browser after running this command.

**Running the application**

```
dotnet run --project src/Sample.Aspire.AppHost
```

Aspire will print a dashboard URL to the console (typically `https://localhost:17002`). Open the dashboard to see all running services, logs, and traces. Sample.Identity.Api exposes a Scalar UI at `/scalar/v1` for obtaining tokens and testing endpoints directly.

**Running tests**

```
dotnet test
```

### 9.3 Sample.Identity.Web

A standalone development identity provider built on `CoreDesign.Identity.Server`. It hosts the full OIDC endpoint suite including the browser login form, so that the Blazor app can authenticate using Authorization Code with PKCE.

`Program.cs` is three lines:

```csharp
builder.Services.AddIdentityServerWebHost(builder.Configuration);
// ...
app.MapIdentityServerWebHost();
```

Configuration comes from the shared `appsettings.Development.json`:

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

The project is pinned to port 5003 by the AppHost so the `Issuer` URL stays stable across restarts.

**Pre-configured clients**

| Client ID | Grant | Purpose |
|---|---|---|
| `sample-blazor` | `authorization_code` (PKCE required) | Blazor UI browser login |
| `sample-api-dev` | `password` | Service-to-service token injection |

The `identities.json` and `clients.json` files live in `src/Shared/` and are linked into the project via MSBuild `<Content>` items.

To run in isolation:

```
dotnet run --project src/Sample.Identity.Web
```

Browse to `https://localhost:5003` for the landing page, or `https://localhost:5003/.well-known/openid-configuration` for the discovery document.

### 9.4 Sample.Api

The main ASP.NET Core REST API. It implements Vertical Slice Architecture (VSA): each HTTP operation is fully self-contained in its own folder. See [Section 10](#10-vertical-slice-architecture-reference) for the full architecture analysis.

#### Project Structure

```
Sample.Api/
├── Data/
│   ├── Migrations/
│   └── SampleDbContext.cs
├── Infrastructure/
│   ├── App.cs
│   ├── Configuration.cs
│   ├── Permissions.cs
│   ├── Endpoints.cs
│   ├── Cache.cs
│   ├── Identity.cs
│   ├── Scalar.cs
│   └── Serilog.cs
├── WeatherForecasts/
│   ├── Shared/
│   │   ├── WeatherForecast.cs
│   │   └── WeatherForecastConfiguration.cs
│   ├── Create/
│   │   ├── Endpoint.cs
│   │   ├── Handler.cs
│   │   ├── Request.cs
│   │   └── Response.cs
│   ├── Delete/
│   │   ├── Endpoint.cs
│   │   └── Handler.cs
│   ├── GetAll/
│   │   ├── Endpoint.cs
│   │   ├── Handler.cs
│   │   └── Response.cs
│   ├── GetById/
│   │   ├── Endpoint.cs
│   │   ├── Handler.cs
│   │   └── Response.cs
│   └── Update/
│       ├── Endpoint.cs
│       ├── Handler.cs
│       └── Request.cs
├── GlobalUsing.cs
├── ModuleConfig.cs
└── Program.cs
```

#### Infrastructure Files

| File | Purpose |
|---|---|
| `App.cs` | Configures the middleware pipeline: HTTPS, CORS, authentication, authorization, output caching, and endpoint mapping. |
| `Configuration.cs` | Registers services: database, identity, CORS, output caching, and telemetry. Also registers `BearerSecurityTransformer`. Permission-based authorization is registered automatically via `AddIdentityClient` in development and `AddPermissionAuthorization` in production. |
| `Permissions.cs` | Application permission constants (`weather:read`, `weather:write`) passed directly to `RequireAuthorization()`. |
| `Endpoints.cs` | Top-level endpoint registration. Delegates to each feature module's endpoint mapper. |
| `Cache.cs` | Output cache policy configuration and the `CacheConfig` enum used for tag-based cache invalidation. |
| `Identity.cs` | Extension method on `HttpContext` that extracts the authenticated user's ID from the `oid` claim. |
| `Scalar.cs` | Registers the OpenAPI and Scalar UI routes in development only. Both routes are marked `AllowAnonymous()`. |
| `Serilog.cs` | Configures Serilog enrichment and the Application Insights sink. |

#### WeatherForecasts Feature

| Endpoint | Required Permission | Notes |
|---|---|---|
| `POST /WeatherForecasts` | `weather:write` | Inserts via `ICudRepository`, evicts cache, returns `201 Created` |
| `GET /WeatherForecasts` | `weather:read` | Output cached |
| `GET /WeatherForecasts/{id}` | `weather:read` | Output cached |
| `PUT /WeatherForecasts/{id}` | `weather:write` | Fetches, applies request, saves via `ICudRepository` |
| `DELETE /WeatherForecasts/{id}` | `weather:write` | Soft-deletes via `ICudRepository`, evicts cache |

`Shared/` holds only the `WeatherForecast` entity and its EF Core configuration. Everything else (request/response types, handler logic) is scoped to its individual operation folder.

#### Logging

Service classes in Sample.Api contain no log statements. All invocation logging is handled by `CoreDesign.Logging`. Services are registered with `AddWithLogging` in place of `AddTransient`:

```csharp
services.AddWithLogging<IWeatherForecastService, WeatherForecastService>();
```

#### Shared App Settings

`appsettings.json` and `appsettings.Development.json` live in `src/Shared/` and are linked into the following projects:

| Project | Links shared settings |
|---|---|
| Sample.Api | Yes |
| Sample.Identity.Api | Yes |
| Sample.Identity.Web | Yes |
| Sample.Blazor | Yes |
| Sample.Data.MigrationService | Yes |
| Sample.Aspire.AppHost | Yes |
| Sample.Api.Tests | No (unit tests use mocked repositories and do not load app settings) |

To change a setting that applies to all services, edit the file in `src/Shared/`. The change is reflected in every linked project on the next build.

### 9.5 Sample.Blazor

An ASP.NET Core Blazor Server application that authenticates via OpenID Connect (Authorization Code with PKCE) and calls Sample.Api to display weather forecast data.

#### How It Works

On the first visit to any protected page, the `RedirectToLogin` component redirects the browser to the identity server's login form. After authentication, the identity server redirects back to `/signin-oidc` with an authorization code. ASP.NET Core's OIDC middleware exchanges the code for tokens, stores them in an encrypted auth cookie (`SaveTokens = true`), and redirects the user to the original page. Subsequent API calls attach the access token from the cookie as a `Bearer` header via `BearerTokenHandler`.

#### Project Structure

```
Sample.Blazor/
├── Components/
│   ├── Pages/
│   │   ├── Home.razor             -- Claims display, active auth provider name
│   │   └── WeatherForecasts.razor -- Fetches data from Sample.Api
│   ├── Layout/
│   │   ├── MainLayout.razor       -- Top bar with Sign in / Sign out links
│   │   └── NavMenu.razor
│   ├── App.razor
│   ├── RedirectToLogin.razor      -- Redirects unauthenticated users to /account/login
│   └── Routes.razor               -- Wires RedirectToLogin as NotAuthorized fallback
├── Infrastructure/
│   ├── Auth/
│   │   ├── IAuthProviderConfigurator.cs
│   │   ├── LocalOidcAuthConfigurator.cs
│   │   └── AzureEntraAuthConfigurator.cs
│   ├── App.cs
│   └── Configuration.cs
└── Services/
    ├── BearerTokenHandler.cs
    ├── SampleClient.cs
    └── WeatherForecastResponse.cs
```

#### Auth Provider Selection

`IAuthProviderConfigurator` abstracts over the authentication provider. The active provider is chosen at startup based on the `Blazor:AuthProvider` key:

| Implementation | Config value | Provider |
|---|---|---|
| `LocalOidcAuthConfigurator` | `"Local"` (default) | Sample.Identity.Web via OIDC |
| `AzureEntraAuthConfigurator` | `"AzureEntra"` | Azure Entra ID via Microsoft.Identity.Web |

Both implementations configure the same cookie paths so all components share one login and logout route regardless of which provider is active.

#### LocalOidcAuthConfigurator: Authority Resolution

When running under Aspire, `LocalOidcAuthConfigurator` resolves the identity server URL in order of preference:

1. `Blazor:Oidc:Authority` (explicit override in appsettings)
2. `services:SampleIdentityWeb:https:0` (Aspire service discovery, IConfiguration normalized form)
3. `services__SampleIdentityWeb__https__0` (Aspire service discovery, raw environment variable form)
4. Connection string `"SampleIdentityWeb"`
5. `IdentityApi:BaseUrl` (developer fallback for running outside Aspire)

#### AzureEntraAuthConfigurator

Required keys in appsettings (supply secrets via user secrets or a key vault):

```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "TenantId": "<your-tenant-id>",
  "ClientId": "<your-client-id>",
  "CallbackPath": "/signin-oidc"
}
```

`AzureAd:ClientSecret` must be stored in user secrets, never in appsettings files.

#### Login and Logout Endpoints

| Route | Behavior |
|---|---|
| `GET /account/login` | Issues an OIDC challenge that redirects the browser to the identity server login form. Accepts `returnUrl` as a query parameter. |
| `GET /account/logout` | Signs out of the local cookie. If `SupportsFederatedLogout` is true, also signs out of the OIDC session at the identity server. |

#### BearerTokenHandler

`BearerTokenHandler` reads the `access_token` stored in the auth cookie and attaches it as `Authorization: Bearer <token>` on every outbound request to Sample.Api:

```csharp
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient<SampleClient>(client =>
        client.BaseAddress = new Uri("https://SampleApi"))
    .AddHttpMessageHandler<BearerTokenHandler>();
```

The base address `https://SampleApi` uses Aspire service discovery so the Blazor app finds Sample.Api automatically without hard-coding a port.

#### Configuration Reference

| Key | Purpose |
|---|---|
| `Blazor:AuthProvider` | `"Local"` or `"AzureEntra"` |
| `Blazor:Oidc:Authority` | Override the OIDC authority URL (optional when running under Aspire) |
| `Blazor:Oidc:ClientId` | OIDC client ID (defaults to `"sample-blazor"`) |
| `Blazor:Oidc:Scopes` | Requested scopes array |
| `AzureAd:TenantId` | Azure Entra tenant ID (required when `AuthProvider` is `"AzureEntra"`) |
| `AzureAd:ClientId` | Azure Entra app registration client ID |
| `AzureAd:ClientSecret` | Azure Entra client secret (supply via user secrets) |

### 9.6 Sample.Data.MigrationService

A .NET hosted service that ensures the database exists, applies pending EF Core migrations, and seeds reference data before any API receives traffic.

`Program.cs` registers `MigrationWorker<SampleDbContext>` and its OpenTelemetry `ActivitySource`:

```csharp
builder.AddMigrationWorker<SampleDbContext>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(MigrationWorker<SampleDbContext>.ActivitySourceName));
```

On startup the worker runs three steps in order:

1. **Ensure database**: creates the database if it does not exist.
2. **Migrate**: applies all pending EF Core migrations via `MigrateAsync`.
3. **Seed**: scans the `SeedData/` directory, matches each `*.json` file to a `BaseEntity` subclass by filename, and inserts any records that do not already exist (identified by `Id`).

When all steps complete, `IHostApplicationLifetime.StopApplication()` is called and the process exits with code 0.

#### Seed File Naming Convention

The filename (without `.json`) must be the **fully qualified type name** of the entity class. For example, the `WeatherForecast` class in namespace `Sample.Api.WeatherForecasts.Models` is seeded from:

```
SeedData/Sample.Api.WeatherForecasts.Models.WeatherForecast.json
```

#### Adding a New Seed File

1. Create a JSON file in `SeedData/` named after the fully qualified type name.
2. Populate it with a JSON array of entity objects including all `BaseEntity` audit fields (`Id`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `IsDeleted`).
3. Add a `<None Update="SeedData\...">` entry to the `.csproj` with `CopyToOutputDirectory: PreserveNewest`.

No code changes are required. The worker discovers and seeds the new file automatically on the next run.

#### Configuration

```json
"DatabaseOptions": {
  "HostName": "sample-mssql",
  "DatabaseName": "sample-db",
  "HostPort": 52881,
  "ConnectionStringName": "sampledb"
}
```

In local development under Aspire this is injected by the AppHost. In a pipeline or non-Aspire environment:

```
ConnectionStrings__sample-db=<connection-string>
```

### 9.7 Deployment

#### Option A: Azure Developer CLI (azd)

```
winget install microsoft.azd
azd auth login
azd up
```

`azd` creates the Azure SQL Database, injects the connection string into the migration service, runs migrations and seeding, then deploys Sample.Api and Sample.Blazor. No secrets or connection strings need to be handled manually.

#### Option B: GitHub Actions

**Required GitHub Secrets**

| Secret | Value |
|---|---|
| `AZURE_CLIENT_ID` | App registration client ID for OIDC federated authentication |
| `AZURE_TENANT_ID` | Azure Entra tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Target subscription ID |
| `AZURE_SQL_CONNECTION_STRING` | Full ADO.NET connection string for the Azure SQL Database |

**Workflow**

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

The migration step runs before the deploy steps so the database schema is always consistent with the application version being deployed. The process exits 0 on success and non-zero on failure, causing the workflow step to fail and stopping the deployment before any containers are updated.

**Connection string formats**

With managed identity (recommended):

```
Server=<server>.database.windows.net;Database=sample-db;Authentication=Active Directory Default;Encrypt=True;
```

With username and password:

```
Server=<server>.database.windows.net;Database=sample-db;User Id=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;
```

---

## 10. Vertical Slice Architecture Reference

This section documents the architecture analysis of Sample.Api and the resulting design decisions.

### Assessment

The architecture is best described as **Feature-Sliced Layered Architecture** before the refactoring. It organizes features into folders correctly and separates infrastructure cleanly, but inside the feature it arranges code by technical role and shares a service class across all operations. After the refactoring to the current state, Sample.Api is a correct implementation of VSA.

### What the API Gets Right

| Element | Status |
|---|---|
| Feature folder (`WeatherForecasts/`) at the project root | Correct |
| One handler per HTTP operation | Correct |
| One endpoint class per HTTP operation | Correct |
| Endpoint does binding and delegation only — no logic | Correct |
| Module registration pattern in `ModuleConfig.cs` | Correct |
| `Infrastructure/` contains only plumbing with no business rules | Correct |
| `Data/` holds `DbContext`, schema constants, and migrations | Correct |
| EF Core config (`WeatherForecastConfiguration.cs`) co-located with the entity | Correct |
| Authorization declared on endpoint definitions, not inside handlers | Correct |
| Cache policy declared on endpoints, eviction called in handlers after writes | Correct |
| Logging applied via `AddWithLogging` decorator, not inline | Correct |
| `OneOf` discriminated unions with `.Match()` for error handling | Correct |
| HTTP-specific types (`HttpContext`, `IResult`) live in handlers, not services | Correct |

### Target Structure

```
Sample.Api/
├── Program.cs
├── GlobalUsing.cs
├── ModuleConfig.cs
├── Infrastructure/
│   ├── App.cs
│   ├── Configuration.cs
│   ├── Permissions.cs
│   ├── Endpoints.cs
│   ├── Cache.cs
│   ├── Serilog.cs
│   ├── Identity.cs
│   └── Scalar.cs
├── Data/
│   ├── SampleDbContext.cs
│   ├── Schemas.cs
│   └── Migrations/
└── WeatherForecasts/
    ├── Shared/
    │   ├── WeatherForecast.cs
    │   └── WeatherForecastConfiguration.cs
    ├── Create/
    │   ├── Endpoint.cs
    │   ├── Handler.cs
    │   ├── Request.cs
    │   └── Response.cs
    ├── Delete/
    │   ├── Endpoint.cs
    │   └── Handler.cs
    ├── GetAll/
    │   ├── Endpoint.cs
    │   ├── Handler.cs
    │   └── Response.cs
    ├── GetById/
    │   ├── Endpoint.cs
    │   ├── Handler.cs
    │   └── Response.cs
    └── Update/
        ├── Endpoint.cs
        ├── Handler.cs
        └── Request.cs
```

`Shared/` holds only what is genuinely shared: the entity class and its EF Core configuration. Everything else is operation-scoped. A developer tracing any single HTTP operation opens one folder and finds everything: the route definition, the request binding, the data access, and the response shape.

### VSA Principles Applied

**Per-operation request and response types.** Each operation owns its `Request.cs` and `Response.cs`. If Create needs a field Update does not, or if GetAll needs a summary shape while GetById returns full detail, the types evolve independently without coupling.

**Handlers inject repositories directly.** There is no shared service class across operations. Each handler injects `IReadRepository` and/or `ICudRepository` directly and contains its own logic.

**Adding a new operation** means adding a new sub-folder under the feature and registering the endpoint in `ModuleConfig.cs`. No other files change.

---

## 11. Release Notes

### CoreDesign.Identity.Client (Permission-Based Authorization)

**Permission-based authorization replaces role-based policies.**

`AddIdentityClient` now registers `PermissionAuthorizationPolicyProvider` and `PermissionAuthorizationHandler` automatically. Endpoints declare their required permission by passing a string directly to `RequireAuthorization()`:

```csharp
app.MapGet("/items", Handler.HandleAsync).RequireAuthorization("items:read");
app.MapPost("/items", Handler.HandleAsync).RequireAuthorization("items:write");
```

No policy registration is needed. The policy provider creates the policy on demand the first time a given permission string is encountered, and checks for a matching `permissions` claim in the bearer token.

`AddPermissionAuthorization` is now available as a standalone extension method on `IServiceCollection`, intended for production auth providers (such as Azure Entra) that do not go through `AddIdentityClient`.

**Breaking changes:**

- Named role-based authorization policies (`AdminOnly`, `UserOrAdmin`, etc.) are no longer created. Replace any `RequireAuthorization("PolicyName")` calls with `RequireAuthorization("permission:string")`.
- The `roles` field in `identities.json` is replaced by `permissions`. Update all identity records to use the `permissions` array.

---

### CoreDesign.Identity.Server (Permission Claims)

Issued tokens now include `permissions` claims instead of `roles` claims. Each entry in the identity record's `permissions` array becomes its own `permissions` claim in the JWT and is returned by the `/connect/userinfo` endpoint. The `identities.json` format changes accordingly: replace the `roles` array with a `permissions` array in every identity record.

---

### CoreDesign.Data 1.0.3

**AddMigrationWorker Extension Method**

A new `AddMigrationWorker<TContext>` extension method on `IHostApplicationBuilder` replaces the previous manual hosted-service registration:

```csharp
builder.AddMigrationWorker<SampleDbContext>();
```

**Configurable Seed Directory**

Pass a directory path as the second argument to override the default `SeedData` folder:

```csharp
builder.AddMigrationWorker<SampleDbContext>("ReferenceData");
```

**MigrationWorker No Longer Abstract**

`MigrationWorker<TContext>` is no longer abstract. `SeedAsync` is now a `virtual` method with a built-in default implementation that delegates to `SeedFromDirectoryAsync`. Consuming projects that rely entirely on convention-based JSON seeding no longer need to create a subclass.

---

### CoreDesign.Identity.Server 1.0.7

**Authorization Code with PKCE**

The identity server now implements the full Authorization Code with PKCE flow, making it compatible with Blazor Server and other browser-based applications. Two new endpoints handle the browser login flow: `GET /connect/authorize` (renders the login form) and `POST /connect/authorize` (processes form submission and issues an authorization code).

**Client Store**

A new `IClientStore` interface and built-in JSON file implementation enforce per-client rules at both the authorization and token endpoints. Register the built-in store:

```csharp
builder.Services.AddJsonFileClientStore("clients.json");
```

**Standalone Web Host Pattern**

`AddIdentityServerWebHost` and `MapIdentityServerWebHost` set up the full identity server stack from a single configuration section, registering both JSON file stores, enabling CORS, serving a landing page at `/`, and mounting all OIDC endpoints.

**Template Customization**

The login form, error banner, and landing page are now fully customizable without modifying the library by placing override files in an `identity-templates` folder at the host project's content root.

**Separate Access Tokens and ID Tokens**

The token endpoint now issues two distinct JWTs per successful authentication. The access token audience is the API resource; the ID token audience is the `client_id`.

**Persistent RSA Signing Key**

The RSA signing key is now persisted across restarts to `%APPDATA%\coredesign-identity\{keyId}.pem`, preventing token validation failures after a server restart in development scenarios.

**OIDC Discovery Updates**

- `response_types_supported` now returns `["code"]` only.
- `grant_types_supported` now returns `["authorization_code", "password"]`.
- `code_challenge_methods_supported` is a new field returning `["S256"]`.

**Bug Fixes**

- Fixed a 403 error that occurred during the browser-based authorization code exchange.
- Fixed the error alert box not rendering correctly after a failed login attempt.

**Breaking Changes**

- `response_types_supported` in the OIDC discovery document no longer includes `"token"` or `"id_token"`.
- The authorization endpoint now validates clients against the client store. A `client_id` not registered in `clients.json` is rejected.

---

### CoreDesign.Identity.Client 1.0.7

**Dual Configuration Section Support**

The client now reads issuer and audience from both `CoreDesign:Identity` and `CoreDesign:IdentityWebHost` configuration sections. `CoreDesign:IdentityWebHost` takes precedence when both are present.

---

### CoreDesign.Logging 1.0.3

New package. Provides a `DispatchProxy`-based middleware that wraps any service interface and automatically produces structured log output for every method call, return value, and exception.

---

### CoreDesign.Data 1.0.2

**Convention-Based Seed Data Loading**

A new `SeedFromDirectoryAsync` method on `MigrationWorker<TContext>` eliminates the need for subclasses to enumerate entity types explicitly. Pass a directory path and the assembly that owns entity types; the base class scans every `*.json` file in the directory, resolves the entity type by filename, and calls `SeedEntitiesAsync<T>` for each one.

---

### Sample Application (Current)

**Sample.Identity.Web** — New standalone identity server for Blazor browser login flows.

**Sample.Blazor** — New Blazor Server application authenticating via OIDC and displaying weather forecast data. Supports pluggable auth providers (`"Local"` or `"AzureEntra"`) via `IAuthProviderConfigurator`.

**Sample.Api** — Reorganized to full Vertical Slice Architecture. Each HTTP operation is self-contained in its own folder with operation-scoped request/response types and direct repository injection.

**Sample.Data.MigrationService** — The `SampleMigrationWorker` subclass has been removed. The service now registers `MigrationWorker<SampleDbContext>` directly with `AddMigrationWorker`. Adding a new seed file requires no code changes.

**Sample.Aspire.AppHost** — `AppHostExtensions.cs` extracts Aspire wiring into named helper methods. All services run on fixed HTTPS ports to ensure OIDC issuer URLs and redirect URIs remain stable.

**Shared Configuration Files** — `clients.json` and `identities.json` now live in a single `Sample/src/Shared/` folder and are linked into all relevant projects.

---

## 12. Feedback

Feedback on these packages is welcome and genuinely respected. If something is missing, confusing, or lower priority than it should be, opening an issue is the best way to make it better for everyone.

Especially useful to hear about:

- Features you expected to find but did not
- Behaviors that required a workaround or a subclass when they should have been built in
- Prioritization input: which gaps would unblock you most

Open an issue at [github.com/codyskidmore/CoreDesign/issues](https://github.com/codyskidmore/CoreDesign/issues) or tag [@codyskidmore](https://github.com/codyskidmore) in an existing issue or discussion. A plain description of what you ran into or what you wish existed is all that is needed.
