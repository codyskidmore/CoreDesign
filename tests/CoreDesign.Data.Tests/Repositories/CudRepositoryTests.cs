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
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions());
        var repo = new CudRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.InsertAsync(MakeEntity(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task InsertAsync_InitializesAuditFields()
    {
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions());
        var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
        var entity = MakeEntity("Audited");
        var userId = Guid.NewGuid();

        await repo.InsertAsync(entity, userId, CancellationToken.None);

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

        await using (var ctx = new TestDbContext(DbContextFactory.CreateOptions(dbName)))
        {
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            await repo.InsertAsync(entity, Guid.NewGuid(), CancellationToken.None);
        }

        await using var readCtx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var found = await readCtx.TestEntities.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Name == "Persisted");
        Assert.NotNull(found);
    }

    [Fact]
    public async Task InsertRangeAsync_ReturnsTrue_WhenAllSaved()
    {
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions());
        var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
        var entities = new[] { MakeEntity("One"), MakeEntity("Two"), MakeEntity("Three") };

        var result = await repo.InsertRangeAsync(entities, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task InsertRangeAsync_InitializesAuditFieldsForAll()
    {
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions());
        var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
        var entities = new[] { MakeEntity("A"), MakeEntity("B") };
        var userId = Guid.NewGuid();

        await repo.InsertRangeAsync(entities, userId, CancellationToken.None);

        Assert.All(entities, e =>
        {
            Assert.NotEqual(default, e.Id);
            Assert.Equal(userId, e.CreatedBy);
        });
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesEntity()
    {
        var dbName = Guid.NewGuid().ToString();
        var entity = MakeEntity("ToDelete");
        var userId = Guid.NewGuid();

        await using (var ctx = new TestDbContext(DbContextFactory.CreateOptions(dbName)))
        {
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            await repo.InsertAsync(entity, userId, CancellationToken.None);
        }

        await using (var ctx = new TestDbContext(DbContextFactory.CreateOptions(dbName)))
        {
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            var result = await repo.DeleteAsync(entity.Id, userId, CancellationToken.None);
            Assert.True(result);
        }

        await using var readCtx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var deleted = await readCtx.TestEntities.IgnoreQueryFilters().FirstAsync(e => e.Id == entity.Id);
        Assert.True(deleted.IsDeleted);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenEntityNotFound()
    {
        await using var ctx = new TestDbContext(DbContextFactory.CreateOptions());
        var repo = new CudRepository<TestDbContext, TestEntity>(ctx);

        var result = await repo.DeleteAsync(Ulid.NewUlid(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteRangeAsync_SoftDeletesAllEntities()
    {
        var dbName = Guid.NewGuid().ToString();
        var entities = new[] { MakeEntity("Del1"), MakeEntity("Del2") };
        var userId = Guid.NewGuid();

        await using (var ctx = new TestDbContext(DbContextFactory.CreateOptions(dbName)))
        {
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            await repo.InsertRangeAsync(entities, userId, CancellationToken.None);
        }

        await using (var ctx = new TestDbContext(DbContextFactory.CreateOptions(dbName)))
        {
            // Load attached entities for the delete range operation
            var attached = await ctx.TestEntities.IgnoreQueryFilters().ToListAsync();
            var repo = new CudRepository<TestDbContext, TestEntity>(ctx);
            var result = await repo.DeleteRangeAsync(attached, userId, CancellationToken.None);
            Assert.True(result);
        }

        await using var readCtx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var remaining = await readCtx.TestEntities.IgnoreQueryFilters().ToListAsync();
        Assert.All(remaining, e => Assert.True(e.IsDeleted));
    }

    [Fact]
    public async Task UpdateAsync_ReturnsTrue_WhenSuccessful()
    {
        var dbName = Guid.NewGuid().ToString();
        var entity = MakeEntity("Original");
        var userId = Guid.NewGuid();

        await using (var insertCtx = new TestDbContext(DbContextFactory.CreateOptions(dbName)))
        {
            var cudRepo = new CudRepository<TestDbContext, TestEntity>(insertCtx);
            await cudRepo.InsertAsync(entity, userId, CancellationToken.None);
        }

        await using var updateCtx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var updateRepo = new CudRepository<TestDbContext, TestEntity>(updateCtx);
        entity.Name = "Updated";

        var result = await updateRepo.UpdateAsync(entity, userId, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesAuditFields()
    {
        var dbName = Guid.NewGuid().ToString();
        var entity = MakeEntity("ForUpdate");
        var originalUserId = Guid.NewGuid();
        var updatingUserId = Guid.NewGuid();

        await using (var insertCtx = new TestDbContext(DbContextFactory.CreateOptions(dbName)))
        {
            var cudRepo = new CudRepository<TestDbContext, TestEntity>(insertCtx);
            await cudRepo.InsertAsync(entity, originalUserId, CancellationToken.None);
        }

        await using var updateCtx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        var updateRepo = new CudRepository<TestDbContext, TestEntity>(updateCtx);
        var before = DateTime.UtcNow;
        await updateRepo.UpdateAsync(entity, updatingUserId, CancellationToken.None);

        Assert.Equal(updatingUserId, entity.UpdatedBy);
        Assert.True(entity.UpdatedAt >= before);
    }

    [Fact]
    public async Task UpdateRangeAsync_UpdatesAllEntities()
    {
        var dbName = Guid.NewGuid().ToString();
        var entities = new[] { MakeEntity("Upd1"), MakeEntity("Upd2") };
        var userId = Guid.NewGuid();
        var updaterId = Guid.NewGuid();

        await using (var insertCtx = new TestDbContext(DbContextFactory.CreateOptions(dbName)))
        {
            var cudRepo = new CudRepository<TestDbContext, TestEntity>(insertCtx);
            await cudRepo.InsertRangeAsync(entities, userId, CancellationToken.None);
        }

        await using var updateCtx = new TestDbContext(DbContextFactory.CreateOptions(dbName));
        foreach (var e in entities)
            e.Name += "-modified";
        var updateRepo = new CudRepository<TestDbContext, TestEntity>(updateCtx);

        var result = await updateRepo.UpdateRangeAsync(entities, updaterId, CancellationToken.None);

        Assert.True(result);
        Assert.All(entities, e => Assert.Equal(updaterId, e.UpdatedBy));
    }
}
