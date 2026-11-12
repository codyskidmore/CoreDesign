using System.Collections.Concurrent;
using System.Net;
using CoreDesign.Identity.Client.Tests.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CoreDesign.Identity.Client.Tests;

/// <summary>
/// End-to-end checks of UseLocalBearerTokenInjection: whether the middleware is added for each
/// combination of environment and EnableLocalTokenInjection, and what it logs at startup.
/// </summary>
public class LocalTokenInjectionTests
{
    private const string InjectedToken = "injected-token";
    private const string DeployedEnvironment = "DEV";
    private const string ProtectedPath = "/api/data";

    [Theory]
    [InlineData(null, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void IsEnabled_InDevelopment(bool? setting, bool expected) =>
        Assert.Equal(expected, LocalTokenInjection.IsEnabled(setting, Environment(Environments.Development)));

    [Theory]
    [InlineData(null, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void IsEnabled_OutsideDevelopment(bool? setting, bool expected) =>
        Assert.Equal(expected, LocalTokenInjection.IsEnabled(setting, Environment(DeployedEnvironment)));

    [Fact]
    public async Task Default_InDevelopment_InjectsToken()
    {
        var run = await RunAsync(Environments.Development, setting: null);

        Assert.Equal($"Bearer {InjectedToken}", run.Authorization);
        Assert.Empty(run.Warnings);
    }

    [Fact]
    public async Task Default_OutsideDevelopment_DoesNotInject()
    {
        var run = await RunAsync(DeployedEnvironment, setting: null);

        Assert.Equal(string.Empty, run.Authorization);
    }

    [Fact]
    public async Task ExplicitOff_InDevelopment_DoesNotInject()
    {
        var run = await RunAsync(Environments.Development, setting: false);

        Assert.Equal(string.Empty, run.Authorization);
    }

    [Fact]
    public async Task ExplicitOn_OutsideDevelopment_InjectsAndWarns()
    {
        var run = await RunAsync(DeployedEnvironment, setting: true);

        Assert.Equal($"Bearer {InjectedToken}", run.Authorization);
        Assert.Contains(run.Warnings, w => w.Contains("injection is ON") && w.Contains(DeployedEnvironment));
    }

    [Fact]
    public async Task ExplicitOn_RemoteRequest_DoesNotInject()
    {
        var run = await RunAsync(Environments.Development, setting: true, remoteAddress: IPAddress.Parse("203.0.113.5"));

        Assert.Equal(string.Empty, run.Authorization);
    }

    [Fact]
    public async Task ExistingToken_IsNotReplaced()
    {
        var run = await RunAsync(Environments.Development, setting: true, existingAuthorization: "Bearer real-token");

        Assert.Equal("Bearer real-token", run.Authorization);
    }

    [Fact]
    public async Task OptionsCallback_OverridesEnvironmentDefault()
    {
        var run = await RunAsync(Environments.Development, setting: null,
            configureOptions: o => o.EnableLocalTokenInjection = false);

        Assert.Equal(string.Empty, run.Authorization);
    }

    [Fact]
    public async Task Enabled_WithoutIdentityClient_SkipsAndWarns()
    {
        var run = await RunAsync(Environments.Development, setting: true, registerIdentityClient: false);

        Assert.Equal(string.Empty, run.Authorization);
        Assert.Contains(run.Warnings, w => w.Contains("AddIdentityClient was not called"));
    }

    private sealed record RunResult(string Authorization, IReadOnlyList<string> Warnings);

    private static async Task<RunResult> RunAsync(
        string environment,
        bool? setting,
        IPAddress? remoteAddress = null,
        string? existingAuthorization = null,
        bool registerIdentityClient = true,
        Action<IdentityApiOptions>? configureOptions = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();

        var logs = new ListLoggerProvider();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);

        if (setting is not null)
            builder.Configuration.AddInMemoryCollection(
                [new(IdentityApiOptions.EnableLocalTokenInjectionKey, setting.Value.ToString())]);

        if (configureOptions is not null)
            builder.Services.Configure(configureOptions);

        if (registerIdentityClient)
            builder.Services.AddSingleton(CreateIdentityClient());

        await using var app = builder.Build();

        // TestServer leaves RemoteIpAddress empty; set it the way a real connection would.
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = remoteAddress ?? IPAddress.Loopback;
            return next(context);
        });
        app.UseLocalBearerTokenInjection();
        app.MapGet(ProtectedPath, (HttpContext context) => context.Request.Headers.Authorization.ToString());

        await app.StartAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
        if (existingAuthorization is not null)
            request.Headers.TryAddWithoutValidation("Authorization", existingAuthorization);

        var response = await app.GetTestClient().SendAsync(request);
        var authorization = await response.Content.ReadAsStringAsync();

        await app.StopAsync();
        return new RunResult(authorization.Trim('"'), logs.Warnings);
    }

    private static IdentityApiClient CreateIdentityClient()
    {
        var payload = $$"""{"access_token":"{{InjectedToken}}","expires_in":3600}""";
        var httpClient = new HttpClient(MockHttpMessageHandler.WithJson(payload, HttpStatusCode.OK))
        {
            BaseAddress = new Uri("https://identity.test")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("coredesign-identity")).Returns(httpClient);
        return new IdentityApiClient(factory.Object, Options.Create(new IdentityApiOptions
        {
            BaseUrl = "https://identity.test",
            Username = "u",
            Password = "p"
        }));
    }

    private static IHostEnvironment Environment(string name) =>
        Mock.Of<IHostEnvironment>(e => e.EnvironmentName == name);

    private sealed class ListLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _warnings = new();

        public IReadOnlyList<string> Warnings => _warnings.ToArray();

        public ILogger CreateLogger(string categoryName) => new ListLogger(_warnings);

        public void Dispose() { }

        private sealed class ListLogger(ConcurrentQueue<string> warnings) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                    warnings.Enqueue(formatter(state, exception));
            }
        }
    }
}
