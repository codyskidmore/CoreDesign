# CoreDesign.Data

A generic, reusable Entity Framework Core data access layer providing base entity infrastructure, repository abstractions, automatic audit field management, and a migration worker base class for .NET projects.

## Requirements

- .NET 10.0
- Microsoft.EntityFrameworkCore 10.x
- Microsoft.EntityFrameworkCore.Relational 10.x
- Microsoft.Extensions.Configuration.Abstractions 10.x
- Microsoft.Extensions.Hosting.Abstractions 10.x
- Ulid 1.4.x

## What Is Included

**Infrastructure**

- `BaseEntity` - Base class all entities must inherit from. Provides `Id` (Ulid, auto-generated), `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, and `IsDeleted` audit fields.
- `BaseEntityConfiguration<T>` - EF Core `IEntityTypeConfiguration<T>` base that wires up primary key, index, soft-delete query filter, and required audit field constraints.
- `CoreDesignDbContext` - Abstract `DbContext` base class that wires the `AuditInterceptor` automatically. Your `DbContext` must inherit from this instead of `DbContext` directly.
- `AuditInterceptor` - EF Core `SaveChangesInterceptor` that sets audit fields on every save. On insert it sets `CreatedAt`, `CreatedBy`, `UpdatedAt`, and `UpdatedBy` (skipped if `CreatedAt` is already set, allowing seeded data to preserve explicit values). On update it sets `UpdatedAt` and `UpdatedBy`. `CreatedBy` is never modified on update.
- `ICurrentUserAccessor` - Interface the consuming application implements to supply the current user's ID to the interceptor. Backed by `IHttpContextAccessor` in web apps.
- `SystemUserAccessor` - Built-in `ICurrentUserAccessor` implementation that returns `Guid.Empty`. Use this in migration services and test projects where there is no authenticated user.
- `CoreDesignDataExtensions` - Provides `AddCoreDesignData<TCurrentUserAccessor>()` to register `ICurrentUserAccessor` and `AuditInterceptor` in one call.
- `ValueConverters` - Provides `GetUlidConverter()` (Ulid to string) and `GetEnumConverter<TEnum>()` (enum to string) for use in entity configurations.
- `MigrationWorker<TContext>` - Concrete `BackgroundService` that ensures the database exists, applies pending EF Core migrations, seeds from JSON files in the configured seed directory (default: `SeedData`), then stops the host. No subclassing is required. Override the virtual `SeedAsync` method only when custom seed logic is needed.

**Interfaces**

- `IReadRepository<TContext, T>` - Read-only repository interface with `GetAllAsync`, `GetAllAttachedAsync`, `GetAsync`, and `GetAttachedAsync`.
- `ICudRepository<TContext, T>` - Create/Update/Delete repository interface. Methods: `InsertAsync`, `InsertRangeAsync`, `UpdateAsync`, `UpdateRangeAsync`, `SoftDeleteAsync`, `SoftDeleteRangeAsync`, `SoftDeleteCascadeAsync`, `HardDeleteAsync`, `HardDeleteCascadeAsync`.

**Repositories**

- `ReadRepository<TContext, T>` - Concrete read repository. All queries use `AsNoTracking()` by default. Supports optional `where` expressions, `orderBy`, and strongly typed `includes`.
- `CudRepository<TContext, T>` - Concrete CUD repository. Soft-delete methods set `IsDeleted = true` rather than removing rows. Hard-delete methods physically remove rows.

## Setup

**1. Install the NuGet package**

Using the .NET CLI:

```bash
dotnet add package CoreDesign.Data
```

Or add directly to your `.csproj`:

```xml
<ItemGroup>
    <PackageReference Include="CoreDesign.Data" Version="*" />
</ItemGroup>
```

**2. Define your entities**

All entities must inherit from `BaseEntity`. The `Id` property is automatically initialized to a new Ulid at construction time:

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

**4. Implement `ICurrentUserAccessor`**

Create one implementation per application. For a web API, read the `oid` claim from the current HTTP context:

```csharp
public class AppCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public Guid UserId
    {
        get
        {
            var oid = httpContextAccessor.HttpContext?.User.FindFirst("oid")?.Value;
            return Guid.TryParse(oid, out var id) ? id : Guid.Empty;
        }
    }
}
```

For migration services and background workers where there is no HTTP context, use the built-in `SystemUserAccessor` (returns `Guid.Empty`) or provide your own implementation.

**5. Register services**

Call `AddCoreDesignData<T>` once in `Program.cs`, before the DbContext is registered. Also register `IHttpContextAccessor` if your accessor depends on it. Then wire the `AuditInterceptor` when registering the DbContext:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddCoreDesignData<AppCurrentUserAccessor>();

builder.Services.AddDbContextPool<AppDbContext>((sp, options) =>
    options.UseSqlServer(connectionString)
        .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
```

**6. Inherit from `CoreDesignDbContext`**

