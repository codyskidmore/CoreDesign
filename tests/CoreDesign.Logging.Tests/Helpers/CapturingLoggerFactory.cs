namespace CoreDesign.Logging.Tests.Helpers;

public class CapturingLoggerFactory : ILoggerFactory
{
    private readonly CapturingLogger _logger;

    public CapturingLoggerFactory(CapturingLogger logger) => _logger = logger;

    public ILogger CreateLogger(string categoryName) => _logger;
    public void AddProvider(ILoggerProvider provider) { }
    public void Dispose() { }
}
