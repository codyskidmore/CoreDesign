# CoreDesign.Data

A generic, reusable Entity Framework Core data access layer providing base entity infrastructure, repository abstractions, and a migration worker base class for .NET projects.

## Requirements

- .NET 10.0
- Microsoft.EntityFrameworkCore 10.x
- Microsoft.EntityFrameworkCore.SqlServer 10.x
- Microsoft.Extensions.Hosting.Abstractions 10.x
- Ulid 1.4.x

## What Is Included

**Infrastructure**

- `BaseEntity` - Base class all entities must inherit from. Provides `Id` (Ulid), `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, and `IsDeleted` audit fields.
- `BaseEntityConfiguration<T>` - EF Core `IEntityTypeConfiguration<T>` base that wires up primary key, index, soft-delete query filter, and required audit field constraints.
- `BaseEntityExtensionMethods` - Extension methods `InitializeAuditFields` and `UpdateAuditFields` for setting audit fields on insert and update.
- `ValueConverters` - Provides `GetUlidConverter()` (Ulid to string) and `GetEnumConverter<TEnum>()` (enum to string) for use in entity configurations.
- `MigrationWorker<TContext>` - Concrete `BackgroundService` that ensures the database exists, applies pending EF Core migrations, seeds from JSON files in the configured seed directory (default: `SeedData`), then stops the host. No subclassing is required. Override the virtual `SeedAsync` method only when custom seed logic is needed.

**Interfaces**

- `IReadRepository<TContext, T>` - Read-only repository interface with `GetAllAsync`, `GetAllAttachedAsync`, `GetAsync`, and `GetAttachedAsync`.
- `ICudRepository<TContext, T>` - Create/Update/Delete repository interface with `InsertAsync`, `InsertRangeAsync`, `UpdateAsync`, `UpdateRangeAsync`, `DeleteAsync`, and `DeleteRangeAsync`.

**Repositories**

- `ReadRepository<TContext, T>` - Concrete read repository. All queries use `AsNoTracking()` by default. Supports optional `where` expressions, `orderBy`, and strongly typed `includes`.
- `CudRepository<TContext, T>` - Concrete CUD repository. Soft-deletes by setting `IsDeleted = true` rather than removing rows.

## Setup

**1. Install the NuGet package**

Using the .NET CLI:

```bash
dotnet add package CoreDesign.Data
```

Using the NuGet Package Manager Console:

```powershell
Install-Package CoreDesign.Data
```

Or add directly to your `.csproj`:

```xml
<ItemGroup>
    <PackageReference Include="CoreDesign.Data" Version="*" />
