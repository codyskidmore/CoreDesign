using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace CoreDesign.UserJourney.Tests;

public class UserJourneyMiddlewareTests
{
    private static Activity? RequestTo(IReadOnlyList<Activity> exported, string path) =>
        exported.FirstOrDefault(a => a.Source.Name == TestApp.AspNetCoreSource && a.OperationName.Contains("HttpRequestIn")
                                     && (string?)a.GetTagItem("test.path") == path);

    private static Action<WebApplication> TagPath => app => app.Use((context, next) =>
    {
        Activity.Current?.SetTag("test.path", context.Request.Path.Value);
        return next(context);
    });

    [Fact]
    public async Task SignedInRequest_IsTaggedWithUser()
    {
        await using var app = await TestApp.StartAsync(endpoints: TagPath);

        await app.Client.SendAsync(app.Request(HttpMethod.Get, "/page"));
        var exported = await app.ExportedAsync(e => RequestTo(e, "/page") is not null);
        var request = RequestTo(exported, "/page")!;

        Assert.Equal(TestApp.UserId, request.GetTagItem(UserJourneyTags.EndUserId));
        Assert.Equal(TestApp.UserEmail, request.GetTagItem(UserJourneyTags.UserEmail));
        Assert.Equal(TestApp.UserName, request.GetTagItem(UserJourneyTags.UserName));
    }

    [Fact]
    public async Task AnonymousRequest_HasNoUserTags()
    {
        await using var app = await TestApp.StartAsync(endpoints: TagPath);

        await app.Client.SendAsync(app.Request(HttpMethod.Get, "/page", signedIn: false));
        var exported = await app.ExportedAsync(e => RequestTo(e, "/page") is not null);
        var request = RequestTo(exported, "/page")!;

        Assert.Null(request.GetTagItem(UserJourneyTags.EndUserId));
        Assert.Null(request.GetTagItem(UserJourneyTags.UserEmail));
    }

    [Fact]
    public async Task CaptureFlagsOff_OmitEmailAndName()
    {
        await using var app = await TestApp.StartAsync(o =>
        {
            o.CaptureUserEmail = false;
            o.CaptureUserName = false;
        }, endpoints: TagPath);

        await app.Client.SendAsync(app.Request(HttpMethod.Get, "/page"));
        var exported = await app.ExportedAsync(e => RequestTo(e, "/page") is not null);
        var request = RequestTo(exported, "/page")!;

        Assert.Equal(TestApp.UserId, request.GetTagItem(UserJourneyTags.EndUserId));
        Assert.Null(request.GetTagItem(UserJourneyTags.UserEmail));
        Assert.Null(request.GetTagItem(UserJourneyTags.UserName));
    }

    [Fact]
    public async Task ClientIp_ComesFromConnection_ByDefault()
    {
        await using var app = await TestApp.StartAsync(remoteAddress: IPAddress.Parse("::ffff:203.0.113.9"), endpoints: TagPath);

        await app.Client.SendAsync(app.Request(HttpMethod.Get, "/page"));
        var exported = await app.ExportedAsync(e => RequestTo(e, "/page") is not null);

        Assert.Equal("203.0.113.9", RequestTo(exported, "/page")!.GetTagItem(UserJourneyTags.ClientIp));
    }

    [Fact]
    public async Task ClientIp_ComesFromConfiguredHeader()
    {
        await using var app = await TestApp.StartAsync(o =>
        {
            o.ClientIpHeaders = ["CF-Connecting-IP"];
            o.ClientIpTag = "client_ip_actual";
        }, endpoints: TagPath);

        var request = app.Request(HttpMethod.Get, "/page");
        request.Headers.Add("CF-Connecting-IP", "192.0.2.44");
        await app.Client.SendAsync(request);
        var exported = await app.ExportedAsync(e => RequestTo(e, "/page") is not null);

        Assert.Equal("192.0.2.44", RequestTo(exported, "/page")!.GetTagItem("client_ip_actual"));
    }

    [Fact]
    public async Task ClientIp_InvalidHeader_FallsBackToConnection()
    {
        await using var app = await TestApp.StartAsync(o => o.ClientIpHeaders = ["CF-Connecting-IP"], endpoints: TagPath);

        var request = app.Request(HttpMethod.Get, "/page");
        request.Headers.Add("CF-Connecting-IP", "not-an-ip");
        await app.Client.SendAsync(request);
        var exported = await app.ExportedAsync(e => RequestTo(e, "/page") is not null);

        Assert.Equal("198.51.100.7", RequestTo(exported, "/page")!.GetTagItem(UserJourneyTags.ClientIp));
    }

    [Fact]
    public async Task CaptureClientIpOff_OmitsIp()
    {
        await using var app = await TestApp.StartAsync(o => o.CaptureClientIp = false, endpoints: TagPath);

        await app.Client.SendAsync(app.Request(HttpMethod.Get, "/page"));
        var exported = await app.ExportedAsync(e => RequestTo(e, "/page") is not null);

        Assert.Null(RequestTo(exported, "/page")!.GetTagItem(UserJourneyTags.ClientIp));
    }

    [Fact]
    public async Task ExcludedPath_IsNotExported()
    {
        await using var app = await TestApp.StartAsync(endpoints: TagPath);

        await app.Client.SendAsync(app.Request(HttpMethod.Get, "/alive"));
        await app.Client.SendAsync(app.Request(HttpMethod.Get, "/page"));
        var exported = await app.ExportedAsync(e => RequestTo(e, "/page") is not null);

        Assert.Null(RequestTo(exported, "/alive"));
    }

    [Fact]
    public async Task QueryString_IsRemoved()
    {
        await using var app = await TestApp.StartAsync(endpoints: application => application.Use((context, next) =>
        {
            // Stand-in for the ASP.NET Core instrumentation, which writes these tags.
            Activity.Current?.SetTag("test.path", context.Request.Path.Value);
            Activity.Current?.SetTag("url.query", "?q=secret");
            Activity.Current?.SetTag("url.full", "https://host/page?q=secret");
            return next(context);
        }));

        await app.Client.SendAsync(app.Request(HttpMethod.Get, "/page?q=secret"));
        var exported = await app.ExportedAsync(e => RequestTo(e, "/page") is not null);
        var request = RequestTo(exported, "/page")!;

        Assert.Null(request.GetTagItem("url.query"));
        Assert.Equal("https://host/page", request.GetTagItem("url.full"));
    }

    [Fact]
    public async Task JourneyViewed_TagsTheRequest()
    {
        await using var app = await TestApp.StartAsync(endpoints: application =>
        {
            application.Use((context, next) =>
            {
                Activity.Current?.SetTag("test.path", context.Request.Path.Value);
                return next(context);
            });
            application.MapGet("/product", () =>
            {
                Journey.Viewed("Product", "Sprocket");
                return "ok";
            });
        });

        await app.Client.SendAsync(app.Request(HttpMethod.Get, "/product"));
        var exported = await app.ExportedAsync(e => RequestTo(e, "/product") is not null);
        var request = RequestTo(exported, "/product")!;

        Assert.Equal("Product", request.GetTagItem(UserJourneyTags.ViewedItemType));
        Assert.Equal("Sprocket", request.GetTagItem(UserJourneyTags.ViewedItem));
    }
}
