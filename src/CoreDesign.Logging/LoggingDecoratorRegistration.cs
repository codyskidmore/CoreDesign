using System;
using Microsoft.Extensions.DependencyInjection;

namespace CoreDesign.Logging;

/// <summary>
/// DI decoration helpers used by the generated <c>DecorateWithLogging()</c> extension.
/// </summary>
public static class LoggingDecoratorRegistration
{
    /// <summary>
    /// Wraps the last registered <typeparamref name="TService"/> with <typeparamref name="TDecorator"/>.
    /// The decorator receives the original implementation as its first constructor argument.
    /// </summary>
    public static IServiceCollection Decorate<TService, TDecorator>(IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType != typeof(TService)) continue;
            if (descriptor.IsKeyedService) continue;

            var lifetime = descriptor.Lifetime;
            var captured = descriptor;
            services[i] = ServiceDescriptor.Describe(
                typeof(TService),
                provider => ActivatorUtilities.CreateInstance<TDecorator>(
                    provider,
                    ResolveInner<TService>(provider, captured)),
                lifetime);
            return services;
        }

        throw new InvalidOperationException(
            $"No registration for {typeof(TService).FullName} was found. " +
            "Call DecorateWithLogging() after registering your services.");
    }

    /// <summary>
    /// Wraps every closed-generic registration of <paramref name="serviceType"/> with the
    /// corresponding closed form of <paramref name="decoratorType"/>.
    /// Both types must be open generic (e.g. <c>typeof(IRepository&lt;&gt;)</c>).
    /// </summary>
    public static IServiceCollection Decorate(IServiceCollection services, Type serviceType, Type decoratorType)
    {
        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (descriptor.IsKeyedService) continue;
            if (!descriptor.ServiceType.IsGenericType) continue;
            if (descriptor.ServiceType.GetGenericTypeDefinition() != serviceType) continue;

            var typeArgs        = descriptor.ServiceType.GenericTypeArguments;
            var closedDecorator = decoratorType.MakeGenericType(typeArgs);
            var lifetime        = descriptor.Lifetime;
            var captured        = descriptor;
            services[i] = ServiceDescriptor.Describe(
                descriptor.ServiceType,
                provider => ActivatorUtilities.CreateInstance(
                    provider,
                    closedDecorator,
                    ResolveInner(provider, captured)),
                lifetime);
        }

        return services;
    }

    private static TService ResolveInner<TService>(IServiceProvider provider, ServiceDescriptor descriptor)
        where TService : class
    {
        if (descriptor.ImplementationInstance is not null)
            return (TService)descriptor.ImplementationInstance;

        if (descriptor.ImplementationFactory is not null)
            return (TService)descriptor.ImplementationFactory(provider);

        return (TService)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
    }

    private static object ResolveInner(IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
            return descriptor.ImplementationInstance;

        if (descriptor.ImplementationFactory is not null)
            return descriptor.ImplementationFactory(provider);

        return ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
    }
}
