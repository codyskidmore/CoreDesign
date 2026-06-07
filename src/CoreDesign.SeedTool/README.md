# CoreDesign.SeedTool

A `dotnet tool` for exporting live entity data to `MigrationWorker`-compatible seed files and comparing seed files against a live database. Works with any project using `CoreDesign.Data`.

## Requirements

- .NET 10.0 SDK (includes the `Microsoft.AspNetCore.App` shared runtime)
- `CoreDesign.Data` 1.1.0 or later in your application project

## Installation

```bash
dotnet tool install -g CoreDesign.SeedTool
```

Or install locally (per-repository):

```bash
dotnet tool install --local CoreDesign.SeedTool
```

## Setup

Run the interactive setup from your repository root. It creates `coredesign.seedtool.json`:

```
dotnet seed setup
```

Setup prompts for the assembly path, seed data directory, entity list, and then asks how the tool should obtain a database connection. There are four connection modes; pick the one that matches your project:

| Mode | Best for |
|---|---|
| App config | Any app (Aspire or non-Aspire) where the connection string lives in `appsettings.json` or user secrets |
| Template + user secrets | Apps using a connection string template where the password is stored separately in user secrets |
| Template only | Any app; password supplied at runtime via `--password` or `SEEDTOOL_PASSWORD` |
| Direct | Dev-only configs not committed to source control |

After setup, export with no extra flags:

```
dotnet seed export
```

### Connection modes

#### App config

Reads the connection string from the specified project's configuration stack (`appsettings.json`, `appsettings.Development.json`, and user secrets), exactly as the app does at runtime. Nothing is stored in `coredesign.seedtool.json` that could go stale.

Any `{ParameterName}` placeholders in the resolved connection string are automatically substituted from `Parameters:ParameterName` in the same config stack. This matches Aspire's own parameter substitution behavior, so pointing `appConfig` at the AppHost project works for Aspire apps without any extra steps.

**Aspire setup:** store the connection string template and password parameter in AppHost user secrets, then point `appConfig` at the AppHost:

```
dotnet user-secrets set "ConnectionStrings:mydb" "Server=localhost,1433;Database=mydb;User Id=sa;Password={SqlPassword};TrustServerCertificate=True" --project src/MyApp.AppHost
dotnet user-secrets set "Parameters:SqlPassword" "your-password" --project src/MyApp.AppHost
```

```json
{
  "assemblyPath": "src/MyApp.Api/bin/Debug/net10.0/MyApp.Api.dll",
  "outputDirectory": "src/MyApp.MigrationService/SeedData",
  "appConfig": {
    "project": "src/MyApp.AppHost",
    "connectionStringKey": "ConnectionStrings:mydb"
  }
}
```

**Non-Aspire setup:** store the full connection string in the app project's user secrets:

```
dotnet user-secrets set "ConnectionStrings:mydb" "Server=...;Password=secret" --project src/MyApp.Api
```

```json
{
  "appConfig": {
    "project": "src/MyApp.Api",
    "connectionStringKey": "ConnectionStrings:mydb"
  }
}
```

The `connectionStringKey` follows standard .NET configuration key syntax. `ConnectionStrings:mydb` is equivalent to `GetConnectionString("mydb")`.

#### Template + user secrets

Stores a connection string template in `coredesign.seedtool.json` and reads the password at runtime from a project's user secrets. No password is ever written to the config file.

```json
{
  "assemblyPath": "src/MyApp.Api/bin/Debug/net10.0/MyApp.Api.dll",
  "outputDirectory": "src/MyApp.MigrationService/SeedData",
  "connectionString": "Server=localhost,1433;Database=mydb;User Id=sa;Password={password};TrustServerCertificate=True",
  "userSecrets": {
    "project": "src/MyApp.AppHost",
    "passwordKey": "Parameters:SqlPassword"
  }
}
```

The tool reads the `UserSecretsId` from the `.csproj` in `project`, loads `secrets.json` from the user secrets store, and substitutes the value at `passwordKey` into `{password}` at runtime.

#### Template only

Stores the template in `coredesign.seedtool.json`; the password must be supplied at runtime:

```json
{
  "connectionString": "Host=localhost;Database=myapp;Username=app;Password={password}"
}
```

```
dotnet seed export --password mysecret
$env:SEEDTOOL_PASSWORD = "mysecret"   # PowerShell, session only
export SEEDTOOL_PASSWORD="mysecret"   # bash/zsh, session only
```

#### Password resolution order

When `{password}` appears in the connection string, the tool resolves it in this order and uses the first value found:

1. `--password` flag
2. `SEEDTOOL_PASSWORD` environment variable
3. `userSecrets` config (project + passwordKey)

`--connection` bypasses all resolution and uses the supplied string directly.

### Manual config

`coredesign.seedtool.json` can also be written by hand. Top-level fields:

| Field | Required | Description |
|---|---|---|
| `assemblyPath` | Yes | Path to the compiled assembly. Relative to the config file. |
| `outputDirectory` | No | Where seed files are written and read. Default: `SeedData`. Relative to the config file. |
| `entities` | No | Entity names to export. Short class name or fully qualified. Omit to export all entities. |
| `connectionString` | No | Connection string or template. Use `{password}` as a credential placeholder. |
| `userSecrets` | No | Reads the `{password}` value from user secrets at runtime. |
| `appConfig` | No | Reads the full connection string from the app's config stack at runtime. |

`userSecrets` fields:

| Field | Description |
|---|---|
| `project` | Path to the project whose user secrets contain the password. Relative to the config file. |
| `passwordKey` | Key in `secrets.json`, using `:` as a path separator (e.g. `Parameters:SqlPassword`). |