</ItemGroup>
```

**2. Define your entities**

All entities must inherit from `BaseEntity`:

```csharp
public class Widget : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}
```

**3. Configure your entities**

Inherit from `BaseEntityConfiguration<T>` and call `base.Configure(builder)` to apply the standard audit field and query filter setup:

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

**4. Register your DbContext**

Your `DbContext` must inherit from EF Core's `DbContext`. Override `OnModelCreating` and call `ApplyConfigurationsFromAssembly` to discover all `BaseEntityConfiguration<T>` implementations automatically. No manual registration is needed when new entity configuration classes are added:

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app");

        // Discovers and applies every BaseEntityConfiguration<T> in the assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

`HasDefaultSchema` is optional but scopes all tables to a named schema so they do not land in `dbo`. `ApplyConfigurationsFromAssembly` scans the assembly for every class that implements `IEntityTypeConfiguration<T>` and calls `Configure` on each one.

**5. Register repositories**

Register the repositories in your DI container, once per entity type that needs them:

```csharp
services.AddTransient<IReadRepository<AppDbContext, Widget>, ReadRepository<AppDbContext, Widget>>();
services.AddTransient<ICudRepository<AppDbContext, Widget>, CudRepository<AppDbContext, Widget>>();
```

## Usage

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

## Notes

- `BaseEntityConfiguration` applies a global query filter (`e => !e.IsDeleted`) to every entity. Soft-deleted rows are automatically excluded from all queries.
- `GetAllAttachedAsync` and `GetAttachedAsync` return tracked entities for use when you need EF Core to detect changes without an explicit `Attach` call.
- `InitializeAuditFields` must be called on new entities before insert; `CudRepository` handles this automatically when using `InsertAsync` or `InsertRangeAsync`.

## Migration Worker

`MigrationWorker<TContext>` is a concrete `BackgroundService` that works in any .NET host application, including .NET Aspire migration services. It runs three steps in order when the host starts:

1. **Ensure database** — creates the database if it does not exist.
2. **Migrate** — applies all pending EF Core migrations via `MigrateAsync`.
3. **Seed** — scans the seed directory for `*.json` files and inserts any records that do not yet exist in the database.

When all steps complete, it calls `IHostApplicationLifetime.StopApplication()` and the process exits with code 0. If any step throws, the exception propagates and the process exits with a non-zero code, which blocks deployment pipelines from proceeding.

Both the ensure and migrate steps wrap their database calls in `CreateExecutionStrategy()` so transient SQL Server errors are retried automatically.

### Registration

Use `AddMigrationWorker<TContext>` in `Program.cs` to register the worker as a hosted service. Register the worker's `ActivitySource` with OpenTelemetry separately:

```csharp
builder.AddMigrationWorker<AppDbContext>();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(MigrationWorker<AppDbContext>.ActivitySourceName));
```

### Seed file naming convention

By default, `MigrationWorker<TContext>` scans a directory named `SeedData` (relative to the working directory) for `*.json` files. Each file must be named after the **fully qualified type name** of the entity it seeds. The worker resolves the filename (without the `.json` extension) to a type in `typeof(TContext).Assembly` and deserializes the JSON as a `List<T>`. Files whose name cannot be resolved to a `BaseEntity` subclass are skipped with a warning.

For an entity `MyApp.Orders.Models.Order` the seed file must be named:

```
SeedData/MyApp.Orders.Models.Order.json
```

Using any other name causes the file to be skipped silently. The fully qualified name includes every namespace segment, separated by dots, with no assembly name prefix.

### Overriding the seed directory

Pass a custom directory path as the second argument to `AddMigrationWorker`:

```csharp
builder.AddMigrationWorker<AppDbContext>("ReferenceData");
```

The path is relative to the working directory. Absolute paths are also accepted. If the directory does not exist at runtime the worker logs a warning and skips seeding without throwing.

### SeedEntitiesAsync

`SeedEntitiesAsync<T>` is a protected helper. It accepts a deserialized sequence of entities and inserts any that do not yet exist in the database, identified by `BaseEntity.Id`. Existence is checked with `IgnoreQueryFilters()` so soft-deleted rows count as existing and no duplicate-key errors occur on re-runs:

```csharp
protected async Task SeedEntitiesAsync<T>(
    TContext dbContext,
    IEnumerable<T> entities,
    CancellationToken cancellationToken)
    where T : BaseEntity
```

The writes are wrapped in `CreateExecutionStrategy()` for the same transient-error retry behaviour as the migration steps.

### Custom seed logic

Override `SeedAsync` to replace or extend the default directory-scanning behavior. Call `SeedFromDirectoryAsync` or `SeedEntitiesAsync` as needed:

```csharp
public class AppMigrationWorker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    ILogger<AppMigrationWorker> logger)
    : MigrationWorker<AppDbContext>(serviceProvider, lifetime, logger)
{
    protected override async Task SeedAsync(AppDbContext dbContext, CancellationToken ct)
    {
        // Seed from the default directory first, then apply supplemental data.
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

### Running outside Aspire

`MigrationWorker<TContext>` has no dependency on the Aspire AppHost. It works in any hosted environment as long as a `TContext` is registered in the DI container. In a GitHub Actions pipeline, supply the connection string as an environment variable and run the migration project with `dotnet run`:

```yaml
- name: Run migrations
  env:
    ConnectionStrings__my-db: ${{ secrets.AZURE_SQL_CONNECTION_STRING }}
  run: dotnet run --project src/MyApp.MigrationService --configuration Release --no-build
```

The process exits 0 on success and non-zero on failure, making it safe to use as a deployment gate step.

## Feedback

Feedback on this package is welcome. If you run into a missing feature, an unexpected behavior, or something that required more effort than it should have, open an issue at [github.com/codyskidmore/CoreDesign/issues](https://github.com/codyskidmore/CoreDesign/issues) or tag [@codyskidmore](https://github.com/codyskidmore). Suggestions about missing features and priority input are especially appreciated.
