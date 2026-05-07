# SampleApi.Api

ASP.NET Core Minimal API demonstrating a feature-sliced structure with clean separation between routing, request handling, business logic, and data access.

## Project Structure

```
SampleApi.Api/
├── Data/
│   ├── Migrations/
│   └── SampleApiDbContext.cs
├── Infrastructure/
├── WeatherForecasts/
│   ├── Endpoints/
│   ├── Handlers/
│   ├── Models/
│   └── Services/
├── GlobalUsing.cs
├── ModuleConfig.cs
└── Program.cs
```

## Root Files

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point. Bootstraps Serilog, builds the `WebApplication`, and runs the host. |
| `GlobalUsing.cs` | Project-wide global using statements. |
| `ModuleConfig.cs` | Registers each feature module's services with the DI container and defines all route path constants. |

## Data

Holds the EF Core `DbContext` and database schema definitions.

| File | Purpose |
|------|---------|
| `SampleApiDbContext.cs` | Declares `DbSet` properties and applies all `IEntityTypeConfiguration` implementations via assembly scanning. |
| `Schemas.cs` | Enum used to keep schema name references consistent across the project. |
| `Migrations/` | EF Core migration files and model snapshot (auto-generated, do not edit manually). |

## Infrastructure

Cross-cutting configuration that applies to the whole application rather than any single feature.

| File | Purpose |
|------|---------|
| `App.cs` | Configures the middleware pipeline: HTTPS, CORS, authentication, authorization, output caching, and endpoint mapping. |
| `Configuration.cs` | Registers services during builder setup: database, identity, authorization policies, CORS, output caching, and telemetry. Also registers `BearerSecurityTransformer`, which annotates the OpenAPI document with the Bearer security scheme and automatically excludes anonymous endpoints. |
| `AuthorizationPolicyConfiguration.cs` | Defines role-based authorization policies with environment-specific role mappings (Development, UAT, Production). |
| `AuthorizationRoles.cs` | Constants for role names and policy names referenced across the application. |
| `Endpoints.cs` | Top-level endpoint registration. Delegates to each feature module's endpoint mapper. |
| `Cache.cs` | Output cache policy configuration and the `CacheConfig` enum used for tag-based cache invalidation. |
| `Identity.cs` | Extension method on `HttpContext` that extracts the authenticated user's ID from the `oid` claim. |
| `Scalar.cs` | Registers the OpenAPI and Scalar UI routes in development only. Both routes are marked `AllowAnonymous()` so they are excluded from the Bearer security requirement in the OpenAPI document and accessible without a token. |
| `Serilog.cs` | Configures Serilog enrichment and the Application Insights sink. |

## Feature Modules

Each feature is a self-contained folder containing its own models, service, handlers, and endpoint mappings. Adding a new feature means adding a new folder with the same internal structure and registering it in `ModuleConfig.cs` and `Infrastructure/Endpoints.cs`.

### WeatherForecasts

#### Models

Holds the entity, its EF Core configuration, request/response DTOs, and the mapper between them.

| File | Purpose |
|------|---------|
| `WeatherForecast.cs` | EF Core entity. Inherits `BaseEntity` for common audit fields. |
| `WeatherForecastConfiguration.cs` | EF Core `IEntityTypeConfiguration` implementation. Defines column constraints and indexes. |
| `WeatherForecastRequest.cs` | Inbound DTO for create and update operations. |
| `WeatherForecastResponse.cs` | Outbound DTO returned to callers. Includes computed `TemperatureF`. |
| `Mapper.cs` | Static extension methods that convert between the entity and the request/response DTOs. |

#### Services

Contains the service interface and implementation. Services encapsulate all business logic and communicate results using `OneOf` discriminated unions instead of exceptions.

| File | Purpose |
|------|---------|
| `IWeatherForecastService.cs` | Defines the async CRUD contract with typed result unions for success, not-found, and error cases. |
| `WeatherForecastService.cs` | Implements the contract using the read and CUD repositories. |

#### Handlers

One handler per operation. Each handler calls the service, maps the result to an `IResult`, and handles cache invalidation where required. Handlers are static classes with a single `HandleAsync` method, which is referenced directly in the endpoint mapping.

| File | Purpose |
|------|---------|
| `CreateWeatherForecastHandler.cs` | Calls `CreateAsync`, evicts the cache on success, returns `201 Created`. |
| `GetAllWeatherForecastsHandler.cs` | Calls `GetAllAsync`, returns the list or `404 Not Found`. |
| `GetWeatherForecastHandler.cs` | Calls `GetAsync` by ID, returns the item or `404 Not Found`. |
| `UpdateWeatherForecastHandler.cs` | Calls `UpdateAsync`, evicts the cache on success, returns the updated item. |
| `DeleteWeatherForecastHandler.cs` | Calls `DeleteAsync`, evicts the cache on success, returns `200 OK`. |

#### Endpoints

One endpoint class per operation. Each class is responsible only for registering the route, configuring metadata (name, produces, authorization), and pointing to the corresponding handler. No business logic lives here.

| File | Purpose |
|------|---------|
| `CreateWeatherForecastEndpoint.cs` | `POST /WeatherForecasts` — requires `AdminOnly` policy. |
| `GetAllWeatherForecastsEndpoint.cs` | `GET /WeatherForecasts` — requires `UserOrAdmin` policy, output cached. |
| `GetWeatherForecastEndpoint.cs` | `GET /WeatherForecasts/{id}` — requires `UserOrAdmin` policy, output cached. |
| `UpdateWeatherForecastEndpoint.cs` | `PUT /WeatherForecasts/{id}` — requires `AdminOnly` policy. |
| `DeleteWeatherForecastEndpoint.cs` | `DELETE /WeatherForecasts/{id}` — requires `AdminOnly` policy. |
