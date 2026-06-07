using CoreDesign.Data.Infrastructure;
using CoreDesign.Data.Repositories;
using CoreDesign.Data.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CoreDesign.Data.Tests.Repositories;

public class CudRepositoryTests
{
    private static TestEntity MakeEntity(string name = "Test") => new() { Name = name };

    [Fact]
    public async Task InsertAsync_ReturnsTrue_WhenSuccessful()
    {
        await using var ctx = DbContextFactory.CreateContext();
        var repo = new CudRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.InsertAsync(MakeEntity(), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task InsertAsync_InitializesAuditFields()
    {
        var userId = Guid.NewGuid();
        await using var ctx = DbContextFactory.CreateContext(userId: userId);
        var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
        var entity = MakeEntity("Audited");

        await repo.InsertAsync(entity, CancellationToken.None);

        Assert.NotEqual(default, entity.Id);
        Assert.Equal(userId, entity.CreatedBy);
        Assert.Equal(userId, entity.UpdatedBy);
        Assert.NotEqual(default, entity.CreatedAt);
    }

    [Fact]
    public async Task InsertAsync_PersistsEntity()
    {
        var dbName = Guid.NewGuid().ToString();
        var entity = MakeEntity("Persisted");

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            await repo.InsertAsync(entity, CancellationToken.None);
        }

        await using var readCtx = DbContextFactory.CreateContext(dbName);
        var found = await readCtx.TestEntities.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Name == "Persisted");
        Assert.NotNull(found);
    }

    [Fact]
    public async Task InsertRangeAsync_ReturnsTrue_WhenAllSaved()
    {
        await using var ctx = DbContextFactory.CreateContext();
        var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
        var entities = new[] { MakeEntity("One"), MakeEntity("Two"), MakeEntity("Three") };

        var result = await repo.InsertRangeAsync(entities, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task InsertRangeAsync_InitializesAuditFieldsForAll()
    {
        var userId = Guid.NewGuid();
        await using var ctx = DbContextFactory.CreateContext(userId: userId);
        var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
        var entities = new[] { MakeEntity("A"), MakeEntity("B") };

        await repo.InsertRangeAsync(entities, CancellationToken.None);

        Assert.All(entities, e =>
        {
            Assert.NotEqual(default, e.Id);
            Assert.Equal(userId, e.CreatedBy);
        });
    }

    [Fact]
    public async Task SoftDeleteAsync_SoftDeletesEntity()
    {
        var dbName = Guid.NewGuid().ToString();
        var entity = MakeEntity("ToDelete");

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            await repo.InsertAsync(entity, CancellationToken.None);
        }

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            var result = await repo.SoftDeleteAsync(entity.Id, CancellationToken.None);
            Assert.True(result);
        }

        await using var readCtx = DbContextFactory.CreateContext(dbName);
        var deleted = await readCtx.TestEntities.IgnoreQueryFilters().FirstAsync(e => e.Id == entity.Id);
        Assert.True(deleted.IsDeleted);
    }

    [Fact]
    public async Task SoftDeleteAsync_ReturnsFalse_WhenEntityNotFound()
    {
        await using var ctx = DbContextFactory.CreateContext();
        var repo = new CudRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.SoftDeleteAsync(Ulid.NewUlid(), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task SoftDeleteRangeAsync_SoftDeletesAllEntities()
    {
        var dbName = Guid.NewGuid().ToString();
        var entities = new[] { MakeEntity("Del1"), MakeEntity("Del2") };

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            await repo.InsertRangeAsync(entities, CancellationToken.None);
        }

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            var attached = await ctx.TestEntities.IgnoreQueryFilters().ToListAsync();
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            var result = await repo.SoftDeleteRangeAsync(attached, CancellationToken.None);
            Assert.True(result);
        }

        await using var readCtx = DbContextFactory.CreateContext(dbName);
        var remaining = await readCtx.TestEntities.IgnoreQueryFilters().ToListAsync();
        Assert.All(remaining, e => Assert.True(e.IsDeleted));
    }

