using CoreDesign.Data.Infrastructure;
using CoreDesign.Data.Tests.Helpers;

namespace CoreDesign.Data.Tests.Infrastructure;

public class BaseEntityExtensionMethodsTests
{
    [Fact]
    public void InitializeAuditFields_AssignsNewId()
    {
        var entity = new TestEntity();
        entity.InitializeAuditFields(Guid.NewGuid());
        Assert.NotEqual(default, entity.Id);
    }

    [Fact]
    public void InitializeAuditFields_SetsCreatedBy()
    {
        var entity = new TestEntity();
        var userId = Guid.NewGuid();
        entity.InitializeAuditFields(userId);
        Assert.Equal(userId, entity.CreatedBy);
    }

    [Fact]
    public void InitializeAuditFields_SetsUpdatedBy()
    {
        var entity = new TestEntity();
        var userId = Guid.NewGuid();
        entity.InitializeAuditFields(userId);
        Assert.Equal(userId, entity.UpdatedBy);
    }

    [Fact]
    public void InitializeAuditFields_SetsCreatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow;
        var entity = new TestEntity();
        entity.InitializeAuditFields(Guid.NewGuid());
        var after = DateTime.UtcNow;
        Assert.InRange(entity.CreatedAt, before, after);
    }

    [Fact]
    public void InitializeAuditFields_SetsUpdatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow;
        var entity = new TestEntity();
        entity.InitializeAuditFields(Guid.NewGuid());
        var after = DateTime.UtcNow;
        Assert.InRange(entity.UpdatedAt, before, after);
    }

    [Fact]
    public void InitializeAuditFields_LeavesIsDeletedFalse()
    {
        var entity = new TestEntity();
        entity.InitializeAuditFields(Guid.NewGuid());
        Assert.False(entity.IsDeleted);
    }

    [Fact]
    public void UpdateAuditFields_SetsUpdatedBy()
    {
        var entity = new TestEntity();
        entity.InitializeAuditFields(Guid.NewGuid());
        var newUserId = Guid.NewGuid();
        entity.UpdateAuditFields(newUserId);
        Assert.Equal(newUserId, entity.UpdatedBy);
    }

    [Fact]
    public void UpdateAuditFields_SetsUpdatedAt_ToApproximatelyNow()
    {
        var entity = new TestEntity();
        entity.InitializeAuditFields(Guid.NewGuid());
        var before = DateTime.UtcNow;
        entity.UpdateAuditFields(Guid.NewGuid());
        var after = DateTime.UtcNow;
        Assert.InRange(entity.UpdatedAt, before, after);
    }

    [Fact]
    public void UpdateAuditFields_DoesNotChangeCreatedBy()
    {
        var originalUserId = Guid.NewGuid();
        var entity = new TestEntity();
        entity.InitializeAuditFields(originalUserId);
        entity.UpdateAuditFields(Guid.NewGuid());
        Assert.Equal(originalUserId, entity.CreatedBy);
    }

    [Fact]
    public void UpdateAuditFields_DoesNotChangeCreatedAt()
    {
        var entity = new TestEntity();
        entity.InitializeAuditFields(Guid.NewGuid());
        var createdAt = entity.CreatedAt;
        entity.UpdateAuditFields(Guid.NewGuid());
        Assert.Equal(createdAt, entity.CreatedAt);
    }

    [Fact]
    public void UpdateAuditFields_DoesNotChangeId()
    {
        var entity = new TestEntity();
        entity.InitializeAuditFields(Guid.NewGuid());
        var id = entity.Id;
        entity.UpdateAuditFields(Guid.NewGuid());
        Assert.Equal(id, entity.Id);
    }
}
