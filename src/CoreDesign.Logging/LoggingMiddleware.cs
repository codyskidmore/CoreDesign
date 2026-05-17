namespace CoreDesign.Logging;

public static class LoggingMiddleware
{
    public const int DefaultMaxResultLength = 500;
}

public class LoggingMiddleware<T> : DispatchProxy where T : class
{
    private T _service = null!;
    private ILogger _logger = null!;

    public static T Create(T service, ILogger logger)
    {
        var proxy = Create<T, LoggingMiddleware<T>>();
        var loggingMiddleware = (LoggingMiddleware<T>)(object)proxy;
        loggingMiddleware._service = service;
        loggingMiddleware._logger = logger;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        if (targetMethod.IsDefined(typeof(SuppressAttribute), false))
        {
            try
            {
                return targetMethod.Invoke(_service, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }

        _logger.LogInformation("Invoking {ServiceType}.{Method} with parameters {@Parameters}",
            typeof(T).Name, targetMethod.Name, SanitizeArgs(targetMethod, args));

        try
        {
            var result = targetMethod.Invoke(_service, args);

            if (result is Task task)
                return WrapTask(task, targetMethod);

            _logger.LogInformation("{ServiceType}.{Method} returned {Result}",
                typeof(T).Name, targetMethod.Name, FormatResult(result, targetMethod));

            return result;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            _logger.LogError(ex.InnerException, "{ServiceType}.{Method} threw an exception",
                typeof(T).Name, targetMethod.Name);
            throw ex.InnerException;
        }
    }

    private static object?[] SanitizeArgs(MethodInfo method, object?[]? args)
    {
        if (args is null || args.Length == 0) return [];

        var parameters = method.GetParameters();
        var sanitized = new object?[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            sanitized[i] = parameters[i].IsDefined(typeof(RedactAttribute), false)
                ? "[REDACTED]"
                : args[i];
        }
        return sanitized;
    }

    private static string FormatResult(object? result, MethodInfo method)
    {
        var maxLength = LoggingMiddleware.DefaultMaxResultLength;
        var attr = method.GetCustomAttribute<TruncateLogAttribute>();
        if (attr is not null)
            maxLength = attr.MaxLength;

        var json = JsonSerializer.Serialize(result);
        if (maxLength <= 0 || json.Length <= maxLength)
            return json;

        return $"{json[..maxLength]}... [truncated, total {json.Length} chars]";
    }

    private object WrapTask(Task task, MethodInfo method)
    {
        var returnType = method.ReturnType;

        if (!returnType.IsGenericType)
            return WrapVoidTaskAsync(task, method);

        var resultType = returnType.GetGenericArguments()[0];
        var wrapMethod = typeof(LoggingMiddleware<T>)
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
                _logger.LogWarning("{ServiceType}.{Method} returned {Result}",
                    typeof(T).Name, method.Name, FormatResult(resultValue, method));
                return result;
            }

            _logger.LogInformation("{ServiceType}.{Method} returned {Result}",
                typeof(T).Name, method.Name, FormatResult(resultValue, method));

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

public static class LoggingMiddlewareExtensions
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
            return LoggingMiddleware<TInterface>.Create(implementation, logger);
        }, lifetime));

        return services;
    }

    public static IServiceCollection AddWithLogging(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        var loggableType = typeof(ILoggable);
        var genericMethod = typeof(LoggingMiddlewareExtensions)
            .GetMethods()
            .First(m => m.Name == nameof(AddWithLogging)
                     && m.IsGenericMethod
                     && m.GetGenericArguments().Length == 2);

        var loggableTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && loggableType.IsAssignableFrom(t));

        foreach (var implementation in loggableTypes)
        {
            var interfaces = implementation.GetInterfaces()
                .Where(i => i != loggableType);

            foreach (var iface in interfaces)
            {
                genericMethod
                    .MakeGenericMethod(iface, implementation)
                    .Invoke(null, [services, lifetime]);
            }
        }

        return services;
    }
}