`appConfig` fields:

| Field | Description |
|---|---|
| `project` | Path to the app project. Relative to the config file. |
| `connectionStringKey` | Configuration key for the connection string (e.g. `ConnectionStrings:mydb`). |

### Connection string formats

The `connectionString` field accepts any connection string your EF Core provider understands. Replace the password value with `{password}` so the credential is never stored in the config file.

**PostgreSQL (Npgsql)**

```
Host=localhost;Port=5432;Database=myapp;Username=app;Password={password}
```

**SQL Server (Microsoft.Data.SqlClient)**

```
Server=localhost;Database=myapp;User Id=app;Password={password};TrustServerCertificate=True
```

Named instance:

```
Server=localhost\SQLEXPRESS;Database=myapp;User Id=app;Password={password};TrustServerCertificate=True
```

**MySQL / MariaDB (Pomelo.EntityFrameworkCore.MySql)**

```
Server=localhost;Port=3306;Database=myapp;User=app;Password={password}
```

**SQLite**

SQLite connection strings do not contain credentials. Pass the full string directly. No `{password}` placeholder is needed:

```json
{
  "connectionString": "Data Source=myapp.db"
}
```

Or supply it at runtime with `--connection`:

```
dotnet seed export --connection "Data Source=myapp.db"
```

### Factory implementation

Add a class to your application project that inherits `CoreDesignSeedFactory<TContext>` from `CoreDesign.Data`. This tells the tool how to create your DbContext from a connection string:

```csharp
using CoreDesign.Data;

public class AppSeedFactory : CoreDesignSeedFactory<AppDbContext>
{
    protected override void Configure(DbContextOptionsBuilder<AppDbContext> builder, string cs)
        => builder.UseNpgsql(cs);  // or UseSqlServer, UseSqlite, etc.

    protected override AppDbContext Create(DbContextOptions<AppDbContext> options)
        => new(options);
}
```

The factory is discovered by reflection at runtime. It must have a public parameterless constructor. No `AuditInterceptor` or `ICurrentUserAccessor` is needed since the tool only reads data.

## Usage

Build your application project first, then run:

```bash
# Export entities listed in coredesign.seedtool.json
dotnet seed export --password mysecret

# Export specific entities (overrides the entity list in coredesign.seedtool.json)
dotnet seed export SiteContent SiteSettings --password mysecret

# Compare seed files to the database
dotnet seed diff --password mysecret

# Compare specific entities only
dotnet seed diff SiteContent --password mysecret
```

### Exit codes

| Code | Meaning |
|---|---|
| 0 | Success (diff: no differences) |
| 1 | Error (config not found, build required, connection failed, etc.) |
| 2 | Diff completed but differences were found (useful as a CI gate) |

### Options

| Option | Description |
|---|---|
| `--connection <cs>` | Full connection string override (skips template substitution) |
| `--password <pwd>` | Database password substituted into `{password}` in `connectionString` |
| `--output <dir>` | Override `outputDirectory` from `coredesign.seedtool.json` |
| `--config <path>` | Explicit path to `coredesign.seedtool.json` |
| `--help`, `-h` | Show usage |

## Config file discovery

`dotnet seed` searches for `coredesign.seedtool.json` by walking up from the current working directory, the same way tools like `dotnet ef` discover project context. You can run the command from any subdirectory of your repository and it will find the config at the root.

## Typical workflow

1. Edit data through your application UI.
2. Run `dotnet seed export` to capture the current state.
3. Review changes with `git diff`.
4. Commit the updated seed files.
5. Before deploying, add the entity to `MigrationWorker:PurgeBeforeSeed` in the migration service's `appsettings.json` so the updated content replaces existing rows.
6. Deploy. Remove the entity from `PurgeBeforeSeed` afterward to restore insert-only behavior.

See the `CoreDesign.Data` README for documentation on `PurgeBeforeSeed`.

## Known issues

### Entity rename or namespace change breaks seed file matching

Seed files are named using the entity's fully qualified type name (e.g. `MyApp.Features.Content.Models.SiteContent.json`). The migration service locates the matching entity by that name at seed time. If an entity class is renamed or moved to a different namespace, the existing seed file no longer matches and the data will not be seeded, silently, with no error.

**Detection:** Run `dotnet seed diff` after any rename or namespace change. The old filename will appear as unmatched and the new filename will be missing entirely, making the break immediately visible.

**Recovery:** Run `dotnet seed export` to regenerate seed files under the correct names, then delete the old files and commit the result. Update `MigrationWorker:PurgeBeforeSeed` in the migration service's `appsettings.json` if the entity's data needs to be replaced rather than appended on the next deploy.

### SQL Server on Docker for Windows, connection refused when using `localhost`

Docker on Windows binds published ports to `127.0.0.1` by default. On Windows 11, `localhost` may resolve to IPv6 (`::1`) first. Because the Docker listener is IPv4-only, the connection is actively refused even though the container is running.

Use `127.0.0.1` in place of `localhost` in the connection string stored in user secrets or `coredesign.seedtool.json`:

```
Server=127.0.0.1,1433;Database=mydb;User Id=sa;Password=...;TrustServerCertificate=True
```

## Version compatibility

Install the same major version of `CoreDesign.SeedTool` as the `CoreDesign.Data` package used by your application.

| CoreDesign.SeedTool | CoreDesign.Data |
|---|---|
| 1.x | 1.1.x |

## Feedback

Open an issue at [github.com/codyskidmore/CoreDesign/issues](https://github.com/codyskidmore/CoreDesign/issues).
