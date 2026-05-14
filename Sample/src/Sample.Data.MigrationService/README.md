# Sample.Data.MigrationService

A .NET hosted service that ensures the database exists, applies pending EF Core migrations, and seeds reference data before any API receives traffic. It inherits from `MigrationWorker<TContext>` in `CoreDesign.Data` and exits cleanly when all steps complete.

## How it works

`SampleMigrationWorker` inherits from `MigrationWorker<SampleDbContext>`. On startup the base class runs three steps in order:

1. **Ensure database**: creates the database if it does not exist yet (using `IRelationalDatabaseCreator`).
2. **Migrate**: applies all pending EF Core migrations via `MigrateAsync`.
3. **Seed**: calls the `SeedAsync` override, which loads `SeedData/*.json` files and inserts any records that do not already exist (identified by `Id`).

When all three steps complete successfully, `IHostApplicationLifetime.StopApplication()` is called and the process exits with code 0. If any step throws, the exception propagates and the process exits with a non-zero code.

## Seed data

Seed files live in the `SeedData/` folder and are set to `CopyToOutputDirectory: PreserveNewest` so they are present in both local runs and published deployments.

Each file is a JSON array of fully populated entity objects, including the `BaseEntity` audit fields (`Id`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `IsDeleted`). The seeding check is idempotent: a record is skipped if a row with the same `Id` already exists in the database, including soft-deleted rows.

Currently seeded:

| File | Entity | Records |
| --- | --- | --- |
| `Sample.Api.WeatherForecasts.Models.WeatherForecast.json` | `WeatherForecast` | 10 |

## Adding a new seed file

1. Create a JSON file in `SeedData/` following the naming pattern `<Namespace>.<ClassName>.json`.
2. Add a `<None Update="SeedData\...">` entry to the `.csproj` with `CopyToOutputDirectory: PreserveNewest`.
3. In `MigrationService.cs`, add a `LoadObjectFromJsonFile` call and pass the result to `SeedEntitiesAsync`.

## Configuration

The migration service reads its configuration from `appsettings.json` and `appsettings.Development.json` (linked from `src/Shared/`). The only required section is `DatabaseOptions`:

```json
"DatabaseOptions": {
  "HostName": "sample-mssql",
  "DatabaseName": "sample-db",
  "HostPort": 52881,
  "ConnectionStringName": "sampledb"
}
```

`DatabaseName` is the connection string key that `AddSqlServerDbContext` looks up in `IConfiguration`. In local development under Aspire this is injected by the AppHost. In a pipeline or non-Aspire environment, set the environment variable:

```
ConnectionStrings__sample-db=<connection-string>
```

## Running locally

The AppHost starts this service automatically and waits for it to complete before marking the other services as healthy. To run it in isolation (requires a reachable SQL Server):

```
dotnet run --project src/Sample.Data.MigrationService
```

## Running in a GitHub Actions pipeline

Set `ConnectionStrings__sample-db` from a GitHub Secret and run:

```yaml
- name: Run database migrations and seed
  env:
    ConnectionStrings__sample-db: ${{ secrets.AZURE_SQL_CONNECTION_STRING }}
  run: dotnet run --project src/Sample.Data.MigrationService --configuration Release --no-build
```

The step exits 0 on success and non-zero on failure, so a failed migration blocks the deployment before any application containers are updated. See the root `README.md` for the full workflow.

## MigrationWorker base class

`SampleMigrationWorker` inherits from `CoreDesign.Data.Infrastructure.MigrationWorker<TContext>`. The base class owns the ensure-database, migrate, and application-lifecycle steps. Consuming projects override only `SeedAsync` and call `SeedEntitiesAsync<T>` for each entity set they need to seed.

See [CoreDesign.Data/README.md](../../../src/CoreDesign.Data/README.md) for the full base class reference.
