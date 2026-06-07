using System.Text.Json;
using CoreDesign.Data.Infrastructure;
using CoreDesign.SeedTool.Tests.Helpers;

namespace CoreDesign.SeedTool.Tests.Seeding;

public class SeedDiffTests
{
    private static async Task<string> WriteSeedFileAsync(IEnumerable<TestEntity> entities)
    {
        var path = Path.GetTempFileName();
        var json = JsonSerializer.Serialize(entities, new JsonSerializerOptions { PropertyNamingPolicy = null });
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    [Fact]
    public async Task DiffAsync_ReturnsEmpty_WhenFileAndDatabaseMatch()
    {
        var dbName = Guid.NewGuid().ToString();
        var entity = new TestEntity { Name = "Match" };

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            ctx.Set<TestEntity>().Add(entity);
            await ctx.SaveChangesAsync();
        }

        await using var readCtx = DbContextFactory.CreateContext(dbName);
        var dbEntity = await readCtx.TestEntities.FindAsync(entity.Id);
        var path = await WriteSeedFileAsync([dbEntity!]);

        try
        {
            var differ = new SeedDiff<TestDbContext>(readCtx);
            var result = await differ.DiffAsync<TestEntity>(path);
            Assert.Empty(result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DiffAsync_DetectsOnlyInFile_WhenEntityMissingFromDatabase()
    {
        await using var ctx = DbContextFactory.CreateContext();
        var missing = new TestEntity { Name = "OnlyInFile" };
        var path = await WriteSeedFileAsync([missing]);

        try
        {
            var differ = new SeedDiff<TestDbContext>(ctx);
            var result = await differ.DiffAsync<TestEntity>(path);

            Assert.Single(result);
            Assert.Equal(SeedDiffStatus.OnlyInFile, result[0].Status);
            Assert.Equal(missing.Id, result[0].Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DiffAsync_DetectsOnlyInDatabase_WhenEntityMissingFromFile()
    {
        var dbName = Guid.NewGuid().ToString();
        var entity = new TestEntity { Name = "OnlyInDb" };

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            ctx.Set<TestEntity>().Add(entity);
            await ctx.SaveChangesAsync();
        }

        var emptyPath = await WriteSeedFileAsync([]);

        try
        {
            await using var readCtx = DbContextFactory.CreateContext(dbName);
            var differ = new SeedDiff<TestDbContext>(readCtx);
            var result = await differ.DiffAsync<TestEntity>(emptyPath);

            Assert.Single(result);
            Assert.Equal(SeedDiffStatus.OnlyInDatabase, result[0].Status);
            Assert.Equal(entity.Id, result[0].Id);
        }
        finally
        {
            File.Delete(emptyPath);
        }
    }

    [Fact]
    public async Task DiffAsync_DetectsDifferent_WhenFieldsChanged()
    {
        var dbName = Guid.NewGuid().ToString();
        var entity = new TestEntity { Name = "Original" };

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            ctx.Set<TestEntity>().Add(entity);
            await ctx.SaveChangesAsync();
        }

        var staleVersion = new TestEntity { Id = entity.Id, Name = "Stale" };
        var path = await WriteSeedFileAsync([staleVersion]);

        try
        {
            await using var readCtx = DbContextFactory.CreateContext(dbName);
            var differ = new SeedDiff<TestDbContext>(readCtx);
            var result = await differ.DiffAsync<TestEntity>(path);

            Assert.Single(result);
            Assert.Equal(SeedDiffStatus.Different, result[0].Status);
            Assert.Equal(entity.Id, result[0].Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DiffAsync_ThrowsFileNotFoundException_WhenSeedFileMissing()
    {
        await using var ctx = DbContextFactory.CreateContext();
        var differ = new SeedDiff<TestDbContext>(ctx);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            differ.DiffAsync<TestEntity>("/nonexistent/path.json"));
    }
}
