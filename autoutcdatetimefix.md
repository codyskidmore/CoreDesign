# Auto UTC DateTime Convention for CoreDesign.Data

## Problem

Npgsql (the .NET PostgreSQL driver) requires that any `DateTime` written to a `timestamp with time zone` column have `Kind=Utc`. If `Kind=Local` or `Kind=Unspecified` is supplied, Npgsql throws:

```
System.ArgumentException: Cannot write DateTime with Kind=Local to PostgreSQL type
'timestamp with time zone', only UTC is supported.
```

Apps built on CoreDesign.Data that receive dates from HTTP request bodies (JSON deserialization) have not encountered this because System.Text.Json defaults to `Kind=Utc`. However, any app that parses dates from non-JSON sources (XML, CSV, flat files, etc.) will hit this error.

## Fix

Add an EF Core model configuration convention to CoreDesign.Data that automatically applies a UTC value converter to every `DateTime` and `DateTime?` property across all entities. This makes the behavior uniform and removes the burden from individual apps.

## Implementation

### 1. Add the value converter

File: `CoreDesign.Data/Converters/UtcDateTimeConverter.cs`

```csharp
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CoreDesign.Data.Converters;

public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    { }
}
```

Write direction (app to DB): if the value is already UTC, pass it through; otherwise convert to UTC via `ToUniversalTime()`.
Read direction (DB to app): stamp the value with `Kind=Utc` since PostgreSQL always returns timestamps in UTC.

### 2. Register the convention in the base DbContext

CoreDesign.Data should expose a base `DbContext` (or a shared `ConfigureConventions` helper) that apps derive from. Add the convention override there.

File: `CoreDesign.Data/Infrastructure/CoreDesignDbContext.cs` (or wherever the base context lives)

```csharp
using CoreDesign.Data.Converters;
using Microsoft.EntityFrameworkCore;

namespace CoreDesign.Data.Infrastructure;

public abstract class CoreDesignDbContext(DbContextOptions options) : DbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder
            .Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();

        configurationBuilder
            .Properties<DateTime?>()
            .HaveConversion<UtcNullableDateTimeConverter>();
    }
}
```

### 3. Add the nullable variant

File: `CoreDesign.Data/Converters/UtcNullableDateTimeConverter.cs`

```csharp
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CoreDesign.Data.Converters;

public class UtcNullableDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public UtcNullableDateTimeConverter() : base(
        v => v.HasValue
            ? v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()
            : v,
        v => v.HasValue
            ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
            : v)
    { }
}
```

### 4. Bump the package version

Increment `CoreDesign.Data` to the next minor version (e.g., `1.1.1` to `1.2.0`) since this is a behavioral change, and publish to the artifacts feed.

## Migration note

Existing data is unaffected. PostgreSQL stores `timestamp with time zone` values internally as UTC regardless of what offset was supplied at insert time. The convention only changes how .NET reads and writes the values at the driver boundary.

## Unit Test Cases

### Test class setup

```csharp
// Uses an in-memory SQLite provider or a real Npgsql test database.
// For full coverage of the Npgsql UTC restriction, use a real PostgreSQL instance.
```

### UtcDateTimeConverter tests

```csharp
public class UtcDateTimeConverterTests
{
    private readonly UtcDateTimeConverter _converter = new();

    [Fact]
    public void WriteDirection_UtcKind_PassesThrough()
    {
        var input = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        var result = _converter.ConvertToProvider(input);
        Assert.Equal(input, result);
        Assert.Equal(DateTimeKind.Utc, ((DateTime)result!).Kind);
    }

    [Fact]
    public void WriteDirection_LocalKind_ConvertsToUtc()
    {
        var input = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Local);
        var result = (DateTime)_converter.ConvertToProvider(input)!;
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(input.ToUniversalTime(), result);
    }

    [Fact]
    public void WriteDirection_UnspecifiedKind_ConvertsToUtc()
    {
        var input = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Unspecified);
        var result = (DateTime)_converter.ConvertToProvider(input)!;
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void ReadDirection_StampsUtcKind()
    {
        var input = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Unspecified);
        var result = (DateTime)_converter.ConvertFromProvider(input)!;
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(input.Ticks, result.Ticks);
    }

    [Fact]
    public void ReadDirection_AlreadyUtc_Unchanged()
    {
        var input = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        var result = (DateTime)_converter.ConvertFromProvider(input)!;
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(input, result);
    }
}
```

