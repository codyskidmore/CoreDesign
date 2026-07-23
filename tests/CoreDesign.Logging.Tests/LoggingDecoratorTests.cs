using CoreDesign.Logging.Tests.Helpers;
using Moq;

namespace CoreDesign.Logging.Tests;

public class LoggingDecoratorTests
{
    private readonly CapturingLogger _logger = new();
    private readonly CapturingLoggerFactory _loggerFactory;
    private readonly Mock<IDecoratorTestService> _serviceMock = new();
    private readonly IDecoratorTestService _decorator;

    public LoggingDecoratorTests()
    {
        _loggerFactory = new CapturingLoggerFactory(_logger);
        _decorator = new DecoratorTestServiceLoggingDecorator(_serviceMock.Object, _loggerFactory);
    }

    [Fact]
    public async Task Invoke_AsyncGenericTask_LogsInvocation()
    {
        _serviceMock.Setup(s => s.GetValueAsync("key")).ReturnsAsync("value");

        await _decorator.GetValueAsync("key");

        Assert.True(_logger.HasEntry(LogLevel.Information, "Invoking"));
    }

    [Fact]
    public async Task Invoke_AsyncGenericTask_LogsResult()
    {
        _serviceMock.Setup(s => s.GetValueAsync("key")).ReturnsAsync("value");

        await _decorator.GetValueAsync("key");

        Assert.True(_logger.HasEntry(LogLevel.Information, "returned"));
    }

    [Fact]
    public async Task Invoke_AsyncGenericTask_ReturnsValueFromInner()
    {
        _serviceMock.Setup(s => s.GetValueAsync("key")).ReturnsAsync("expected");

        var result = await _decorator.GetValueAsync("key");

        Assert.Equal("expected", result);
    }

    [Fact]
    public async Task Invoke_AsyncGenericTask_WhenThrows_LogsError()
    {
        _serviceMock.Setup(s => s.GetValueAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _decorator.GetValueAsync("key"));

        Assert.True(_logger.HasEntry(LogLevel.Error, "threw an exception"));
    }

