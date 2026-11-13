using System;
using Microsoft.Extensions.DependencyInjection;

namespace CoreDesign.Logging;

/// <summary>
/// DI decoration helpers used by the generated <c>DecorateWithLogging()</c> extension.
/// </summary>
public static class LoggingDecoratorRegistration
{
    /// <summary>
    /// The service key under which an open generic implementation is re-registered when its
    /// interface is decorated. Generated decorators for generic interfaces receive their inner
    /// instance through <c>[FromKeyedServices(InnerServiceKey)]</c>.
    /// </summary>
    public const string InnerServiceKey = "CoreDesign.Logging.DecoratedInner";

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
    /// Wraps every registration of the generic <paramref name="serviceType"/> with the
    /// corresponding form of <paramref name="decoratorType"/>.
    /// Both types must be open generic (e.g. <c>typeof(IRepository&lt;&gt;)</c>).
    ///
    /// Closed registrations (<c>AddScoped&lt;IRepository&lt;Order&gt;, OrderRepository&gt;()</c>) are
    /// wrapped individually. An open generic registration
    /// (<c>AddScoped(typeof(IRepository&lt;&gt;), typeof(Repository&lt;&gt;))</c>) is moved to the
    /// keyed slot <see cref="InnerServiceKey"/> and replaced by an open generic registration of
    /// <paramref name="decoratorType"/> with the same lifetime, so every closed form resolved from
    /// it is decorated. Because the keyed slot holds a single implementation, at most one open
    /// generic registration per interface can be decorated.
    /// </summary>
    public static IServiceCollection Decorate(IServiceCollection services, Type serviceType, Type decoratorType)
    {
        var openRegistrationFound = false;

        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (descriptor.IsKeyedService) continue;

            if (descriptor.ServiceType == serviceType)
            {
                // Already decorated by an earlier call; decorating again would make the
                // decorator resolve itself from the keyed slot.
                if (descriptor.ImplementationType == decoratorType) continue;

                if (openRegistrationFound)
                    throw new InvalidOperationException(
                        $"More than one open generic registration of {serviceType.FullName} was found. " +
                        "DecorateWithLogging() can decorate only one open generic implementation per interface. " +
                        "Register the additional implementations with closed types instead.");

                openRegistrationFound = true;
                services[i] = ServiceDescriptor.DescribeKeyed(
                    serviceType, InnerServiceKey, descriptor.ImplementationType!, descriptor.Lifetime);
                services.Insert(i + 1, ServiceDescriptor.Describe(serviceType, decoratorType, descriptor.Lifetime));
                i++;
                continue;
            }

            if (!descriptor.ServiceType.IsConstructedGenericType) continue;
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
