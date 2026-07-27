# Sample.Data.MigrationService

A .NET hosted service that ensures the database exists, applies pending EF Core migrations, and seeds reference data before any API receives traffic. It uses `MigrationWorker<TContext>` from `CoreDesign.Data` and exits cleanly when all steps complete.

## How it works

`Program.cs` registers `MigrationWorker<SampleDbContext>` and its OpenTelemetry `ActivitySource`:

```csharp
builder.AddMigrationWorker<SampleDbContext>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(MigrationWorker<SampleDbContext>.ActivitySourceName));
```

On startup the worker runs three steps in order:

1. **Ensure database**: creates the database if it does not exist yet (using `IRelationalDatabaseCreator`).
2. **Migrate**: applies all pending EF Core migrations via `MigrateAsync`.
3. **Seed**: scans the configured seed directory (default: `SeedData/`), matches each `*.json` file to a `BaseEntity` subclass by filename, and inserts any records that do not already exist (identified by `Id`). The seed directory can be changed without modifying any code (see [Overriding the seed directory](#overriding-the-seed-directory)).

When all three steps complete successfully, `IHostApplicationLifetime.StopApplication()` is called and the process exits with code 0. If any step throws, the exception propagates and the process exits with a non-zero code.

## Seed data

Seed files live in the `SeedData/` folder and are set to `CopyToOutputDirectory: PreserveNewest` so they are present in both local runs and published deployments.

Each file is a JSON array of fully populated entity objects, including the `BaseEntity` audit fields (`Id`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `IsDeleted`). The seeding check is idempotent: a record is skipped if a row with the same `Id` already exists in the database, including soft-deleted rows.

### Seed file naming convention

The filename (without the `.json` extension) must be the **fully qualified type name** of the entity class as it appears in `Sample.Api`. This includes every namespace segment separated by dots, with no assembly name prefix.

For example, the `WeatherForecast` class lives in the namespace `Sample.Api.WeatherForecasts.Models`, so its seed file is named:

```
SeedData/Sample.Api.WeatherForecasts.Models.WeatherForecast.json
```

If the filename does not resolve to a known `BaseEntity` subclass in the `Sample.Api` assembly, the file is skipped with a warning and no data is inserted. This means a renamed class, a typo in the filename, or a missing namespace segment will silently produce no seed data.

Currently seeded:

| File | Entity | Records |
| --- | --- | --- |
| `Sample.Api.WeatherForecasts.Models.WeatherForecast.json` | `WeatherForecast` | 10 |

## Overriding the seed directory

The seed directory defaults to `SeedData` but can be changed by passing a different path to `AddMigrationWorker` in `Program.cs`. No other code changes are needed:

```csharp
builder.AddMigrationWorker<SampleDbContext>("ReferenceData");
```

The path is relative to the working directory. Absolute paths are also accepted. If the directory does not exist at runtime, a warning is logged and seeding is skipped without throwing.

This is useful when different environments need different seed sets, or when reference data and test fixtures are kept in separate folders.

## Adding a new seed file

1. Create a JSON file in `SeedData/` named after the fully qualified type name of the entity: `<Full.Namespace.ClassName>.json`.
2. Populate it with a JSON array of entity objects. Every record must include all `BaseEntity` fields (`Id`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `IsDeleted`).
3. Add a `<None Update="SeedData\...">` entry to the `.csproj` with `CopyToOutputDirectory: PreserveNewest`.

No code changes are required. The worker discovers and seeds the new file automatically on the next run.

## Configuration

The migration service reads its configuration from `appsettings.json` and `appsettings.Development.json` (linked from `src/Shared/`). The only required section is `DatabaseOptions`:

```json
"DatabaseOptions": {
  "HostName": "sample-postgres",
  "DatabaseName": "sample-db",
  "HostPort": 55432,
  "ConnectionStringName": "sampledb"
}
```

`DatabaseName` is the connection string key that `builder.Configuration.GetConnectionString(dbOptions.DatabaseName)` looks up in `IConfiguration`. In local development under Aspire this is injected by the AppHost. In a pipeline or non-Aspire environment, set the environment variable:

```
ConnectionStrings__sample-db=<connection-string>
```

## Running locally

The AppHost starts this service automatically and waits for it to complete before marking the other services as healthy. To run it in isolation (requires a reachable PostgreSQL server):

```
dotnet run --project src/Sample.Data.MigrationService
```

## Running in a GitHub Actions pipeline

Set `ConnectionStrings__sample-db` from a GitHub Secret and run:

```yaml
- name: Run database migrations and seed
  env:
    ConnectionStrings__sample-db: ${{ secrets.AZURE_POSTGRES_CONNECTION_STRING }}
  run: dotnet run --project src/Sample.Data.MigrationService --configuration Release --no-build
```

The step exits 0 on success and non-zero on failure, so a failed migration blocks the deployment before any application containers are updated. See the root `README.md` for the full workflow.

## MigrationWorker base class

`MigrationWorker<TContext>` in `CoreDesign.Data` owns the ensure-database, migrate, seed, and application-lifecycle steps. The default seeding behavior scans the configured directory for `*.json` files and uses filenames to resolve entity types. To replace or extend that behavior, create a subclass and override `SeedAsync`.

See [CoreDesign.Data/README.md](../../../src/CoreDesign.Data/README.md) for the full base class reference.
