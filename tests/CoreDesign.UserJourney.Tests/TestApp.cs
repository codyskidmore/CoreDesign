using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using CoreDesign.UserJourney;

namespace CoreDesign.UserJourney.Tests;

/// <summary>
/// A minimal web app wired the way the docs describe: AddUserJourney before the exporter, UseUserJourney
/// after authentication. Requests carrying the <see cref="UserHeader"/> header are signed in as that user.
/// </summary>
internal sealed class TestApp : IAsyncDisposable
{
    public const string UserHeader = "X-Test-User";
    public const string UserId = "11111111-1111-1111-1111-111111111111";
    public const string UserEmail = "admin@example.local";
    public const string UserName = "Admin User";
    public const string AspNetCoreSource = "Microsoft.AspNetCore";

    private readonly WebApplication _app;
    private readonly List<Activity> _exported;

    private TestApp(WebApplication app, List<Activity> exported)
    {
        _app = app;
        _exported = exported;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public static async Task<TestApp> StartAsync(
        Action<UserJourneyOptions>? configure = null,
        IPAddress? remoteAddress = null,
        Action<WebApplication>? endpoints = null)
    {
        var exported = new List<Activity>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddUserJourney(configure);
        builder.Services.AddOpenTelemetry().WithTracing(t => t
            .AddSource(AspNetCoreSource)
            .AddInMemoryExporter(exported));

        builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = remoteAddress ?? IPAddress.Parse("198.51.100.7");
            return next(context);
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseUserJourney();
        app.MapGet("/page", () => "ok");
        app.MapGet("/alive", () => "ok");
        endpoints?.Invoke(app);

        await app.StartAsync();
        return new TestApp(app, exported);
    }

    public HttpRequestMessage Request(HttpMethod method, string path, bool signedIn = true)
    {
        var request = new HttpRequestMessage(method, path);
        if (signedIn)
            request.Headers.Add(UserHeader, UserId);
        return request;
    }

    /// <summary>Exported activities; waits briefly because the request activity ends after the response.</summary>
    public async Task<IReadOnlyList<Activity>> ExportedAsync(Func<IReadOnlyList<Activity>, bool> until)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            Activity[] snapshot;
            lock (_exported)
                snapshot = _exported.ToArray();
            if (until(snapshot))
                return snapshot;
            await Task.Delay(20);
        }

        lock (_exported)
            return _exported.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var id))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(
            [
                new Claim("oid", id.ToString()),
                new Claim("preferred_username", UserEmail),
                new Claim("name", UserName),
            ], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
