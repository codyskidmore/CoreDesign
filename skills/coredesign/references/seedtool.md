# CoreDesign.SeedTool (`dotnet seed`)

Exports live database rows to `MigrationWorker` seed files and compares seed files with a database. It loads your compiled assembly and creates your DbContext through a `CoreDesignSeedFactory<T>`.

## Install and set up

```
dotnet tool install -g CoreDesign.SeedTool      # needs the .NET 11 runtime
dotnet seed setup                                # run at the repository root; writes coredesign.seedtool.json
```

Add the factory to the project that contains the DbContext (public, parameterless constructor):

```csharp
using CoreDesign.Data;

public class MyAppSeedFactory : CoreDesignSeedFactory<MyAppDbContext>
{
    protected override void Configure(DbContextOptionsBuilder<MyAppDbContext> builder, string connectionString)
        => builder.UseNpgsql(connectionString);
    protected override MyAppDbContext Create(DbContextOptions<MyAppDbContext> options) => new(options);
}
```

## coredesign.seedtool.json

Never put a password in this file. For an Aspire app, read the connection string from the AppHost's user secrets:

```json
{
  "assemblyPath": "src/MyApp.Api/bin/Debug/net11.0/MyApp.Api.dll",
  "outputDirectory": "src/MyApp.Data.MigrationService/SeedData",
  "entities": [ "SiteContent", "Category" ],
  "appConfig": {
    "project": "src/MyApp.AppHost",
    "connectionStringKey": "ConnectionStrings:myappdb"
  }
}
```

`{ParameterName}` placeholders in that connection string resolve from `Parameters:ParameterName` in the same user secrets, exactly as Aspire does, so the AppHost redirection pattern (`AddConnectionString` plus `WithConnectionStringRedirection`, see `project-setup.md`) gives the app and the tool the same connection. For a non-Aspire app, point `appConfig.project` at the app itself. Alternatives: a `connectionString` template with `{password}` supplied by `--password`, the `SEEDTOOL_PASSWORD` variable, or `userSecrets` (`project`, `passwordKey`); `--connection` overrides everything.

Paths are relative to the config file, and the tool finds the config by walking up from the current directory. The assembly must be built first.

## Commands

```
dotnet seed export                     # entities from the config
dotnet seed export SiteContent         # specific entities
dotnet seed diff                       # compare seed files with the database
```

Exit codes: 0 success (diff: identical), 1 error, 2 diff found differences, so `dotnet seed diff` works as a CI gate.

## Workflow for changing reference data

1. Change the data through the application in development.
2. `dotnet seed export`, then review with `git diff` and commit.
3. Existing databases keep their old rows, because seeding only inserts missing Ids. To apply the new content in development or staging, add the entity to `MigrationWorker:PurgeBeforeSeed`, run the migration service, then remove it again. Never do this against Production; production content is changed by an administrator.

## Known problems

- **Renamed or moved entity:** seed files are named by fully qualified type name, so the old file stops matching silently. `dotnet seed diff` shows it; `dotnet seed export` regenerates it under the new name; delete the old file.
- **SQL Server in Docker on Windows refuses `localhost`:** use `127.0.0.1` in the connection string, because `localhost` can resolve to IPv6 first.
