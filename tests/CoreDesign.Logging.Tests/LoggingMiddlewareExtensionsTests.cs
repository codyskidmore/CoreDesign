using CoreDesign.Logging.Tests.Helpers;

namespace CoreDesign.Logging.Tests;

public class LoggingMiddlewareExtensionsTests
{
    private static ServiceProvider BuildProvider(ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWithLogging<ITestService, TestService>(lifetime);
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildProviderFromAssembly(ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWithLogging(typeof(TestService).Assembly, lifetime);
        return services.BuildServiceProvider();
    }

    // Explicit registration

    [Fact]
    public void AddWithLogging_InterfaceIsResolvable()
    {
        using var provider = BuildProvider();

        var service = provider.GetService<ITestService>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddWithLogging_ImplementationIsResolvable()
    {
        using var provider = BuildProvider();

        var impl = provider.GetService<TestService>();

        Assert.NotNull(impl);
    }

    [Fact]
    public void AddWithLogging_InterfaceResolvesAsProxy_NotConcreteType()
    {
        using var provider = BuildProvider();

        var service = provider.GetRequiredService<ITestService>();

        Assert.IsAssignableFrom<ITestService>(service);
        Assert.IsNotType<TestService>(service);
    }

    [Fact]
    public void AddWithLogging_TransientLifetime_ReturnsDifferentInstancesPerResolve()
    {
        using var provider = BuildProvider(ServiceLifetime.Transient);

        var s1 = provider.GetRequiredService<ITestService>();
        var s2 = provider.GetRequiredService<ITestService>();

        Assert.NotSame(s1, s2);
    }

    [Fact]
    public void AddWithLogging_ScopedLifetime_ReturnsSameInstanceWithinScope()
    {
        using var provider = BuildProvider(ServiceLifetime.Scoped);
        using var scope = provider.CreateScope();

        var s1 = scope.ServiceProvider.GetRequiredService<ITestService>();
        var s2 = scope.ServiceProvider.GetRequiredService<ITestService>();

        Assert.Same(s1, s2);
    }

    [Fact]
    public void AddWithLogging_ScopedLifetime_ReturnsDifferentInstancesAcrossScopes()
    {
        using var provider = BuildProvider(ServiceLifetime.Scoped);

        ITestService s1, s2;
        using (var scope1 = provider.CreateScope())
            s1 = scope1.ServiceProvider.GetRequiredService<ITestService>();
        using (var scope2 = provider.CreateScope())
            s2 = scope2.ServiceProvider.GetRequiredService<ITestService>();

        Assert.NotSame(s1, s2);
    }

    [Fact]
    public void AddWithLogging_SingletonLifetime_ReturnsSameInstanceGlobally()
    {
        using var provider = BuildProvider(ServiceLifetime.Singleton);

        var s1 = provider.GetRequiredService<ITestService>();
        var s2 = provider.GetRequiredService<ITestService>();

        Assert.Same(s1, s2);
    }

    // Assembly scanning via ILoggable

    [Fact]
    public void AddWithLogging_Assembly_LoggableInterface_IsResolvable()
    {
        using var provider = BuildProviderFromAssembly();

        var service = provider.GetService<ITestService>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddWithLogging_Assembly_LoggableImplementation_IsResolvable()
    {
        using var provider = BuildProviderFromAssembly();

        var impl = provider.GetService<TestService>();

        Assert.NotNull(impl);
    }

    [Fact]
    public void AddWithLogging_Assembly_LoggableInterface_ResolvesAsProxy_NotConcreteType()
    {
        using var provider = BuildProviderFromAssembly();

        var service = provider.GetRequiredService<ITestService>();

        Assert.IsAssignableFrom<ITestService>(service);
        Assert.IsNotType<TestService>(service);
    }

    [Fact]
    public void AddWithLogging_Assembly_NonLoggableType_IsNotRegistered()
    {
        using var provider = BuildProviderFromAssembly();

        var service = provider.GetService<IUnloggableService>();

        Assert.Null(service);
    }

    [Fact]
    public void AddWithLogging_Assembly_TransientLifetime_ReturnsDifferentInstancesPerResolve()
    {
        using var provider = BuildProviderFromAssembly(ServiceLifetime.Transient);

        var s1 = provider.GetRequiredService<ITestService>();
        var s2 = provider.GetRequiredService<ITestService>();

        Assert.NotSame(s1, s2);
    }

    [Fact]
    public void AddWithLogging_Assembly_ScopedLifetime_ReturnsSameInstanceWithinScope()
    {
        using var provider = BuildProviderFromAssembly(ServiceLifetime.Scoped);
        using var scope = provider.CreateScope();

        var s1 = scope.ServiceProvider.GetRequiredService<ITestService>();
        var s2 = scope.ServiceProvider.GetRequiredService<ITestService>();

        Assert.Same(s1, s2);
    }

    [Fact]
    public void AddWithLogging_Assembly_SingletonLifetime_ReturnsSameInstanceGlobally()
    {
        using var provider = BuildProviderFromAssembly(ServiceLifetime.Singleton);

        var s1 = provider.GetRequiredService<ITestService>();
        var s2 = provider.GetRequiredService<ITestService>();

        Assert.Same(s1, s2);
    }
}
