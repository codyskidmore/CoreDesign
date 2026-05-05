using Bogus;
using CoreDesign.Data.Infrastructure;
using CoreDesign.Data.Repositories;
using CoreDesign.Data.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CoreDesign.Data.Tests.Repositories;

public class ReadRepositoryTests
{
    private static TestEntity MakeEntity(string name = "Test") =>
        new() { Name = name };

    private static async Task<string> SeedAsync(params TestEntity[] entities)
    {
        var dbName = Guid.NewGuid().ToString();
        var opts = DbContextFactory.CreateOptions(dbName);
        await using var ctx = new TestDbContext(opts);
        var userId = Guid.NewGuid();
        foreach (var e in entities)
            e.InitializeAuditFields(userId);
        ctx.TestEntities.AddRange(entities);
        await ctx.SaveChangesAsync();
        return dbName;
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllNonDeletedEntities()
    {
        var dbName = await SeedAsync(MakeEntity("A"), MakeEntity("B"), MakeEntity("C"));
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var repo = new ReadRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.GetAllAsync(whereExpression: null);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_ExcludesSoftDeletedEntities()
    {
        var entity = MakeEntity("ToDelete");
        var dbName = await SeedAsync(MakeEntity("Keep"), entity);

        // Soft-delete via raw EF (bypass query filter using IgnoreQueryFilters)
        await using var deleteCtx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var toDelete = await deleteCtx.TestEntities.IgnoreQueryFilters()
            .FirstAsync(e => e.Name == "ToDelete");
        toDelete.IsDeleted = true;
        await deleteCtx.SaveChangesAsync();

        await using var readCtx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var repo = new ReadRepository<TestDbContext, TestEntity>(readCtx);

        var result = await repo.GetAllAsync(whereExpression: null);

        Assert.Single(result);
        Assert.Equal("Keep", result[0].Name);
    }

    [Fact]
    public async Task GetAllAsync_WithFilter_ReturnsMatchingEntities()
    {
        var dbName = await SeedAsync(MakeEntity("Alpha"), MakeEntity("Beta"), MakeEntity("Alpha2"));
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var repo = new ReadRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.GetAllAsync(e => e.Name.StartsWith("Alpha"));

        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.StartsWith("Alpha", e.Name));
    }

    [Fact]
    public async Task GetAllAsync_WithOrderBy_ReturnsOrderedEntities()
    {
        var dbName = await SeedAsync(MakeEntity("Charlie"), MakeEntity("Alice"), MakeEntity("Bob"));
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var repo = new ReadRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.GetAllAsync(orderBy: q => q.OrderBy(e => e.Name));

        Assert.Equal("Alice", result[0].Name);
        Assert.Equal("Bob", result[1].Name);
        Assert.Equal("Charlie", result[2].Name);
    }

    [Fact]
    public async Task GetAllAsync_Empty_ReturnsEmptyList()
    {
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions());
        var repo = new ReadRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.GetAllAsync(whereExpression: null);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_NoParams_ReturnsAllNonDeleted()
    {
        var dbName = await SeedAsync(MakeEntity("X"), MakeEntity("Y"));
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var repo = new ReadRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.GetAllAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAttachedAsync_ReturnsNonDeletedEntities()
    {
        var dbName = await SeedAsync(MakeEntity("Attached"));
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var repo = new ReadRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.GetAllAttachedAsync();

        Assert.Single(result);
    }

    [Fact]
    public async Task GetAsync_WithMatchingExpression_ReturnsEntity()
    {
        var entity = MakeEntity("FindMe");
        var dbName = await SeedAsync(entity, MakeEntity("Other"));
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var repo = new ReadRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.GetAsync(e => e.Name == "FindMe");

        Assert.NotNull(result);
        Assert.Equal("FindMe", result.Name);
    }

    [Fact]
    public async Task GetAsync_WithNoMatch_ReturnsNull()
    {
        var dbName = await SeedAsync(MakeEntity("Existing"));
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var repo = new ReadRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.GetAsync(e => e.Name == "DoesNotExist");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAttachedAsync_WithMatchingExpression_ReturnsTrackedEntity()
    {
        var dbName = await SeedAsync(MakeEntity("Tracked"));
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var repo = new ReadRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.GetAttachedAsync(e => e.Name == "Tracked");

        Assert.NotNull(result);
        Assert.Equal("Tracked", result.Name);
    }

    [Fact]
    public async Task GetAttachedAsync_WithNoMatch_ReturnsNull()
    {
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions());
        var repo = new ReadRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.GetAttachedAsync(e => e.Name == "Missing");

        Assert.Null(result);
    }
}
