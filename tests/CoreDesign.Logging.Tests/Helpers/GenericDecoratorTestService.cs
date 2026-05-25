namespace CoreDesign.Logging.Tests.Helpers;

public interface IGenericDecoratorTestService<T> where T : class
{
    Task<T?> FindAsync(string id, CancellationToken ct = default);
    Task SaveAsync(T item, CancellationToken ct = default);
}

public class GenericDecoratorTestService<T> : IGenericDecoratorTestService<T> where T : class
{
    public Task<T?> FindAsync(string id, CancellationToken ct = default) => Task.FromResult<T?>(null);
    public Task SaveAsync(T item, CancellationToken ct = default) => Task.CompletedTask;
}

// Hand-written decorator matching the generator's output pattern for a generic interface.
// The class declaration, constraint clause, and interface references all carry <T>.
public sealed class GenericDecoratorTestServiceLoggingDecorator<T> : IGenericDecoratorTestService<T>
    where T : class
{
    private readonly IGenericDecoratorTestService<T> _inner;
    private readonly ILogger _logger;

    public GenericDecoratorTestServiceLoggingDecorator(
        IGenericDecoratorTestService<T> inner, ILoggerFactory loggerFactory)
    {
        _inner = inner;
        _logger = loggerFactory.CreateLogger(
            "CoreDesign.Logging.Tests.Helpers.IGenericDecoratorTestService");
    }

    public async Task<T?> FindAsync(string id, CancellationToken ct = default)
    {
        _logger.LogInformation("Invoking {Method} with {@id}", "FindAsync", id);
        try
        {
            var __result = await _inner.FindAsync(id, ct);
            _logger.LogInformation("{Method} returned {@Result}", "FindAsync", __result);
            return __result;
        }
        catch (Exception __ex)
        {
            _logger.LogError(__ex, "{Method} threw an exception", "FindAsync");
            throw;
        }
    }

    public async Task SaveAsync(T item, CancellationToken ct = default)
    {
        _logger.LogInformation("Invoking {Method} with {@item}", "SaveAsync", item);
        try
        {
            await _inner.SaveAsync(item, ct);
            _logger.LogInformation("{Method} completed", "SaveAsync");
        }
        catch (Exception __ex)
        {
            _logger.LogError(__ex, "{Method} threw an exception", "SaveAsync");
            throw;
        }
    }
}
