using System.Net;
using CoreDesign.Identity.Client;
using CoreDesign.Identity.Client.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CoreDesign.Identity.Client.Tests;

public class BearerTokenInjectionMiddlewareTests
{
    private static IdentityApiClient CreateIdentityClient(string token = "injected-token", HttpStatusCode status = HttpStatusCode.OK)
    {
        var payload = $$"""{"access_token":"{{token}}","expires_in":3600}""";
        var httpClient = new HttpClient(MockHttpMessageHandler.WithJson(payload, status))
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

    private static DefaultHttpContext LocalContext(string path = "/api/data")
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Loopback;
        ctx.Request.Path = path;
        return ctx;
    }

    private static DefaultHttpContext RemoteContext(string path = "/api/data")
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.5");
        ctx.Request.Path = path;
        return ctx;
    }

    [Fact]
    public async Task InvokeAsync_InjectsToken_ForLocalRequestWithoutAuth()
    {
        var identityClient = CreateIdentityClient("my-test-token");
        var logger = NullLogger<BearerTokenInjectionMiddleware>.Instance;
        var nextCalled = false;
        var middleware = new BearerTokenInjectionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, logger);
        var ctx = LocalContext();

        await middleware.InvokeAsync(ctx, identityClient);

        Assert.True(nextCalled);
        Assert.Equal("Bearer my-test-token", ctx.Request.Headers.Authorization.ToString());
    }

    [Fact]
    public async Task InvokeAsync_DoesNotInjectToken_WhenAuthHeaderAlreadyPresent()
    {
        var identityClient = CreateIdentityClient("should-not-inject");
        var logger = NullLogger<BearerTokenInjectionMiddleware>.Instance;
        var middleware = new BearerTokenInjectionMiddleware(_ => Task.CompletedTask, logger);
        var ctx = LocalContext();
        ctx.Request.Headers.Authorization = "Bearer existing-token";

        await middleware.InvokeAsync(ctx, identityClient);

        Assert.Equal("Bearer existing-token", ctx.Request.Headers.Authorization.ToString());
    }

    [Fact]
    public async Task InvokeAsync_DoesNotInjectToken_ForRemoteRequest()
    {
        var identityClient = CreateIdentityClient("remote-token");
        var logger = NullLogger<BearerTokenInjectionMiddleware>.Instance;
        var middleware = new BearerTokenInjectionMiddleware(_ => Task.CompletedTask, logger);
        var ctx = RemoteContext();

        await middleware.InvokeAsync(ctx, identityClient);

        Assert.Equal(string.Empty, ctx.Request.Headers.Authorization.ToString());
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger/index.html")]
    [InlineData("/scalar/v1")]
    [InlineData("/")]
    [InlineData("/health")]
    public async Task InvokeAsync_DoesNotInjectToken_ForPublicEndpoints(string path)
    {
        var identityClient = CreateIdentityClient("public-token");
        var logger = NullLogger<BearerTokenInjectionMiddleware>.Instance;
        var middleware = new BearerTokenInjectionMiddleware(_ => Task.CompletedTask, logger);
        var ctx = LocalContext(path);

        await middleware.InvokeAsync(ctx, identityClient);

        Assert.Equal(string.Empty, ctx.Request.Headers.Authorization.ToString());
    }

    [Fact]
    public async Task InvokeAsync_StillCallsNext_WhenTokenFetchFails()
    {
        var identityClient = CreateIdentityClient(status: HttpStatusCode.Unauthorized);
        var logger = NullLogger<BearerTokenInjectionMiddleware>.Instance;
        var nextCalled = false;
        var middleware = new BearerTokenInjectionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, logger);
        var ctx = LocalContext();

        await middleware.InvokeAsync(ctx, identityClient);

        Assert.True(nextCalled);
        Assert.Equal(string.Empty, ctx.Request.Headers.Authorization.ToString());
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext()
    {
        var identityClient = CreateIdentityClient();
        var logger = NullLogger<BearerTokenInjectionMiddleware>.Instance;
        var nextCalled = false;
        var middleware = new BearerTokenInjectionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, logger);

        await middleware.InvokeAsync(RemoteContext(), identityClient);

        Assert.True(nextCalled);
    }
}
