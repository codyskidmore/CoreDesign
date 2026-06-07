using CoreDesign.Data.Infrastructure;

namespace CoreDesign.Data.Tests.Infrastructure;

public class SystemUserAccessorTests
{
    [Fact]
    public void UserId_ReturnsGuidEmpty()
    {
        var accessor = new SystemUserAccessor();

        Assert.Equal(Guid.Empty, accessor.UserId);
    }

    [Fact]
    public void UserId_IsConsistentAcrossMultipleCalls()
    {
        var accessor = new SystemUserAccessor();

        Assert.Equal(accessor.UserId, accessor.UserId);
    }

    [Fact]
    public void Implements_ICurrentUserAccessor()
    {
        var accessor = new SystemUserAccessor();

        Assert.IsAssignableFrom<ICurrentUserAccessor>(accessor);
    }
}
