# Sample.Api

ASP.NET Core Minimal API demonstrating Vertical Slice Architecture (VSA): each HTTP operation is fully self-contained in its own folder, with no shared service layer between operations.

## Project Structure

```
Sample.Api/
├── Data/
│   ├── Migrations/
│   └── SampleDbContext.cs
├── Infrastructure/
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

## Root Files

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point. Bootstraps Serilog, builds the `WebApplication`, and runs the host. |
| `GlobalUsing.cs` | Project-wide global using statements. Includes `WeatherForecasts.Shared` so entity types are available everywhere. |
| `ModuleConfig.cs` | Registers each feature module's handlers and repositories with the DI container, calls `DecorateWithLogging()` to apply all generated logging decorators, and defines all route path constants. |

## Data

Holds the EF Core `DbContext` and database schema definitions.

| File | Purpose |
|------|---------|
| `SampleDbContext.cs` | Declares `DbSet` properties and applies all `IEntityTypeConfiguration` implementations via assembly scanning. |
| `Schemas.cs` | Enum used to keep schema name references consistent across the project. |
| `Migrations/` | EF Core migration files and model snapshot (auto-generated, do not edit manually). |

## Infrastructure

Cross-cutting configuration that applies to the whole application rather than any single feature.

| File | Purpose |
|------|---------|
| `App.cs` | Configures the middleware pipeline: HTTPS, CORS, authentication, authorization, output caching, and endpoint mapping. |
| `Configuration.cs` | Registers services during builder setup: database, identity, authorization, CORS, output caching, and telemetry. Also registers `BearerSecurityTransformer`, which annotates the OpenAPI document with the Bearer security scheme and automatically excludes anonymous endpoints. |
| `Permissions.cs` | Application permission constants (`weather:read`, `weather:write`) passed directly to `RequireAuthorization()`. The underlying policy provider and claim handler live in `CoreDesign.Identity.Client` and are registered automatically by `AddIdentityClient()`. |
| `Endpoints.cs` | Top-level endpoint registration. Delegates to each feature module's endpoint mapper. |
| `Cache.cs` | Output cache policy configuration and the `CacheConfig` enum used for tag-based cache invalidation. |
| `Identity.cs` | Extension method on `HttpContext` that extracts the authenticated user's ID from the `oid` claim. |
| `Scalar.cs` | Registers the OpenAPI and Scalar UI routes in development only. Both routes are marked `AllowAnonymous()` so they are excluded from the Bearer security requirement in the OpenAPI document and accessible without a token. |
| `Serilog.cs` | Configures Serilog enrichment and the Application Insights sink. |

## Feature Modules

Each feature is organized by operation rather than by technical role. A developer tracing any single HTTP operation opens one folder and finds everything: the route definition, the request binding, the data access, and the response shape. Adding a new operation means adding a new sub-folder and registering the endpoint in `ModuleConfig.cs`.

### WeatherForecasts

#### Shared

Holds only what is genuinely shared across all operations: the entity class and its EF Core configuration. Both represent the database table, not any particular operation.

| File | Purpose |
|------|---------|
| `WeatherForecast.cs` | EF Core entity. Inherits `BaseEntity` for common audit fields. |
| `WeatherForecastConfiguration.cs` | EF Core `IEntityTypeConfiguration` implementation. Defines column constraints and unique index on `(Location, Date)`. |

#### Create

`POST /WeatherForecasts` — requires `weather:write` permission.

| File | Purpose |
|------|---------|
| `Request.cs` | Inbound DTO. Includes `ToNewEntity()` to create a `WeatherForecast` from the request. |
| `Response.cs` | Outbound DTO with `TemperatureF` computed from `TemperatureC`. Includes `From(entity)` factory. |
| `Handler.cs` | Inserts via `ICudRepository`, evicts cache on success, returns `201 Created` with location header. |
| `Endpoint.cs` | Registers the POST route, declares produces metadata, and applies the authorization policy. |

#### Delete

`DELETE /WeatherForecasts/{id}` — requires `weather:write` permission.

| File | Purpose |
|------|---------|
| `Handler.cs` | Deletes via `ICudRepository`. Returns `200 OK` on success or `404 Not Found` if the record does not exist. Evicts cache on success. |
| `Endpoint.cs` | Registers the DELETE route and applies the authorization policy. |

#### GetAll

`GET /WeatherForecasts` — requires `weather:read` permission, output cached.

| File | Purpose |
|------|---------|
| `Response.cs` | Outbound DTO for the collection. Includes `From(entity)` factory. |
| `Handler.cs` | Retrieves all records via `IReadRepository`, maps to `Response`, returns `200 OK` or `404 Not Found` when no records exist. |
| `Endpoint.cs` | Registers the GET route, applies cache policy and authorization. |

#### GetById

`GET /WeatherForecasts/{id}` — requires `weather:read` permission, output cached.

| File | Purpose |
|------|---------|
| `Response.cs` | Outbound DTO for a single record. Includes `From(entity)` factory. Also used as the response type for the Update operation. |
| `Handler.cs` | Retrieves a single record by ID via `IReadRepository`, returns `200 OK` or `404 Not Found`. |
| `Endpoint.cs` | Registers the GET-by-id route, applies cache policy and authorization. Defines the route name used by Create's `CreatedAtRoute` response. |

#### Update

`PUT /WeatherForecasts/{id}` — requires `weather:write` permission.

| File | Purpose |
|------|---------|
| `Request.cs` | Inbound DTO. Includes `Apply(entity)` to update an existing `WeatherForecast` in place. |
| `Handler.cs` | Fetches the entity, applies the request, saves via `ICudRepository`. Returns `200 OK` with the updated record, `404 Not Found`, or `400 Bad Request` if the repository update fails. |
| `Endpoint.cs` | Registers the PUT route and applies the authorization policy. |
