# CoreDesign.Data

A generic, reusable Entity Framework Core data access layer providing base entity infrastructure and repository abstractions for .NET projects.

## Requirements

- .NET 10.0
- Microsoft.EntityFrameworkCore 10.x
- Microsoft.EntityFrameworkCore.SqlServer 10.x
- Ulid 1.4.x

## What Is Included

**Infrastructure**

- `BaseEntity` - Base class all entities must inherit from. Provides `Id` (Ulid), `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, and `IsDeleted` audit fields.
- `BaseEntityConfiguration<T>` - EF Core `IEntityTypeConfiguration<T>` base that wires up primary key, index, soft-delete query filter, and required audit field constraints.
- `BaseEntityExtensionMethods` - Extension methods `InitializeAuditFields` and `UpdateAuditFields` for setting audit fields on insert and update.
- `ValueConverters` - Provides `GetUlidConverter()` (Ulid to string) and `GetEnumConverter<TEnum>()` (enum to string) for use in entity configurations.

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

Your `DbContext` must inherit from EF Core's `DbContext`. Apply entity configurations in `OnModelCreating`:

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

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
