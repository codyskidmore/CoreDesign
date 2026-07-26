# CoreDesign

A collection of reusable .NET 10 libraries for data access, shared infrastructure, logging, and identity. Each package is independent and can be referenced on its own.

## NuGet Packages

| Package | Link |
|---|---|
| `CoreDesign.Shared` | [nuget.org/packages/CoreDesign.Shared](https://www.nuget.org/packages/CoreDesign.Shared/) |
| `CoreDesign.Data` | [nuget.org/packages/CoreDesign.Data](https://www.nuget.org/packages/CoreDesign.Data/) |
| `CoreDesign.Logging` | [nuget.org/packages/CoreDesign.Logging](https://www.nuget.org/packages/CoreDesign.Logging/) |
| `CoreDesign.ExceptionHandling` | [nuget.org/packages/CoreDesign.ExceptionHandling](https://www.nuget.org/packages/CoreDesign.ExceptionHandling/) |
| `CoreDesign.Identity.Server` | [nuget.org/packages/CoreDesign.Identity.Server](https://www.nuget.org/packages/CoreDesign.Identity.Server/) |
| `CoreDesign.Identity.Client` | [nuget.org/packages/CoreDesign.Identity.Client](https://www.nuget.org/packages/CoreDesign.Identity.Client/) |
| `CoreDesign.SeedTool` | [nuget.org/packages/CoreDesign.SeedTool](https://www.nuget.org/packages/CoreDesign.SeedTool/) |

## User Guide

For a single consolidated reference covering all packages, the sample application, authentication setup, deployment, and release notes, see [CoreDesignUserGuide.md](CoreDesignUserGuide.md).

## Projects

### CoreDesign.Shared

`src/CoreDesign.Shared/`

Shared infrastructure, error result types, and utility extension methods intended for use across all layers of a solution, including both host/API projects and Aspire AppHost projects.

Includes `DatabaseOptions` configuration binding, `AddAppSettings` and `AddDatabaseConfiguration` extension methods, lightweight error result records (`NotFoundMessage`, `BadRequestMessage`, `ErrorMessage`, `InvalidOperationMessage`), and object/string extension methods for JSON serialization, deep cloning, and type conversion.

Full details: [src/CoreDesign.Shared/README.md](src/CoreDesign.Shared/README.md)

### CoreDesign.Data

`src/CoreDesign.Data/`

A generic, reusable Entity Framework Core data access layer. Provides a `BaseEntity` base class with ULID primary keys and audit fields, a corresponding `BaseEntityConfiguration<T>` for EF Core model configuration, and `IReadRepository` / `ICudRepository` interfaces with concrete implementations that include soft-delete support.

Full details: [src/CoreDesign.Data/README.md](src/CoreDesign.Data/README.md)

### CoreDesign.Logging

`src/CoreDesign.Logging/`

Compile-time logging decorators generated from a single attribute. Place `[LoggingDecorator]` on any interface and the included Roslyn source generator produces a decorator class at compile time that logs every method invocation, return value, and exception. No reflection, no runtime overhead, fully AOT-compatible.

```csharp
[LoggingDecorator]
public interface ICreateForecastHandler
{
    Task<OneOf<WeatherForecast, BadRequestMessage>> CreateAsync(Request request, CancellationToken ct);
}
```

After marking interfaces, register all decorators in one call during startup:

```csharp
services.DecorateWithLogging();
```

Successful calls log at Information, error result arms log at Warning, and exceptions log at Error. Both synchronous and async methods are fully supported.

Full details: [src/CoreDesign.Logging/README.md](src/CoreDesign.Logging/README.md)

### CoreDesign.ExceptionHandling

`src/CoreDesign.ExceptionHandling/`

Global unhandled exception handling for ASP.NET Core. A Roslyn source generator produces a compile-time dispatch table from exception types marked with `[ProblemMapping]`, mapping them to RFC 7807 `ProblemDetails` responses. Works with zero configuration (a generic 500 for every exception) and layers in per-exception status codes as mappings are added.

```csharp
[ProblemMapping(404, Title = "Resource not found")]
public sealed class EntityNotFoundException(string entity, Guid id)
    : Exception($"{entity} '{id}' was not found.");
```

```csharp
builder.Services.AddCoreDesignExceptionHandling();
builder.Services.AddGeneratedProblemMappings();
// ...
app.UseExceptionHandler();
```

Full details: [src/CoreDesign.ExceptionHandling/README.md](src/CoreDesign.ExceptionHandling/README.md)

### CoreDesign.Identity

`src/CoreDesign.Identity/`

A pair of packages that provide an OIDC-compatible identity layer for development and testing. Teams can authenticate requests from day one without standing up an external provider. When the project is ready for production, swapping to a real provider requires only configuration changes.

Overview and quick-start guide: [src/CoreDesign.Identity/README.md](src/CoreDesign.Identity/README.md)

Azure Entra integration notes: [src/CoreDesign.Identity/README.AzureEntra.md](src/CoreDesign.Identity/README.AzureEntra.md)

#### CoreDesign.Identity.Server

`src/CoreDesign.Identity/CoreDesign.Identity.Server/`

A self-contained OIDC server that runs inside a solution. Exposes discovery, JWKS, token issuance, and userinfo endpoints as minimal API routes. Intended for development and integration testing only (ephemeral signing key, plaintext passwords, open CORS).

Full details: [src/CoreDesign.Identity/CoreDesign.Identity.Server/README.md](src/CoreDesign.Identity/CoreDesign.Identity.Server/README.md)

#### CoreDesign.Identity.Client

`src/CoreDesign.Identity/CoreDesign.Identity.Client/`

An ASP.NET Core client library for APIs that validate tokens issued by `CoreDesign.Identity.Server` or any standard OIDC provider. Configures JWT Bearer authentication via OIDC discovery, provides a development-only middleware that auto-injects bearer tokens on local requests, and includes an OpenAPI document transformer for the Bearer security scheme.

Full details: [src/CoreDesign.Identity/CoreDesign.Identity.Client/README.md](src/CoreDesign.Identity/CoreDesign.Identity.Client/README.md)

### CoreDesign.SeedTool

`src/CoreDesign.SeedTool/`

A `dotnet tool` for exporting live entity data to `MigrationWorker`-compatible seed files and comparing seed files against a live database. Works with any project using `CoreDesign.Data`. Install globally with `dotnet tool install -g CoreDesign.SeedTool`, then run `dotnet seed setup` from the repository root to generate the config file and `dotnet seed export` to capture the current state of your entities.

Full details: [src/CoreDesign.SeedTool/README.md](src/CoreDesign.SeedTool/README.md)

## Shared Configuration

When multiple projects in the solution share configuration values (JWT issuer, audience, etc.), use a single `shared/` folder at the solution root and link the files into each project via the `.csproj`. This avoids configuration drift across projects.

Full guidance: [SharedAppsettings.md](SharedAppsettings.md)

## Feedback

Feedback on these packages is welcome and genuinely respected. If something is missing, confusing, or lower priority than it should be, opening an issue is the best way to make it better for everyone.

Especially useful to hear about:

- Features you expected to find but did not
- Behaviors that required a workaround or a subclass when they should have been built in
- Prioritization input: which gaps would unblock you most

Open an issue at [github.com/codyskidmore/CoreDesign/issues](https://github.com/codyskidmore/CoreDesign/issues) or tag [@codyskidmore](https://github.com/codyskidmore) in an existing issue or discussion. There are no templates to fill out; a plain description of what you ran into or what you wish existed is enough.
