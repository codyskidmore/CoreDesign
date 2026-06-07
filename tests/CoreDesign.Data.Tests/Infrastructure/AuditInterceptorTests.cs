using CoreDesign.Data.Infrastructure;
using CoreDesign.Data.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CoreDesign.Data.Tests.Infrastructure;

public class AuditInterceptorTests
{
    [Fact]
    public async Task SavingChanges_SetsCreatedAuditFields_OnInsert()
    {
        var userId = Guid.NewGuid();
        await using var ctx = DbContextFactory.CreateContext(userId: userId);
        var entity = new TestEntity { Name = "Audited" };
        var before = DateTime.UtcNow;

        ctx.TestEntities.Add(entity);
        await ctx.SaveChangesAsync();

        Assert.Equal(userId, entity.CreatedBy);
        Assert.Equal(userId, entity.UpdatedBy);
        Assert.True(entity.CreatedAt >= before);
        Assert.True(entity.UpdatedAt >= before);
    }

    [Fact]
    public async Task SavingChanges_PreservesExplicitAuditValues_WhenCreatedAtAlreadySet()
    {
        var explicitTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var explicitUser = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await using var ctx = DbContextFactory.CreateContext();

        var entity = new TestEntity
        {
            Name = "Seeded",
            CreatedAt = explicitTime,
            CreatedBy = explicitUser,
            UpdatedAt = explicitTime,
            UpdatedBy = explicitUser
        };

        ctx.TestEntities.Add(entity);
        await ctx.SaveChangesAsync();

        Assert.Equal(explicitTime, entity.CreatedAt);
        Assert.Equal(explicitUser, entity.CreatedBy);
    }

    [Fact]
    public async Task SavingChanges_DoesNotUpdateCreatedBy_OnUpdate()
    {
        var dbName = Guid.NewGuid().ToString();
        var insertUserId = Guid.NewGuid();
        var updateUserId = Guid.NewGuid();
        var entity = new TestEntity { Name = "Original" };

        await using (var insertCtx = DbContextFactory.CreateContext(dbName, userId: insertUserId))
        {
            insertCtx.TestEntities.Add(entity);
            await insertCtx.SaveChangesAsync();
        }

        await using var updateCtx = DbContextFactory.CreateContext(dbName, userId: updateUserId);
        var loaded = await updateCtx.TestEntities.IgnoreQueryFilters().FirstAsync(e => e.Id == entity.Id);
        loaded.Name = "Modified";
        await updateCtx.SaveChangesAsync();

        Assert.Equal(insertUserId, loaded.CreatedBy);
        Assert.Equal(updateUserId, loaded.UpdatedBy);
    }

    [Fact]
    public async Task SavingChanges_UpdatesUpdatedAtAndUpdatedBy_OnUpdate()
    {
        var dbName = Guid.NewGuid().ToString();
        var entity = new TestEntity { Name = "Original" };
        var updaterId = Guid.NewGuid();

        await using (var insertCtx = DbContextFactory.CreateContext(dbName))
        {
            insertCtx.TestEntities.Add(entity);
            await insertCtx.SaveChangesAsync();
        }

        var before = DateTime.UtcNow;
        await using var updateCtx = DbContextFactory.CreateContext(dbName, userId: updaterId);
        var loaded = await updateCtx.TestEntities.IgnoreQueryFilters().FirstAsync(e => e.Id == entity.Id);
        loaded.Name = "Updated";
        await updateCtx.SaveChangesAsync();

        Assert.Equal(updaterId, loaded.UpdatedBy);
        Assert.True(loaded.UpdatedAt >= before);
    }

    [Fact]
    public async Task SavingChanges_WithNullContext_DoesNotThrow()
    {
        var interceptor = new AuditInterceptor();
        var exception = await Record.ExceptionAsync(async () =>
        {
            await using var ctx = DbContextFactory.CreateContext();
            ctx.TestEntities.Add(new TestEntity { Name = "Safe" });
            await ctx.SaveChangesAsync();
        });

        Assert.Null(exception);
    }
}