### UtcNullableDateTimeConverter tests

```csharp
public class UtcNullableDateTimeConverterTests
{
    private readonly UtcNullableDateTimeConverter _converter = new();

    [Fact]
    public void WriteDirection_Null_ReturnsNull()
    {
        var result = _converter.ConvertToProvider(null);
        Assert.Null(result);
    }

    [Fact]
    public void WriteDirection_LocalKind_ConvertsToUtc()
    {
        DateTime? input = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Local);
        var result = (DateTime?)_converter.ConvertToProvider(input);
        Assert.NotNull(result);
        Assert.Equal(DateTimeKind.Utc, result!.Value.Kind);
    }

    [Fact]
    public void ReadDirection_Null_ReturnsNull()
    {
        var result = _converter.ConvertFromProvider(null);
        Assert.Null(result);
    }

    [Fact]
    public void ReadDirection_StampsUtcKind()
    {
        DateTime? input = new DateTime(2026, 6, 30, 8, 0, 0, DateTimeKind.Unspecified);
        var result = (DateTime?)_converter.ConvertFromProvider(input);
        Assert.Equal(DateTimeKind.Utc, result!.Value.Kind);
    }
}
```

### Integration test against real PostgreSQL

```csharp
// Requires a live PostgreSQL instance. Use a test container (Testcontainers.PostgreSql).
public class UtcDateTimeIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;
    private TestDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder().Build();
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _db = new TestDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Save_LocalDateTime_DoesNotThrow()
    {
        var entity = new SampleEntity
        {
            CreatedAt = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Local)
        };
        _db.Samples.Add(entity);
        // Before the fix this throws; after the fix it should succeed.
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Save_UnspecifiedDateTime_DoesNotThrow()
    {
        var entity = new SampleEntity
        {
            CreatedAt = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Unspecified)
        };
        _db.Samples.Add(entity);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Roundtrip_LocalDateTime_ReturnsUtc()
    {
        var localInput = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Local);
        var entity = new SampleEntity { CreatedAt = localInput };
        _db.Samples.Add(entity);
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        var loaded = await _db.Samples.FindAsync(entity.Id);

        Assert.Equal(DateTimeKind.Utc, loaded!.CreatedAt.Kind);
        Assert.Equal(localInput.ToUniversalTime(), loaded.CreatedAt);
    }

    [Fact]
    public async Task Roundtrip_UtcDateTime_Unchanged()
    {
        var utcInput = new DateTime(2026, 6, 30, 16, 0, 0, DateTimeKind.Utc);
        var entity = new SampleEntity { CreatedAt = utcInput };
        _db.Samples.Add(entity);
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        var loaded = await _db.Samples.FindAsync(entity.Id);

        Assert.Equal(DateTimeKind.Utc, loaded!.CreatedAt.Kind);
        Assert.Equal(utcInput, loaded.CreatedAt);
    }
}
```

## Applying the fix in a later session

1. Open `D:\repos\CoreDesign` and locate the `CoreDesign.Data` project.
2. Add `UtcDateTimeConverter.cs` and `UtcNullableDateTimeConverter.cs` under `Converters/`.
3. Find the base `DbContext` class in `CoreDesign.Data` and override `ConfigureConventions` as shown above. If no base context exists, add one and have all CoreDesign.Data-consuming DbContexts derive from it.
4. Add the unit test classes under the existing `CoreDesign.Data.Tests` project (or create one).
5. Add the integration test class; it requires the `Testcontainers.PostgreSql` NuGet package.
6. Run all tests and confirm the integration tests that previously would have thrown now pass.
7. Bump the `CoreDesign.Data` version and publish.
8. Update consuming projects (starting with `PiTagXmlProcessor`) to the new version and remove the manual `DateTime.SpecifyKind` call in `XmlImportWorker.cs` since CoreDesign.Data will handle it automatically.