    [Fact]
    public async Task Invoke_AsyncGenericTask_WhenThrows_RethrowsOriginalException()
    {
        _serviceMock.Setup(s => s.GetValueAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("inner error"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _decorator.GetValueAsync("key"));

        Assert.Equal("inner error", ex.Message);
    }

    [Fact]
    public async Task Invoke_AsyncVoidTask_LogsInvocationAndCompletion()
    {
        _serviceMock.Setup(s => s.RunAsync()).Returns(Task.CompletedTask);

        await _decorator.RunAsync();

        Assert.True(_logger.HasEntry(LogLevel.Information, "Invoking"));
        Assert.True(_logger.HasEntry(LogLevel.Information, "completed"));
    }

    [Fact]
    public async Task Invoke_AsyncVoidTask_WhenThrows_RethrowsException()
    {
        _serviceMock.Setup(s => s.RunAsync())
            .Returns(Task.FromException(new InvalidOperationException("async error")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _decorator.RunAsync());
    }

    [Fact]
    public async Task Invoke_AsyncVoidTask_WhenThrows_LogsError()
    {
        _serviceMock.Setup(s => s.RunAsync())
            .Returns(Task.FromException(new InvalidOperationException("async error")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _decorator.RunAsync());

        Assert.True(_logger.HasEntry(LogLevel.Error, "threw an exception"));
    }

    [Fact]
    public async Task Invoke_Union_SuccessArm_LogsInformation()
    {
        FindResult found = "result value";
        _serviceMock.Setup(s => s.FindAsync("existing")).ReturnsAsync(found);

        await _decorator.FindAsync("existing");

        Assert.True(_logger.HasEntry(LogLevel.Information, "returned"));
        Assert.False(_logger.HasEntry(LogLevel.Warning, "returned"));
    }

    [Fact]
    public async Task Invoke_Union_NotFoundArm_LogsWarning()
    {
        FindResult notFound = new NotFoundMessage("not found");
        _serviceMock.Setup(s => s.FindAsync("missing")).ReturnsAsync(notFound);

        await _decorator.FindAsync("missing");

        Assert.True(_logger.HasEntry(LogLevel.Warning, "returned"));
        Assert.False(_logger.HasEntry(LogLevel.Information, "returned"));
    }

    [Fact]
    public async Task Invoke_Union_ReturnsValueFromInner()
    {
        FindResult found = "the result";
        _serviceMock.Setup(s => s.FindAsync("id")).ReturnsAsync(found);

        var result = await _decorator.FindAsync("id");

        Assert.True(result is string);
        Assert.Equal("the result", (string)result.Value!);
    }

    [Fact]
    public void Invoke_Redact_ReplacesParameterWithRedacted()
    {
        _serviceMock.Setup(s => s.Login("alice", "s3cr3t")).Returns("alice");

        _decorator.Login("alice", "s3cr3t");

        Assert.True(_logger.HasEntry(LogLevel.Information, "[REDACTED]"));
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("s3cr3t"));
    }

    [Fact]
    public void Invoke_Redact_NonSensitiveParameterIsLogged()
    {
        _serviceMock.Setup(s => s.Login("alice", "s3cr3t")).Returns("alice");

        _decorator.Login("alice", "s3cr3t");

        Assert.Contains(_logger.Entries, e => e.Message.Contains("alice"));
    }

    [Fact]
    public void Invoke_Redact_ReturnsValueFromInner()
    {
        _serviceMock.Setup(s => s.Login("alice", "s3cr3t")).Returns("alice");

        var result = _decorator.Login("alice", "s3cr3t");

        Assert.Equal("alice", result);
    }

    [Fact]
    public void Invoke_Suppress_ProducesNoLogEntries()
    {
        _serviceMock.Setup(s => s.GetSecret()).Returns("secret");

        _decorator.GetSecret();

        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void Invoke_Suppress_ReturnsValueFromInner()
    {
        _serviceMock.Setup(s => s.GetSecret()).Returns("secret");

        var result = _decorator.GetSecret();

        Assert.Equal("secret", result);
    }

    [Fact]
    public async Task Invoke_Suppress_AsyncMethod_ProducesNoLogEntries()
    {
        _serviceMock.Setup(s => s.GetSecretAsync()).ReturnsAsync("secret");

        await _decorator.GetSecretAsync();

        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task Invoke_Suppress_AsyncMethod_ReturnsValueFromInner()
    {
        _serviceMock.Setup(s => s.GetSecretAsync()).ReturnsAsync("secret");

        var result = await _decorator.GetSecretAsync();

        Assert.Equal("secret", result);
    }

    // Gap 2: interface properties are delegated to _inner with no logging

    [Fact]
    public void Property_ReadWrite_GetDelegatesToInner()
    {
        var inner = new DecoratorTestService { Status = "active" };
        var decorator = new DecoratorTestServiceLoggingDecorator(inner, _loggerFactory);

        Assert.Equal("active", decorator.Status);
    }

    [Fact]
    public void Property_ReadWrite_SetDelegatesToInner()
    {
        var inner = new DecoratorTestService();
        var decorator = new DecoratorTestServiceLoggingDecorator(inner, _loggerFactory);

        decorator.Status = "inactive";

        Assert.Equal("inactive", inner.Status);
    }

    [Fact]
    public void Property_ReadWrite_ProducesNoLogEntries()
    {
        var inner = new DecoratorTestService { Status = "active" };
        var decorator = new DecoratorTestServiceLoggingDecorator(inner, _loggerFactory);

        _ = decorator.Status;
        decorator.Status = "changed";

        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void Property_ReadOnly_GetDelegatesToInner()
    {
        var inner = new DecoratorTestService();
        var decorator = new DecoratorTestServiceLoggingDecorator(inner, _loggerFactory);

        Assert.Equal(inner.Label, decorator.Label);
    }

    [Fact]
    public void Indexer_GetDelegatesToInner()
    {
        var inner = new DecoratorTestService();
        var decorator = new DecoratorTestServiceLoggingDecorator(inner, _loggerFactory);

        Assert.Equal(inner[42], decorator[42]);
    }

    [Fact]
    public void Indexer_ProducesNoLogEntries()
    {
        var inner = new DecoratorTestService();
        var decorator = new DecoratorTestServiceLoggingDecorator(inner, _loggerFactory);

        _ = decorator[0];
        decorator[0] = "x";

        Assert.Empty(_logger.Entries);
    }
}

public class GenericLoggingDecoratorTests
{
    private readonly CapturingLogger _logger = new();
    private readonly CapturingLoggerFactory _loggerFactory;
    private readonly Mock<IGenericDecoratorTestService<string>> _serviceMock = new();
    private readonly IGenericDecoratorTestService<string> _decorator;

    public GenericLoggingDecoratorTests()
    {
        _loggerFactory = new CapturingLoggerFactory(_logger);
        _decorator = new GenericDecoratorTestServiceLoggingDecorator<string>(
            _serviceMock.Object, _loggerFactory);
    }

    // Gap 1: generic interface decorator carries the type parameter

    [Fact]
    public async Task Generic_FindAsync_LogsInvocation()
    {
        _serviceMock.Setup(s => s.FindAsync("id", It.IsAny<CancellationToken>()))
            .ReturnsAsync("result");

        await _decorator.FindAsync("id");

        Assert.True(_logger.HasEntry(LogLevel.Information, "Invoking"));
    }

    [Fact]
    public async Task Generic_FindAsync_LogsResult()
    {
        _serviceMock.Setup(s => s.FindAsync("id", It.IsAny<CancellationToken>()))
            .ReturnsAsync("result");

        await _decorator.FindAsync("id");

        Assert.True(_logger.HasEntry(LogLevel.Information, "returned"));
    }

    [Fact]
    public async Task Generic_FindAsync_ReturnsValueFromInner()
    {
        _serviceMock.Setup(s => s.FindAsync("id", It.IsAny<CancellationToken>()))
            .ReturnsAsync("expected");

        var result = await _decorator.FindAsync("id");

        Assert.Equal("expected", result);
    }

    [Fact]
    public async Task Generic_SaveAsync_LogsInvocationAndCompletion()
    {
        _serviceMock.Setup(s => s.SaveAsync("item", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _decorator.SaveAsync("item");

        Assert.True(_logger.HasEntry(LogLevel.Information, "Invoking"));
        Assert.True(_logger.HasEntry(LogLevel.Information, "completed"));
    }

    [Fact]
    public async Task Generic_WhenThrows_LogsError()
    {
        _serviceMock.Setup(s => s.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _decorator.FindAsync("id"));

        Assert.True(_logger.HasEntry(LogLevel.Error, "threw an exception"));
    }
}