    [Fact]
    public async Task UpdateAsync_ReturnsTrue_WhenSuccessful()
    {
        var dbName = Guid.NewGuid().ToString();
        var entity = MakeEntity("Original");

        await using (var insertCtx = DbContextFactory.CreateContext(dbName))
        {
            var cudRepo = new CudRepository<TestDbContext, TestEntity>(insertCtx);
            await cudRepo.InsertAsync(entity, CancellationToken.None);
        }

        await using var updateCtx = DbContextFactory.CreateContext(dbName);
        var updateRepo = new CudRepository<TestDbContext, TestEntity>(updateCtx);
        entity.Name = "Updated";

        var result = await updateRepo.UpdateAsync(entity, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesAuditFields()
    {
        var dbName = Guid.NewGuid().ToString();
        var entity = MakeEntity("ForUpdate");
        var updatingUserId = Guid.NewGuid();

        await using (var insertCtx = DbContextFactory.CreateContext(dbName))
        {
            var cudRepo = new CudRepository<TestDbContext, TestEntity>(insertCtx);
            await cudRepo.InsertAsync(entity, CancellationToken.None);
        }

        await using var updateCtx = DbContextFactory.CreateContext(dbName, userId: updatingUserId);
        var updateRepo = new CudRepository<TestDbContext, TestEntity>(updateCtx);
        var before = DateTime.UtcNow;
        await updateRepo.UpdateAsync(entity, CancellationToken.None);

        Assert.Equal(updatingUserId, entity.UpdatedBy);
        Assert.True(entity.UpdatedAt >= before);
    }

    [Fact]
    public async Task HardDeleteAsync_RemovesEntity_WhenExists()
    {
        var dbName = Guid.NewGuid().ToString();
        var entity = MakeEntity("HardDelete");

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            await repo.InsertAsync(entity, CancellationToken.None);
        }

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            var result = await repo.HardDeleteAsync(entity.Id, CancellationToken.None);
            Assert.True(result);
        }

        await using var readCtx = DbContextFactory.CreateContext(dbName);
        var found = await readCtx.TestEntities.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == entity.Id);
        Assert.Null(found);
    }

    [Fact]
    public async Task HardDeleteAsync_ReturnsFalse_WhenEntityNotFound()
    {
        await using var ctx = DbContextFactory.CreateContext();
        var repo = new CudRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.HardDeleteAsync(Ulid.NewUlid(), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task HardDeleteAsync_CanDelete_SoftDeletedEntity()
    {
        var dbName = Guid.NewGuid().ToString();
        var entity = MakeEntity("SoftThenHard");

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            await repo.InsertAsync(entity, CancellationToken.None);
            await repo.SoftDeleteAsync(entity.Id, CancellationToken.None);
        }

        await using (var ctx = DbContextFactory.CreateContext(dbName))
        {
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            var result = await repo.HardDeleteAsync(entity.Id, CancellationToken.None);
            Assert.True(result);
        }

        await using var readCtx = DbContextFactory.CreateContext(dbName);
        var found = await readCtx.TestEntities.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == entity.Id);
        Assert.Null(found);
    }

    [Fact]
    public async Task UpdateRangeAsync_UpdatesAllEntities()
    {
        var dbName = Guid.NewGuid().ToString();
        var entities = new[] { MakeEntity("Upd1"), MakeEntity("Upd2") };
        var updaterId = Guid.NewGuid();

        await using (var insertCtx = DbContextFactory.CreateContext(dbName))
        {
            var cudRepo = new CudRepository<TestDbContext, TestEntity>(insertCtx);
            await cudRepo.InsertRangeAsync(entities, CancellationToken.None);
        }

        await using var updateCtx = DbContextFactory.CreateContext(dbName, userId: updaterId);
        foreach (var e in entities)
            e.Name += "-modified";
        var updateRepo = new CudRepository<TestDbContext, TestEntity>(updateCtx);

        var result = await updateRepo.UpdateRangeAsync(entities, CancellationToken.None);

        Assert.True(result);
        Assert.All(entities, e => Assert.Equal(updaterId, e.UpdatedBy));
    }
}
