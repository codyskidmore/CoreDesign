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

    [Fact]
    public void Invoke_Redact_RedactsValueInLog()
    {
        _serviceMock.Setup(s => s.Login("alice", "s3cr3t")).Returns("alice");

        _proxy.Login("alice", "s3cr3t");

        Assert.True(_logger.HasEntry(LogLevel.Information, "[REDACTED]"));
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("s3cr3t"));
    }

    [Fact]
    public void Invoke_Redact_NonSensitiveParamStillLogged()
    {
        _serviceMock.Setup(s => s.Login("alice", "s3cr3t")).Returns("alice");

        _proxy.Login("alice", "s3cr3t");

        Assert.Contains(_logger.Entries, e => e.Message.Contains("alice"));
    }

    [Fact]
    public void Invoke_Redact_ReturnsValueFromInnerService()
    {
        _serviceMock.Setup(s => s.Login("alice", "s3cr3t")).Returns("alice");

        var result = _proxy.Login("alice", "s3cr3t");

        Assert.Equal("alice", result);
    }

    [Fact]
    public void Invoke_Suppress_ProducesNoLogEntries()
    {
        _serviceMock.Setup(s => s.GetSecret("x")).Returns("secret");

        _proxy.GetSecret("x");

        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void Invoke_Suppress_ReturnsValueFromInnerService()
    {
        _serviceMock.Setup(s => s.GetSecret("x")).Returns("secret");

        var result = _proxy.GetSecret("x");

        Assert.Equal("secret", result);
    }

    [Fact]
    public void Invoke_Suppress_WhenThrows_RethrowsException()
    {
        _serviceMock.Setup(s => s.GetSecret(It.IsAny<string>()))
            .Throws(new InvalidOperationException("secret error"));

        Assert.Throws<InvalidOperationException>(() => _proxy.GetSecret("x"));
    }

    [Fact]
    public async Task Invoke_Suppress_AsyncMethod_ProducesNoLogEntries()
    {
        _serviceMock.Setup(s => s.FetchSecretAsync("x")).ReturnsAsync("secret");

        await _proxy.FetchSecretAsync("x");

        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task Invoke_Suppress_AsyncMethod_ReturnsValueFromInnerService()
    {
        _serviceMock.Setup(s => s.FetchSecretAsync("x")).ReturnsAsync("secret");

        var result = await _proxy.FetchSecretAsync("x");

        Assert.Equal("secret", result);
    }

    [Fact]
    public void Invoke_SyncMethod_ResultUnderDefaultLimit_LogsFullJson()
    {
        _serviceMock.Setup(s => s.GetValue("x")).Returns("y");

        _proxy.GetValue("x");

        Assert.Contains(_logger.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("returned") &&
            e.Message.Contains("\"y\"") &&
            !e.Message.Contains("truncated"));
    }

    [Fact]
    public void Invoke_SyncMethod_ResultOverDefaultLimit_LogsTruncated()
    {
        var longValue = new string('a', 600);
        _serviceMock.Setup(s => s.GetValue(It.IsAny<string>())).Returns(longValue);

        _proxy.GetValue("x");

        Assert.Contains(_logger.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("returned") &&
            e.Message.Contains("truncated"));
    }

    [Fact]
    public void Invoke_SyncMethod_ResultOverDefaultLimit_TruncatesAtDefaultLength()
    {
        var longValue = new string('a', 600);
        _serviceMock.Setup(s => s.GetValue(It.IsAny<string>())).Returns(longValue);

        _proxy.GetValue("x");

        var entry = _logger.Entries.First(e => e.Level == LogLevel.Information && e.Message.Contains("returned"));
        var afterReturned = entry.Message[(entry.Message.IndexOf("returned") + "returned".Length)..];
        Assert.Contains($"total {longValue.Length + 2} chars", afterReturned);
    }

    [Fact]
    public void Invoke_TruncateLog_CustomLimit_TruncatesAtCustomLength()
    {
        _serviceMock.Setup(s => s.GetValueWithLimit(It.IsAny<string>())).Returns("hello world");

        _proxy.GetValueWithLimit("x");

        Assert.Contains(_logger.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("returned") &&
            e.Message.Contains("truncated"));
    }

    [Fact]
    public void Invoke_TruncateLog_ZeroLimit_DoesNotTruncate()
    {
        var longValue = new string('a', 600);
        _serviceMock.Setup(s => s.GetValueUnlimited(It.IsAny<string>())).Returns(longValue);

        _proxy.GetValueUnlimited("x");

        Assert.DoesNotContain(_logger.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("returned") &&
            e.Message.Contains("truncated"));
    }

    [Fact]
    public async Task Invoke_AsyncGenericTask_ResultOverDefaultLimit_LogsTruncated()
    {
        var longValue = new string('a', 600);
        _serviceMock.Setup(s => s.FetchAsync(It.IsAny<string>())).ReturnsAsync(longValue);

        await _proxy.FetchAsync("x");

        Assert.Contains(_logger.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("returned") &&
            e.Message.Contains("truncated"));
    }

    [Fact]
    public async Task Invoke_AsyncGenericTask_TruncateLog_CustomLimit_TruncatesAtCustomLength()
    {
        _serviceMock.Setup(s => s.FetchWithLimitAsync(It.IsAny<string>())).ReturnsAsync("hello world");

        await _proxy.FetchWithLimitAsync("x");

        Assert.Contains(_logger.Entries, e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("returned") &&
            e.Message.Contains("truncated"));
    }
}
