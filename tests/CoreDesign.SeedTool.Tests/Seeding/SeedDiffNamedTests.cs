using System.Text.Json;
using CoreDesign.SeedTool.Tests.Helpers;

namespace CoreDesign.SeedTool.Tests.Seeding;

public class SeedDiffNamedTests
{
    [Fact]
    public async Task DiffAsync_Named_ReturnsEmpty_WhenFileAndDatabaseMatch()
    {
        var dbName = Guid.NewGuid().ToString();
        var dir    = CreateTempDir();
        var entity = new TestEntity { Name = "Match" };

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            ctx.Set<TestEntity>().Add(entity);
            await ctx.SaveChangesAsync();
        }

        await using var readCtx = DbContextFactory.CreateContext(dbName);
        var dbEntity = await readCtx.TestEntities.FindAsync(entity.Id);
        await WriteSeedFileAsync(dir, [dbEntity!]);

        try
        {
            var differ = new SeedDiff<TestDbContext>(readCtx);
            var results = await differ.DiffAsync(["TestEntity"], dir);

            Assert.True(results.ContainsKey("TestEntity"));
            Assert.Empty(results["TestEntity"]);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task DiffAsync_Named_ReportsDifferences()
    {
        var dbName = Guid.NewGuid().ToString();
        var dir    = CreateTempDir();
        var entity = new TestEntity { Name = "Original" };

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            ctx.Set<TestEntity>().Add(entity);
            await ctx.SaveChangesAsync();
        }

        var stale = new TestEntity { Id = entity.Id, Name = "Stale" };
        await WriteSeedFileAsync(dir, [stale]);

        try
        {
            await using var readCtx = DbContextFactory.CreateContext(dbName);
            var differ = new SeedDiff<TestDbContext>(readCtx);
            var results = await differ.DiffAsync(["TestEntity"], dir);

            Assert.True(results.ContainsKey("TestEntity"));
            Assert.Single(results["TestEntity"]);
            Assert.Equal(SeedDiffStatus.Different, results["TestEntity"][0].Status);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task DiffAsync_Named_SkipsMissingSeedFile()
    {
        var dir = CreateTempDir();
        try
        {
            await using var ctx = DbContextFactory.CreateContext();
            var differ = new SeedDiff<TestDbContext>(ctx);
            var results = await differ.DiffAsync(["TestEntity"], dir);

            Assert.DoesNotContain("TestEntity", results.Keys);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task DiffAsync_Named_SkipsUnknownEntityName()
    {
        var dir = CreateTempDir();
        try
        {
            await using var ctx = DbContextFactory.CreateContext();
            var differ = new SeedDiff<TestDbContext>(ctx);
            var results = await differ.DiffAsync(["DoesNotExist"], dir);

            Assert.Empty(results);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task DiffAsync_Named_ResolvesByFullyQualifiedName()
    {
        var dir = CreateTempDir();
        try
        {
            await using var ctx = DbContextFactory.CreateContext();
            ctx.Set<TestEntity>().Add(new TestEntity { Name = "FQN" });
            await ctx.SaveChangesAsync();

            await WriteSeedFileAsync(dir, []);

            var differ = new SeedDiff<TestDbContext>(ctx);
            var results = await differ.DiffAsync([typeof(TestEntity).FullName!], dir);

            Assert.True(results.ContainsKey("TestEntity"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task WriteSeedFileAsync(string dir, IEnumerable<TestEntity> entities)
    {
        var path = Path.Combine(dir, $"{typeof(TestEntity).FullName}.json");
        var json = JsonSerializer.Serialize(entities, new JsonSerializerOptions { PropertyNamingPolicy = null });
        await File.WriteAllTextAsync(path, json);
    }
}
