using System.Reflection;

namespace SampleApi.Api.Infrastructure;

public class LoggingProxy<T> : DispatchProxy where T : class
{
    private T _service = null!;
    private ILogger _logger = null!;

    public static T Create(T service, ILogger logger)
    {
        var proxy = Create<T, LoggingProxy<T>>();
        var loggingProxy = (LoggingProxy<T>)(object)proxy;
        loggingProxy._service = service;
        loggingProxy._logger = logger;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        _logger.LogInformation("Invoking {ServiceType}.{Method} with parameters {@Parameters}",
            typeof(T).Name, targetMethod.Name, args);

        try
        {
            var result = targetMethod.Invoke(_service, args);

            if (result is Task task)
                return WrapTask(task, targetMethod);

            _logger.LogInformation("{ServiceType}.{Method} returned {@Result}",
                typeof(T).Name, targetMethod.Name, result);

            return result;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            _logger.LogError(ex.InnerException, "{ServiceType}.{Method} threw an exception",
                typeof(T).Name, targetMethod.Name);
            throw ex.InnerException;
        }
    }

    private object WrapTask(Task task, MethodInfo method)
    {
        var returnType = method.ReturnType;

        if (!returnType.IsGenericType)
            return WrapVoidTaskAsync(task, method);

        var resultType = returnType.GetGenericArguments()[0];
        var wrapMethod = typeof(LoggingProxy<T>)
            .GetMethod(nameof(WrapGenericTaskAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(resultType);

        return wrapMethod.Invoke(this, [task, method])!;
    }

    private async Task WrapVoidTaskAsync(Task task, MethodInfo method)
    {
        try
        {
            await task;
            _logger.LogInformation("{ServiceType}.{Method} completed",
                typeof(T).Name, method.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ServiceType}.{Method} threw an exception",
                typeof(T).Name, method.Name);
            throw;
        }
    }

    private async Task<TResult> WrapGenericTaskAsync<TResult>(Task<TResult> task, MethodInfo method)
    {
        try
        {
            var result = await task;
            var resultValue = result is IOneOf oneOf ? oneOf.Value : result;

            if (resultValue is NotFoundMessage or BadRequestMessage)
            {
                _logger.LogWarning("{ServiceType}.{Method} returned {@Result}",
                    typeof(T).Name, method.Name, result);
                return result;
            }

            _logger.LogInformation("{ServiceType}.{Method} returned {@Result}",
                    typeof(T).Name, method.Name, result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ServiceType}.{Method} threw an exception",
                typeof(T).Name, method.Name);
            throw;
        }
    }
}

public static class LoggingProxyExtensions
{
    public static IServiceCollection AddWithLogging<TInterface, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.Add(new ServiceDescriptor(typeof(TImplementation), typeof(TImplementation), lifetime));
        services.Add(new ServiceDescriptor(typeof(TInterface), provider =>
        {
            var implementation = provider.GetRequiredService<TImplementation>();
            var logger = provider.GetRequiredService<ILogger<TImplementation>>();
            return LoggingProxy<TInterface>.Create(implementation, logger);
        }, lifetime));

        return services;
    }
}
