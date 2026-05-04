# SampleApi

SampleApi is a .NET 10 microservices solution that demonstrates how to build a REST API using [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/), Entity Framework Core, and the **CoreDesign** family of packages. It serves as a working reference for setting up CoreDesign.Identity.Server, CoreDesign.Identity.Client, and CoreDesign.Data in a real application.

---

## Projects

### SampleApi.Aspire.AppHost

The Aspire application host that orchestrates all services for local development. It provisions a SQL Server Docker container with a persistent volume, starts the Identity API and main API, and runs the migration service to prepare the database. Run this project to start the full local environment.

### SampleApi.Aspire.ServiceDefaults

A shared class library referenced by all service projects. It provides common OpenTelemetry observability configuration, HTTP resilience policies, and service discovery so that each service gets consistent infrastructure behavior without duplicating setup code.

### SampleApi.Api

The main ASP.NET Core REST API. It exposes weather forecast CRUD endpoints protected by JWT bearer authentication. In development it validates tokens issued by the local Identity API. In production it validates tokens from Azure Entra ID. It uses CoreDesign.Data for repository-pattern data access and CoreDesign.Identity.Client for authentication setup.

### SampleApi.Identity.Api

A lightweight OAuth2/OIDC identity server built on CoreDesign.Identity.Server. It reads users from a local `identities.json` file and issues JWTs for development. Two accounts are pre-configured:

| Email | Password | Roles |
|---|---|---|
| admin@sampleapi.local | Password1! | DevAdmin, DevAppUsers |
| user@sampleapi.local | Password1! | DevAppUsers |

### SampleApi.Data.MigrationService

A hosted service that runs on startup to ensure the database exists, applies any pending EF Core migrations, and seeds initial data from JSON files in its `SeedData` folder. It is wired into the Aspire AppHost so migrations run automatically before the API receives traffic.

### SampleApi.Api.Tests

xUnit unit tests for the API service layer. Tests use Moq for repository mocking and Bogus for generating realistic fake data. Coverage includes all CRUD operations with success, not-found, and failure scenarios.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (required for the SQL Server container)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling)

Install the Aspire workload if you have not already:

```
dotnet workload install aspire
```

---

## Setup

### 1. Add user secrets

The SQL Server container password is supplied via .NET user secrets so it is never stored in source control. Run the following commands from the repository root.

Initialize user secrets for the AppHost project:

```
dotnet user-secrets init --project src/SampleApi.Aspire.AppHost
```

Set the SQL Server password:

```
dotnet user-secrets set "Parameters:SqlPassword" "my-secret-password" --project src/SampleApi.Aspire.AppHost
```

### 2. Trust the developer SSL certificate

All services run over HTTPS locally. If you see SSL/certificate errors when the app starts, run:

```
dotnet dev-certs https --trust
```

Restart your browser after running this command.

---

## Running the Application

Start the Aspire AppHost project:

```
dotnet run --project src/SampleApi.Aspire.AppHost
```

Aspire will print a dashboard URL to the console (typically `https://localhost:17002`). Open the dashboard to see all running services, logs, and traces.

The services will be available at addresses shown in the dashboard. The main API and Identity API each expose a Scalar UI at `/scalar/v1` for browsing and testing endpoints.

---

## Running Tests

```
dotnet test
```

---

## CoreDesign Packages

This solution is a working example of how to integrate the CoreDesign library suite. The three packages used are described below.

### CoreDesign.Identity.Server

**Used in**: SampleApi.Identity.Api

Provides a ready-made identity server that issues JWTs for development. Registration requires two calls in `Program.cs`:

```csharp
builder.Services.AddIdentityServer(builder.Configuration);
builder.Services.AddJsonFileIdentityStore("identities.json");
```

Then map the endpoints:

```csharp
app.MapIdentityEndpoints();
```

The `identities.json` file in the project root defines users with their passwords, email addresses, and role claims. This replaces the need for a full identity provider during local development.

Configuration lives under `CoreDesign:Identity` in `appsettings.Development.json`:

```json
"CoreDesign": {
  "Identity": {
    "Issuer": "https://identity.sampleapi.local",
    "Audience": "https://api.sampleapi.local",
    "TokenLifetime": "08:00:00"
  }
}
```

### CoreDesign.Identity.Client

**Used in**: SampleApi.Api

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

The identity API base URL and default credentials are read from `appsettings.Development.json` under `IdentityApi`.

### CoreDesign.Data

**Used in**: SampleApi.Api, SampleApi.Data.MigrationService, SampleApi.Api.Tests

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
services.AddTransient<IReadRepository<SampleApiDbContext, WeatherForecast>,
                      ReadRepository<SampleApiDbContext, WeatherForecast>>();
services.AddTransient<ICudRepository<SampleApiDbContext, WeatherForecast>,
                      CudRepository<SampleApiDbContext, WeatherForecast>>();
```

Services then declare their repository dependencies in the constructor and call the async CRUD methods. The repository interfaces are also easy to mock in unit tests using Moq, as shown in `WeatherForecastServiceTests.cs`.

---

## Project Configuration

Shared `appsettings.json` files live in `src/Shared/` and are linked into each project. Environment-specific overrides follow the standard `appsettings.{Environment}.json` convention. Secrets (passwords, connection string credentials) are always supplied via user secrets or environment variables and never committed to source control.
