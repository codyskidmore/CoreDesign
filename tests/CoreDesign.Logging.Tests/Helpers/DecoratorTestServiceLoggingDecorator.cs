namespace CoreDesign.Logging.Tests.Helpers;

// Hand-written decorator matching the output pattern produced by the source generator.
// This class exists so the test project can verify decorator behavior without referencing
// the generator as an analyzer.
public sealed class DecoratorTestServiceLoggingDecorator : IDecoratorTestService
{
    private readonly IDecoratorTestService _inner;
    private readonly ILogger _logger;

    public DecoratorTestServiceLoggingDecorator(IDecoratorTestService inner, ILoggerFactory loggerFactory)
    {
        _inner = inner;
        _logger = loggerFactory.CreateLogger("CoreDesign.Logging.Tests.Helpers.IDecoratorTestService");
    }

    public async Task<string> GetValueAsync(string key)
    {
        _logger.LogInformation("Invoking {Method} with {@key}", "GetValueAsync", key);
        try
        {
            var __result = await _inner.GetValueAsync(key);
            _logger.LogInformation("{Method} returned {@Result}", "GetValueAsync", __result);
            return __result;
        }
        catch (Exception __ex)
        {
            _logger.LogError(__ex, "{Method} threw an exception", "GetValueAsync");
            throw;
        }
    }

    public async Task<FindResult> FindAsync(string id)
    {
        _logger.LogInformation("Invoking {Method} with {@id}", "FindAsync", id);
        try
        {
            var __result = await _inner.FindAsync(id);
            switch (__result)
            {
                case string __t0:
                    _logger.LogInformation("{Method} returned {@Result}", "FindAsync", __t0);
                    break;
                case NotFoundMessage __t1:
                    _logger.LogWarning("{Method} returned {@Result}", "FindAsync", __t1);
                    break;
            }
            return __result;
        }
        catch (Exception __ex)
        {
            _logger.LogError(__ex, "{Method} threw an exception", "FindAsync");
            throw;
        }
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("Invoking {Method}", "RunAsync");
        try
        {
            await _inner.RunAsync();
            _logger.LogInformation("{Method} completed", "RunAsync");
        }
        catch (Exception __ex)
        {
            _logger.LogError(__ex, "{Method} threw an exception", "RunAsync");
            throw;
        }
    }

    public string Login(string username, string password)
    {
        _logger.LogInformation("Invoking {Method} with {@username}, {@password}", "Login", username, "[REDACTED]");
        try
        {
            var __result = _inner.Login(username, password);
            _logger.LogInformation("{Method} returned {@Result}", "Login", __result);
            return __result;
        }
        catch (Exception __ex)
        {
            _logger.LogError(__ex, "{Method} threw an exception", "Login");
            throw;
        }
    }

    public string GetSecret() => _inner.GetSecret();

    public Task<string> GetSecretAsync() => _inner.GetSecretAsync();

    // Properties delegated to _inner (Gap 2 pattern)
    public string Status
    {
        get => _inner.Status;
        set => _inner.Status = value;
    }

    public string Label => _inner.Label;

    public string this[int index]
    {
        get => _inner[index];
        set => _inner[index] = value;
    }
}
