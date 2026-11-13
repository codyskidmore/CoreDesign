using System.Reflection;
using CoreDesign.Logging.Tests.Helpers;

namespace CoreDesign.Logging.Tests;

// Exercises LoggingDecoratorRegistration.Decorate(services, Type, Type), the registration path the
// generated DecorateWithLogging() uses for generic [LoggingDecorator] interfaces.
public class DecoratorRegistrationTests
{
    private readonly CapturingLogger _logger = new();

    private ServiceCollection NewServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(new CapturingLoggerFactory(_logger));
        return services;
    }

    private static void DecorateGeneric(IServiceCollection services) =>
        LoggingDecoratorRegistration.Decorate(
            services,
            typeof(IGenericDecoratorTestService<>),
            typeof(GenericDecoratorTestServiceLoggingDecorator<>));

    private static object InnerOf(object decorator) =>
        decorator.GetType()
            .GetField("_inner", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(decorator)!;

    [Fact]
    public async Task OpenGenericRegistration_IsDecorated()
    {
        var services = NewServices();
        services.AddScoped(typeof(IGenericDecoratorTestService<>), typeof(GenericDecoratorTestService<>));

        DecorateGeneric(services);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IGenericDecoratorTestService<string>>();

        Assert.IsType<GenericDecoratorTestServiceLoggingDecorator<string>>(service);
        Assert.IsType<GenericDecoratorTestService<string>>(InnerOf(service));

        await service.FindAsync("id");
        Assert.True(_logger.HasEntry(LogLevel.Information, "Invoking"));
    }

    [Fact]
    public void OpenGenericRegistration_KeepsLifetime()
    {
        var services = NewServices();
        services.AddScoped(typeof(IGenericDecoratorTestService<>), typeof(GenericDecoratorTestService<>));

        DecorateGeneric(services);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();
        var a = scope1.ServiceProvider.GetRequiredService<IGenericDecoratorTestService<string>>();
        var b = scope1.ServiceProvider.GetRequiredService<IGenericDecoratorTestService<string>>();
        var c = scope2.ServiceProvider.GetRequiredService<IGenericDecoratorTestService<string>>();

        Assert.Same(a, b);
        Assert.NotSame(a, c);
        Assert.Same(InnerOf(a), InnerOf(b));
        Assert.NotSame(InnerOf(a), InnerOf(c));
    }

    [Fact]
    public void OpenGenericRegistration_DecoratesEveryClosedForm()
    {
        var services = NewServices();
        services.AddTransient(typeof(IGenericDecoratorTestService<>), typeof(GenericDecoratorTestService<>));

        DecorateGeneric(services);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<GenericDecoratorTestServiceLoggingDecorator<string>>(
            provider.GetRequiredService<IGenericDecoratorTestService<string>>());
        Assert.IsType<GenericDecoratorTestServiceLoggingDecorator<object>>(
            provider.GetRequiredService<IGenericDecoratorTestService<object>>());
    }

    [Fact]
    public void ClosedGenericRegistration_IsDecorated()
    {
        var services = NewServices();
        services.AddScoped<IGenericDecoratorTestService<string>, GenericDecoratorTestService<string>>();

        DecorateGeneric(services);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IGenericDecoratorTestService<string>>();

        Assert.IsType<GenericDecoratorTestServiceLoggingDecorator<string>>(service);
        Assert.IsType<GenericDecoratorTestService<string>>(InnerOf(service));
    }

    [Fact]
    public void OpenAndClosedRegistrations_AreBothDecorated()
    {
        var services = NewServices();
        services.AddTransient(typeof(IGenericDecoratorTestService<>), typeof(GenericDecoratorTestService<>));
        services.AddTransient<IGenericDecoratorTestService<string>, ClosedStringService>();

        DecorateGeneric(services);

        using var provider = services.BuildServiceProvider();
        var closed = provider.GetRequiredService<IGenericDecoratorTestService<string>>();
        var open = provider.GetRequiredService<IGenericDecoratorTestService<object>>();

        Assert.IsType<ClosedStringService>(InnerOf(closed));
        Assert.IsType<GenericDecoratorTestService<object>>(InnerOf(open));
    }

    [Fact]
    public void OpenGenericRegistration_DecoratingTwice_DoesNotWrapTheDecorator()
    {
        var services = NewServices();
        services.AddTransient(typeof(IGenericDecoratorTestService<>), typeof(GenericDecoratorTestService<>));

        DecorateGeneric(services);
        DecorateGeneric(services);

        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IGenericDecoratorTestService<string>>();

        Assert.IsType<GenericDecoratorTestService<string>>(InnerOf(service));
    }

    [Fact]
    public void TwoOpenGenericRegistrations_ThrowsClearError()
    {
        var services = NewServices();
        services.AddTransient(typeof(IGenericDecoratorTestService<>), typeof(GenericDecoratorTestService<>));
        services.AddTransient(typeof(IGenericDecoratorTestService<>), typeof(OtherGenericService<>));

        var ex = Assert.Throws<InvalidOperationException>(() => DecorateGeneric(services));
        Assert.Contains("More than one open generic registration", ex.Message);
    }

    private sealed class ClosedStringService : IGenericDecoratorTestService<string>
    {
        public Task<string?> FindAsync(string id, CancellationToken ct = default) => Task.FromResult<string?>(id);
        public Task SaveAsync(string item, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class OtherGenericService<T> : IGenericDecoratorTestService<T> where T : class
    {
        public Task<T?> FindAsync(string id, CancellationToken ct = default) => Task.FromResult<T?>(null);
        public Task SaveAsync(T item, CancellationToken ct = default) => Task.CompletedTask;
    }
}
