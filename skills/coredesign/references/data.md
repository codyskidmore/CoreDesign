# CoreDesign.Data

Namespaces: `CoreDesign.Data.Infrastructure` (entities, DbContext, interceptor, worker), `CoreDesign.Data.Interfaces`, `CoreDesign.Data.Repositories`, and `CoreDesign.Data` for `CoreDesignSeedFactory`.

## Contents

1. Entities and configuration
2. DbContext and registration
3. Current user and audit fields
4. Repositories: every method
5. Patterns for writes
6. Migration worker and seeding
7. PurgeBeforeSeed
8. Migrations

## 1. Entities and configuration

```csharp
public class Order : BaseEntity
{
    public required string Number { get; set; }
    public OrderStatus Status { get; set; }
    public Ulid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
}
```

`BaseEntity` provides `Ulid Id` (generated), `DateTime CreatedAt`, `DateTime UpdatedAt`, `Guid CreatedBy`, `Guid UpdatedBy` and `bool IsDeleted`. Never redeclare them.

```csharp
public class OrderConfiguration : BaseEntityConfiguration<Order>
{
    public override void Configure(EntityTypeBuilder<Order> builder)
    {
        base.Configure(builder);
        builder.Property(e => e.Number).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Status).HasConversion(ValueConverters.GetEnumConverter<OrderStatus>());
        builder.Property(e => e.CustomerId).HasConversion(ValueConverters.GetUlidConverter());
        builder.HasIndex(e => e.Number).IsUnique();
        builder.ToTable("Orders", nameof(Schemas.MyApp));
    }
}
```

`base.Configure` sets the key, the soft-delete query filter (`!IsDeleted`), and required audit columns. `ValueConverters.GetUlidConverter()` stores Ulid foreign keys as strings, matching the primary key; `GetEnumConverter<T>()` stores enums as their names.

Every `DateTime` and `DateTime?` in the model is normalized to UTC on write and stamped `Kind = Utc` on read by `CoreDesignDbContext`. Assign `DateTime.UtcNow`; don't add per-property converters for this.

## 2. DbContext and registration

```csharp
public class MyAppDbContext(DbContextOptions<MyAppDbContext> options) : CoreDesignDbContext(options)
{
    public DbSet<Order> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(nameof(Schemas.MyApp));
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MyAppDbContext).Assembly);
    }
}
```