Your `DbContext` must inherit from `CoreDesignDbContext` instead of `DbContext` directly:

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : CoreDesignDbContext(options)
{
    public DbSet<Widget> Widgets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

`ApplyConfigurationsFromAssembly` scans the assembly for every class that implements `IEntityTypeConfiguration<T>` and calls `Configure` on each one automatically. No manual registration is needed when new entity configuration classes are added.

**7. Register repositories**

Register the repositories in your DI container, once per entity type:

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

Audit fields (`Id`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`) are set automatically by the interceptor. No `userId` parameter is needed on any repository method:

```csharp
// Insert
var widget = new Widget { Name = "Sprocket" };
await cudRepository.InsertAsync(widget, cancellationToken);

// Update
widget.Name = "Updated Sprocket";
await cudRepository.UpdateAsync(widget, cancellationToken);

// Soft delete (sets IsDeleted = true, row is not removed)
await cudRepository.SoftDeleteAsync(id, cancellationToken);

// Soft delete a parent and all child entities in the EF navigation graph
await cudRepository.SoftDeleteCascadeAsync(id, cancellationToken);

// Hard delete (physically removes the row; finds the entity even if soft-deleted)
await cudRepository.HardDeleteAsync(id, cancellationToken);

// Hard delete a parent and all child entities in the EF navigation graph
await cudRepository.HardDeleteCascadeAsync(id, cancellationToken);
```

## Notes

- `BaseEntityConfiguration` applies a global query filter (`e => !e.IsDeleted`) to every entity. Soft-deleted rows are automatically excluded from all queries.
- `HardDeleteAsync` uses `IgnoreQueryFilters()` internally so it can remove rows that were previously soft-deleted.
- Cascade methods walk EF Core's navigation graph in memory. They load child collections before marking or removing them, so large graphs increase memory usage proportionally.
- `GetAllAttachedAsync` and `GetAttachedAsync` return tracked entities for use when you need EF Core to detect changes without an explicit `Attach` call.
- Seeded entities whose JSON includes explicit audit values (non-default `CreatedAt`) are preserved as-is. The interceptor only fills in audit fields when `CreatedAt` is at its default value.

## Migration Worker

`MigrationWorker<TContext>` is a concrete `BackgroundService` that works in any .NET host application, including .NET Aspire migration services. It runs three steps in order when the host starts:

1. **Ensure database**: creates the database if it does not exist.
2. **Migrate**: applies all pending EF Core migrations via `MigrateAsync`.
3. **Seed**: scans the seed directory for `*.json` files and inserts any records that do not yet exist in the database.

When all steps complete, it calls `IHostApplicationLifetime.StopApplication()` and the process exits with code 0. If any step throws, the exception propagates and the process exits with a non-zero code, which blocks deployment pipelines from proceeding.

Both the ensure and migrate steps wrap their database calls in `CreateExecutionStrategy()` so transient SQL Server errors are retried automatically.

### Registration

Register `AddCoreDesignData` before `AddMigrationWorker`. Migration services have no authenticated user, so use `SystemUserAccessor`:

```csharp
builder.Services.AddCoreDesignData<SystemUserAccessor>();
builder.AddMigrationWorker<AppDbContext>();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(MigrationWorker<AppDbContext>.ActivitySourceName));
```

### Seed file naming convention

By default, `MigrationWorker<TContext>` scans a directory named `SeedData` (relative to the working directory) for `*.json` files. Each file must be named after the **fully qualified type name** of the entity it seeds. The worker resolves the filename (without the `.json` extension) to a type in `typeof(TContext).Assembly` and deserializes the JSON as a `List<T>`. Files whose name cannot be resolved to a `BaseEntity` subclass are skipped with a warning.

For an entity `MyApp.Orders.Models.Order` the seed file must be named:

```
MyApp.Orders.Models.Order.json
```

Seed files may include all `BaseEntity` audit fields (`Id`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`). When explicit values are present the interceptor leaves them unchanged. When they are absent the interceptor fills them in with `Guid.Empty` (the `SystemUserAccessor` value) and the current timestamp.

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

### PurgeBeforeSeed

By default the worker only inserts rows that do not yet exist (identified by `BaseEntity.Id`). This is safe to re-run as many times as needed but it means updated seed content is never pushed to a database that already has rows for that entity.

Pass entity type names to `purgeBeforeSeed` to clear specific tables before their seed file is applied. This lets you replace the content of a table on every migration run without having to manually delete rows first.

Short class names and fully qualified type names are both accepted:

```csharp
builder.AddMigrationWorker<AppDbContext>(purgeBeforeSeed: ["SiteContent", "PolicyText"]);
```

The list is additive: tables not in the list use the default insert-only behavior. Purge uses `ExecuteDeleteAsync` on relational providers (one round-trip, no change-tracker overhead) and falls back to a load-then-remove approach on non-relational providers (in-memory databases used in tests).

#### Configuring PurgeBeforeSeed at runtime

The purge list can also be driven entirely from configuration so you can change it between runs without redeploying. Add a `MigrationWorker:PurgeBeforeSeed` array to `appsettings.json`:

```json
{
  "MigrationWorker": {
    "PurgeBeforeSeed": [ "SiteContent" ]
  }
}
```

Or via environment variable (useful in Docker and Aspire):

```
MigrationWorker__PurgeBeforeSeed__0=SiteContent
```

Config-driven names and names passed directly to `AddMigrationWorker` are merged into a single set, so you can combine both. Remove an entry from config and the table reverts to insert-only behavior on the next run.

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
