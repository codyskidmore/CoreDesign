using CoreDesign.Logging.Tests.Helpers;
using Moq;

namespace CoreDesign.Logging.Tests;

public class LoggingMiddlewareTests
{
    private readonly CapturingLogger _logger = new();
    private readonly Mock<ITestService> _serviceMock = new();
    private readonly ITestService _proxy;

    public LoggingMiddlewareTests()
    {
        _proxy = LoggingMiddleware<ITestService>.Create(_serviceMock.Object, _logger);
    }

    [Fact]
    public void Create_ReturnsInstanceImplementingInterface()
    {
        Assert.NotNull(_proxy);
        Assert.IsAssignableFrom<ITestService>(_proxy);
    }

    [Fact]
    public void Invoke_SyncMethod_LogsInvocation()
    {
        _serviceMock.Setup(s => s.GetValue("x")).Returns("y");

        _proxy.GetValue("x");

        Assert.True(_logger.HasEntry(LogLevel.Information, "Invoking"));
    }

    [Fact]
    public void Invoke_SyncMethod_LogsResult()
    {
        _serviceMock.Setup(s => s.GetValue("x")).Returns("y");

        _proxy.GetValue("x");

        Assert.True(_logger.HasEntry(LogLevel.Information, "returned"));
    }

    [Fact]
    public void Invoke_SyncMethod_ReturnsValueFromInnerService()
    {
        _serviceMock.Setup(s => s.GetValue("hello")).Returns("world");

        var result = _proxy.GetValue("hello");

        Assert.Equal("world", result);
    }

    [Fact]
    public void Invoke_SyncVoidMethod_LogsInvocationAndResult()
    {
        _proxy.Execute("input");

        Assert.True(_logger.HasEntry(LogLevel.Information, "Invoking"));
        Assert.True(_logger.HasEntry(LogLevel.Information, "returned"));
    }

    [Fact]
    public void Invoke_SyncMethod_WhenThrows_RethrowsOriginalException()
    {
        _serviceMock.Setup(s => s.GetValue(It.IsAny<string>()))
            .Throws(new InvalidOperationException("inner error"));

        var ex = Assert.Throws<InvalidOperationException>(() => _proxy.GetValue("x"));

        Assert.Equal("inner error", ex.Message);
    }

    [Fact]
    public void Invoke_SyncMethod_WhenThrows_LogsError()
    {
        _serviceMock.Setup(s => s.GetValue(It.IsAny<string>()))
            .Throws(new InvalidOperationException("boom"));

        Assert.Throws<InvalidOperationException>(() => _proxy.GetValue("x"));

        Assert.True(_logger.HasEntry(LogLevel.Error, "threw an exception"));
    }

    [Fact]
    public async Task Invoke_AsyncVoidTask_LogsInvocationAndCompletion()
    {
        _serviceMock.Setup(s => s.RunAsync()).Returns(Task.CompletedTask);

        await _proxy.RunAsync();

        Assert.True(_logger.HasEntry(LogLevel.Information, "Invoking"));
        Assert.True(_logger.HasEntry(LogLevel.Information, "completed"));
    }

    [Fact]
    public async Task Invoke_AsyncVoidTask_WhenThrows_RethrowsException()
    {
        _serviceMock.Setup(s => s.RunAsync())
            .Returns(Task.FromException(new InvalidOperationException("async error")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _proxy.RunAsync());
    }

    [Fact]
    public async Task Invoke_AsyncVoidTask_WhenThrows_LogsError()
    {
        _serviceMock.Setup(s => s.RunAsync())
            .Returns(Task.FromException(new InvalidOperationException("async error")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _proxy.RunAsync());

        Assert.True(_logger.HasEntry(LogLevel.Error, "threw an exception"));
    }

    [Fact]
    public async Task Invoke_AsyncGenericTask_LogsInvocationAndResult()
    {
        _serviceMock.Setup(s => s.FetchAsync("in")).ReturnsAsync("out");

        await _proxy.FetchAsync("in");

        Assert.True(_logger.HasEntry(LogLevel.Information, "Invoking"));
        Assert.True(_logger.HasEntry(LogLevel.Information, "returned"));
    }

    [Fact]
    public async Task Invoke_AsyncGenericTask_ReturnsValueFromInnerService()
    {
        _serviceMock.Setup(s => s.FetchAsync("key")).ReturnsAsync("value");

        var result = await _proxy.FetchAsync("key");

        Assert.Equal("value", result);
    }

    [Fact]
    public async Task Invoke_AsyncGenericTask_WhenThrows_RethrowsException()
    {
        _serviceMock.Setup(s => s.FetchAsync(It.IsAny<string>()))
            .Returns(Task.FromException<string>(new InvalidOperationException("fetch error")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _proxy.FetchAsync("x"));
    }

    [Fact]
    public async Task Invoke_AsyncGenericTask_WhenThrows_LogsError()
    {
        _serviceMock.Setup(s => s.FetchAsync(It.IsAny<string>()))
            .Returns(Task.FromException<string>(new InvalidOperationException("fetch error")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _proxy.FetchAsync("x"));

        Assert.True(_logger.HasEntry(LogLevel.Error, "threw an exception"));
    }

    [Fact]
    public async Task Invoke_AsyncGenericTask_WithNotFoundResult_LogsWarning()
    {
        OneOf<string, NotFoundMessage> notFound = new NotFoundMessage("not found");
        _serviceMock.Setup(s => s.FindAsync(It.IsAny<string>())).ReturnsAsync(notFound);

        await _proxy.FindAsync("missing");

        Assert.True(_logger.HasEntry(LogLevel.Warning, "returned"));
        Assert.False(_logger.HasEntry(LogLevel.Information, "returned"));
    }

    [Fact]
    public async Task Invoke_AsyncGenericTask_WithBadRequestResult_LogsWarning()
    {
        OneOf<string, BadRequestMessage> badRequest = new BadRequestMessage("invalid input");
        _serviceMock.Setup(s => s.ValidateAsync(It.IsAny<string>())).ReturnsAsync(badRequest);

        await _proxy.ValidateAsync("bad");

        Assert.True(_logger.HasEntry(LogLevel.Warning, "returned"));
        Assert.False(_logger.HasEntry(LogLevel.Information, "returned"));
    }

    [Fact]
    public async Task Invoke_AsyncGenericTask_WithSuccessOneOfResult_LogsInformation()
    {
        OneOf<string, NotFoundMessage> found = "result value";
        _serviceMock.Setup(s => s.FindAsync("existing")).ReturnsAsync(found);

        await _proxy.FindAsync("existing");

        Assert.True(_logger.HasEntry(LogLevel.Information, "returned"));
        Assert.False(_logger.HasEntry(LogLevel.Warning, "returned"));
    }
}
