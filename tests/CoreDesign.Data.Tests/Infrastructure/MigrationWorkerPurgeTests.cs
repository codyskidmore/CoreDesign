using CoreDesign.Data.Infrastructure;
using CoreDesign.Data.Repositories;
using CoreDesign.Data.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CoreDesign.Data.Tests.Infrastructure;

public class MigrationWorkerPurgeTests
{
    private static TestMigrationWorker CreateWorker(
        string seedDirectory = "SeedData",
        ISet<string>? purgeBeforeSeed = null)
    {
        var sp = Mock.Of<IServiceProvider>();
        var lifetime = Mock.Of<IHostApplicationLifetime>();
        return new TestMigrationWorker(sp, lifetime, seedDirectory, purgeBeforeSeed);
    }

    [Fact]
    public async Task PurgeEntitiesAsync_RemovesAllRows_IncludingSoftDeleted()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            await repo.InsertAsync(new TestEntity { Name = "Active" }, CancellationToken.None);
            var soft = new TestEntity { Name = "SoftDeleted" };
            await repo.InsertAsync(soft, CancellationToken.None);
            await repo.SoftDeleteAsync(soft.Id, CancellationToken.None);
        }

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            var worker = CreateWorker();
            await worker.ExposedPurgeAsync<TestEntity>(ctx, CancellationToken.None);
        }

        await using var read = DbContextFactory.CreateContext(dbName);
        var remaining = await read.TestEntities.IgnoreQueryFilters().ToListAsync();
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task PurgeEntitiesAsync_IsIdempotent_WhenTableAlreadyEmpty()
    {
        await using var ctx = DbContextFactory.CreateContext();
        var worker = CreateWorker();

        var exception = await Record.ExceptionAsync(() =>
            worker.ExposedPurgeAsync<TestEntity>(ctx, CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SeedFromDirectoryAsync_PurgesBeforeSeed_WhenFullyQualifiedNameMatches()
    {
        var dbName   = Guid.NewGuid().ToString();
        var seedDir  = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(seedDir);

        try
        {
            var existingId = Ulid.NewUlid();
            var seededId   = Ulid.NewUlid();

            await using (var ctx = DbContextFactory.CreateContext(dbName))
            {
                ctx.Set<TestEntity>().Add(new TestEntity { Id = existingId, Name = "ExistingRow" });
                await ctx.SaveChangesAsync();
            }

            await WriteSeedFile(seedDir, seededId, "SeededRow");

            await using (var ctx = DbContextFactory.CreateContext(dbName))
            {
                var worker = CreateWorker(seedDir,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        { typeof(TestEntity).FullName! });
                await worker.ExposedSeedFromDirectoryAsync(ctx, seedDir, typeof(TestEntity).Assembly, CancellationToken.None);
            }

            await using var read = DbContextFactory.CreateContext(dbName);
            var all = await read.TestEntities.IgnoreQueryFilters().ToListAsync();

            Assert.Single(all);
            Assert.Equal("SeededRow", all[0].Name);
        }
        finally { Directory.Delete(seedDir, recursive: true); }
    }

    [Fact]
    public async Task SeedFromDirectoryAsync_PurgesBeforeSeed_WhenShortNameMatches()
    {
        var dbName  = Guid.NewGuid().ToString();
        var seedDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(seedDir);

        try
        {
            var existingId = Ulid.NewUlid();
            var seededId   = Ulid.NewUlid();

            await using (var ctx = DbContextFactory.CreateContext(dbName))
            {
                ctx.Set<TestEntity>().Add(new TestEntity { Id = existingId, Name = "ExistingRow" });
                await ctx.SaveChangesAsync();
            }

            await WriteSeedFile(seedDir, seededId, "SeededRow");

            await using (var ctx = DbContextFactory.CreateContext(dbName))
            {
                // Short name only — "TestEntity" not the fully qualified name
                var worker = CreateWorker(seedDir,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TestEntity" });
                await worker.ExposedSeedFromDirectoryAsync(ctx, seedDir, typeof(TestEntity).Assembly, CancellationToken.None);
            }

            await using var read = DbContextFactory.CreateContext(dbName);
            var all = await read.TestEntities.IgnoreQueryFilters().ToListAsync();

            Assert.Single(all);
            Assert.Equal("SeededRow", all[0].Name);
        }
        finally { Directory.Delete(seedDir, recursive: true); }
    }

    [Fact]
    public async Task SeedFromDirectoryAsync_DoesNotPurge_WhenTypeNotRegistered()
    {
        var dbName  = Guid.NewGuid().ToString();
        var seedDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(seedDir);

        try
        {
            var existingId = Ulid.NewUlid();
            var seededId   = Ulid.NewUlid();

            await using (var ctx = DbContextFactory.CreateContext(dbName))
            {
                ctx.Set<TestEntity>().Add(new TestEntity { Id = existingId, Name = "ExistingRow" });
                await ctx.SaveChangesAsync();
            }

            await WriteSeedFile(seedDir, seededId, "SeededRow");

            await using (var ctx = DbContextFactory.CreateContext(dbName))
            {
                var worker = CreateWorker(seedDir); // no purge list
                await worker.ExposedSeedFromDirectoryAsync(ctx, seedDir, typeof(TestEntity).Assembly, CancellationToken.None);
            }

            await using var read = DbContextFactory.CreateContext(dbName);
            var all = await read.TestEntities.IgnoreQueryFilters().ToListAsync();

            Assert.Equal(2, all.Count);
            Assert.Contains(all, e => e.Id == existingId);
            Assert.Contains(all, e => e.Id == seededId);
        }
        finally { Directory.Delete(seedDir, recursive: true); }
    }

    private static async Task WriteSeedFile(string seedDir, Ulid id, string name)
    {
        var json = $$"""
            [
              {
                "Id": "{{id}}",
                "Name": "{{name}}",
                "CreatedAt": "2026-01-01T00:00:00Z",
                "UpdatedAt": "2026-01-01T00:00:00Z",
                "CreatedBy": "00000000-0000-0000-0000-000000000000",
                "UpdatedBy": "00000000-0000-0000-0000-000000000000",
                "IsDeleted": false
              }
            ]
            """;
        var seedFile = Path.Combine(seedDir, $"{typeof(TestEntity).FullName}.json");
        await File.WriteAllTextAsync(seedFile, json);
    }
}

/// <summary>Exposes protected MigrationWorker methods for testing.</summary>
public class TestMigrationWorker(
    IServiceProvider sp,
    IHostApplicationLifetime lifetime,
    string seedDirectory = "SeedData",
    ISet<string>? purgeBeforeSeed = null)
    : MigrationWorker<TestDbContext>(sp, lifetime,
        NullLogger<MigrationWorker<TestDbContext>>.Instance,
        seedDirectory, purgeBeforeSeed)
{
    public Task ExposedPurgeAsync<T>(TestDbContext ctx, CancellationToken ct) where T : BaseEntity
        => PurgeEntitiesAsync<T>(ctx, ct);

    public Task ExposedSeedFromDirectoryAsync(
        TestDbContext ctx, string directory, System.Reflection.Assembly assembly, CancellationToken ct)
        => SeedFromDirectoryAsync(ctx, directory, assembly, ct);
}
