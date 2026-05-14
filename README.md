# CoreDesign

A collection of reusable .NET 10 libraries for data access, shared infrastructure, logging, and identity. Each package is independent and can be referenced on its own.

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

A `DispatchProxy`-based logging middleware that wraps any service interface and automatically logs every method invocation, return value, and exception. Service classes stay free of log statements while still producing structured, consistent log output for every operation.

Register a service using `AddWithLogging` in place of the standard `AddTransient` or `AddScoped` call:

```csharp
services.AddWithLogging<IWeatherForecastService, WeatherForecastService>();
```

The DI container resolves the interface as a proxy-wrapped instance. Successful calls log at Information, `NotFoundMessage` and `BadRequestMessage` results log at Warning, and exceptions log at Error. Both synchronous and async methods are fully supported.

Full details: [src/CoreDesign.Logging/README.md](src/CoreDesign.Logging/README.md)

Design rationale and comparison with Serilog and Serilog.Enrichers.Sensitive: [src/CoreDesign.Logging/SerilogVsMiddleware.md](src/CoreDesign.Logging/SerilogVsMiddleware.md)

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
