using CoreDesign.Data.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CoreDesign.Data.Tests.Infrastructure;

public class CoreDesignDataExtensionsTests
{
    [Fact]
    public void AddCoreDesignData_RegistersICurrentUserAccessor()
    {
        var services = new ServiceCollection();
        services.AddCoreDesignData<SystemUserAccessor>();
        var sp = services.BuildServiceProvider();

        var accessor = sp.GetRequiredService<ICurrentUserAccessor>();

        Assert.IsType<SystemUserAccessor>(accessor);
    }

    [Fact]
    public void AddCoreDesignData_RegistersAuditInterceptor()
    {
        var services = new ServiceCollection();
        services.AddCoreDesignData<SystemUserAccessor>();
        var sp = services.BuildServiceProvider();

        var interceptor = sp.GetRequiredService<AuditInterceptor>();

        Assert.NotNull(interceptor);
    }

    [Fact]
    public void AddCoreDesignData_RegistersICurrentUserAccessor_AsSingleton()
    {
        var services = new ServiceCollection();
        services.AddCoreDesignData<SystemUserAccessor>();
        var sp = services.BuildServiceProvider();

        var first = sp.GetRequiredService<ICurrentUserAccessor>();
        var second = sp.GetRequiredService<ICurrentUserAccessor>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddCoreDesignData_RegistersAuditInterceptor_AsSingleton()
    {
        var services = new ServiceCollection();
        services.AddCoreDesignData<SystemUserAccessor>();
        var sp = services.BuildServiceProvider();

        var first = sp.GetRequiredService<AuditInterceptor>();
        var second = sp.GetRequiredService<AuditInterceptor>();

        Assert.Same(first, second);
    }

    [Fact]
    public void AddCoreDesignData_ReturnsServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();

        var result = services.AddCoreDesignData<SystemUserAccessor>();

        Assert.Same(services, result);
    }
}