If you override `ConfigureConventions`, call `base.ConfigureConventions(configurationBuilder)` or the UTC convention is lost.

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddCoreDesignData<CurrentUserAccessor>();          // before the DbContext
builder.Services.AddDbContextPool<MyAppDbContext>((sp, options) =>
    options.UseNpgsql(connectionString)                              // or UseSqlServer
        .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
```

Without `AddInterceptors(... AuditInterceptor ...)` the audit fields stay at their defaults and inserts can fail on required columns.

## 3. Current user and audit fields

`ICurrentUserAccessor` has one member, `Guid UserId`. The API implementation reads the `oid` claim (see `project-setup.md`); hosts without a user (migration service, background jobs, tests) register `SystemUserAccessor`, which returns `Guid.Empty`.

The `AuditInterceptor` sets `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` on insert and only `UpdatedAt`, `UpdatedBy` on update. When `CreatedAt` already has a value (seeded rows), it keeps the given values. So: never set these fields in handlers, and never pass a user ID to a repository.

To show who did something in a notification or response, read the user from `HttpContext` in the endpoint and pass it to the handler as a parameter; that is display data, not audit data.

## 4. Repositories: every method

Register one pair per entity:

```csharp
s.AddTransient<IReadRepository<MyAppDbContext, Order>, ReadRepository<MyAppDbContext, Order>>();
s.AddTransient<ICudRepository<MyAppDbContext, Order>, CudRepository<MyAppDbContext, Order>>();
```

`IReadRepository<TContext, T>` (all queries are `AsNoTracking` unless the name says Attached; soft-deleted rows are excluded):

| Method | Returns |
|---|---|
| `GetAllAsync(CancellationToken)` | `Task<IList<T>>` |
| `GetAllAsync(whereExpression?, orderBy?, includes?, CancellationToken)` | `Task<IList<T>>` |
| `GetAllAttachedAsync(CancellationToken)` | `Task<IList<T>>`, tracked |
| `GetAsync(expression, includes?, CancellationToken)` | `Task<T?>` |
| `GetAttachedAsync(expression, includes?, CancellationToken)` | `Task<T?>`, tracked |

```csharp
var open = await readRepository.GetAllAsync(
    whereExpression: o => o.Status == OrderStatus.Open,
    orderBy: q => q.OrderByDescending(o => o.CreatedAt),
    includes: q => q.Include(o => o.Lines).ThenInclude(l => l.Product),
    token: ct);

var order = await readRepository.GetAsync(o => o.Id == id, null, ct);
```

`ICudRepository<TContext, T>`, every method returns `Task<bool>` (false when nothing was written or the row was not found):

| Method | Effect |
|---|---|
| `InsertAsync(entity, ct)` / `InsertRangeAsync(entities, ct)` | Adds and saves |
| `UpdateAsync(entity, ct)` / `UpdateRangeAsync(entities, ct)` | Saves changes |
| `SoftDeleteAsync(id, ct)` / `SoftDeleteRangeAsync(entities, ct)` | Sets `IsDeleted = true` |
| `SoftDeleteCascadeAsync(id, ct)` | Soft-deletes the entity and its child collections (loads them first) |
| `HardDeleteAsync(id, ct)` | Removes the row, even if already soft-deleted |
| `HardDeleteCascadeAsync(id, ct)` | Removes the entity and its children |

Cascade methods load the navigation graph into memory first; avoid them on very large graphs.

## 5. Patterns for writes

**Update: load slim, mutate, save.** Load without includes, change it, call `UpdateAsync`. An entity loaded with `Include(...)` drags its whole graph into the update and can corrupt change tracking. If the response needs related data, reload with includes after saving.

```csharp
var order = await readRepository.GetAsync(o => o.Id == id, null, ct);
if (order is null) return new NotFoundMessage($"Order {id} not found.");
request.Apply(order);
if (!await cudRepository.UpdateAsync(order, ct))
    return new BadRequestMessage("Failed to update order.");
return await readRepository.GetAsync(o => o.Id == id, q => q.Include(o => o.Lines), ct) ?? order;
```

**Linking many-to-many.** `InsertAsync` does not walk navigation collections, so adding a child to a collection before inserting does not create the link. Both repositories share the same scoped DbContext, so:

```csharp
await tagRepository.InsertAsync(tag, ct);          // 1. insert the new entity
var product = await productReadRepository.GetAttachedAsync(
    p => p.Id == productId, q => q.Include(p => p.Tags), ct);   // 2. load the owner tracked
product!.Tags.Add(tag);                             // 3. add to the collection
await productCudRepository.UpdateAsync(product, ct);   // 4. save the link
```

**Delete.** `SoftDeleteAsync` returning false means not found:

```csharp
return await cudRepository.SoftDeleteAsync(id, ct)
    ? new Success()
    : new NotFoundMessage($"Order {id} not found.");
```

**Unique constraints.** A duplicate value on a unique index throws `DbUpdateException` from `InsertAsync` or `UpdateAsync`, which becomes a 500 through the global exception handler. When duplicates are an expected user error, check first and return a result:

```csharp
if (await readRepository.GetAsync(w => w.Name == request.Name, null, ct) is not null)
    return new InvalidOperationMessage($"A widget named '{request.Name}' already exists.");
```

Keep the unique index as the real guarantee (two requests can race past the check), and map what still escapes with `[assembly: ProblemMapping(409, ExceptionType = typeof(DbUpdateException), Title = "Conflict", IncludeMessage = false)]` (see `exception-handling.md`). Remember that soft-deleted rows still occupy the unique index, so a deleted name cannot be reused unless the index has a filter such as `.HasFilter("\"IsDeleted\" = false")` (PostgreSQL) or `"[IsDeleted] = 0"` (SQL Server).

**Querying deleted rows** (admin restore screens): the repositories always apply the filter. Use the DbContext directly with `IgnoreQueryFilters()` in that one handler.

## 6. Migration worker and seeding

```csharp
builder.Services.AddCoreDesignData<SystemUserAccessor>();
builder.AddMigrationWorker<MyAppDbContext>();                        // seeds from "SeedData"
// builder.AddMigrationWorker<MyAppDbContext>("ReferenceData");      // custom folder
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(MigrationWorker<MyAppDbContext>.ActivitySourceName));
```

The worker creates the database if needed, applies migrations (with the provider's retry strategy), seeds, and stops the host with exit code 0. Any failure exits non-zero, which stops a deployment pipeline. Outside Aspire, pass the connection string by environment variable (`ConnectionStrings__myapp-db=...`) and `dotnet run` the project before deploying the app.

Seed files: one JSON array per entity in `SeedData/`, named after the fully qualified type name (`MyApp.Api.Orders.Shared.Order.json`). Include fixed `Id` values. A row is inserted only if its `Id` does not exist (soft-deleted rows count as existing). Unknown file names are skipped with a warning; after renaming or moving an entity class, rename its seed file too (`dotnet seed diff` finds the mismatch).

Custom seeding (computed data, data from another source):

```csharp
public class AppMigrationWorker(IServiceProvider sp, IHostApplicationLifetime lifetime, ILogger<AppMigrationWorker> logger)
    : MigrationWorker<MyAppDbContext>(sp, lifetime, logger)
{
    protected override async Task SeedAsync(MyAppDbContext db, CancellationToken ct)
    {
        await SeedFromDirectoryAsync(db, "SeedData", typeof(MyAppDbContext).Assembly, ct);
        await SeedEntitiesAsync(db, BuildDefaultCategories(), ct);
    }
}

builder.Services.AddHostedService(sp => new AppMigrationWorker(
    sp, sp.GetRequiredService<IHostApplicationLifetime>(), sp.GetRequiredService<ILogger<AppMigrationWorker>>()));
```

Use the subclass instead of `AddMigrationWorker`, not in addition to it.

## 7. PurgeBeforeSeed

Seeding never updates existing rows. To replace a table's content from its seed file, list it in `PurgeBeforeSeed`, which deletes every row in that table first:

```csharp
builder.AddMigrationWorker<MyAppDbContext>(purgeBeforeSeed: ["SiteContent"]);
```

```json
{ "MigrationWorker": { "PurgeBeforeSeed": [ "SiteContent" ] } }
```

Short and fully qualified names both work; code and configuration lists are merged. **This is destructive and must never be configured for Production**, in code or in `appsettings.Production.json`. Use it in development or staging, then remove the entry. Production content changes are made by an administrator, or by a script the user reviews and runs.

## 8. Migrations

Create migrations only with the tooling:

```
dotnet ef migrations add <Name> --project src/MyApp.Api --startup-project src/MyApp.Api --output-dir Data/Migrations
```

Never write a migration by hand or edit a generated migration, its Designer file or the model snapshot. If the generated migration is wrong, fix the entity or configuration, `dotnet ef migrations remove`, and add it again. Data backfills belong in application code or a reviewed script, not inside a migration. If the command fails because the API is running under Aspire, stop the AppHost and rerun it. `dotnet ef migrations has-pending-model-changes` confirms whether the model and snapshot agree.
