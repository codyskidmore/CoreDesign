using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;

namespace CoreDesign.UserJourney.Tests;

public class BrowserEventTests
{
    private const string EventsPath = "/user-journey/events";

    private static Action<WebApplication> MapEvents => app => app.MapUserJourneyEvents();

    private static IReadOnlyList<Activity> JourneyEvents(IReadOnlyList<Activity> exported) =>
        exported.Where(a => a.Source.Name == UserJourneyTelemetry.ActivitySourceName).ToList();

    private static async Task<HttpResponseMessage> PostAsync(TestApp app, object events, bool signedIn = true)
    {
        var request = app.Request(HttpMethod.Post, EventsPath, signedIn);
        request.Content = JsonContent.Create(events);
        return await app.Client.SendAsync(request);
    }

    [Fact]
    public async Task PageAndClick_AreRecorded_WithUser()
    {
        await using var app = await TestApp.StartAsync(endpoints: MapEvents);

        var response = await PostAsync(app, new[]
        {
            new BrowserJourneyEvent(BrowserEventKinds.Page, "/products/42?ref=email#top", ItemType: "Product", Item: "Sprocket"),
            new BrowserJourneyEvent(BrowserEventKinds.Click, "/products/42", Target: "add-to-cart"),
        });
        var exported = await app.ExportedAsync(e => JourneyEvents(e).Count == 2);
        var events = JourneyEvents(exported);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var page = Assert.Single(events, a => a.DisplayName == "Page /products/42");
        Assert.Equal("/products/42", page.GetTagItem(UserJourneyTags.BrowserPath));
        Assert.Equal("Sprocket", page.GetTagItem(UserJourneyTags.ViewedItem));
        Assert.Equal(TestApp.UserId, page.GetTagItem(UserJourneyTags.EndUserId));

        var click = Assert.Single(events, a => a.DisplayName == "Click add-to-cart");
        Assert.Equal(BrowserEventKinds.Click, click.GetTagItem(UserJourneyTags.BrowserEventKind));
        Assert.Equal(TestApp.UserId, click.GetTagItem(UserJourneyTags.EndUserId));
    }

    [Fact]
    public async Task Events_ShareTheRequestTrace()
    {
        await using var app = await TestApp.StartAsync(endpoints: MapEvents);

        await PostAsync(app, new[] { new BrowserJourneyEvent(BrowserEventKinds.Page, "/home") });
        var exported = await app.ExportedAsync(e => JourneyEvents(e).Count == 1 && e.Any(a => a.Source.Name == TestApp.AspNetCoreSource));

        var page = Assert.Single(JourneyEvents(exported));
        var request = exported.First(a => a.Source.Name == TestApp.AspNetCoreSource);
        Assert.Equal(request.TraceId, page.TraceId);
    }

    [Fact]
    public async Task Anonymous_IsRejected()
    {
        await using var app = await TestApp.StartAsync(endpoints: MapEvents);

        var response = await PostAsync(app, new[] { new BrowserJourneyEvent(BrowserEventKinds.Page, "/home") }, signedIn: false);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("scroll", "/home", null)]
    [InlineData(BrowserEventKinds.Page, "home", null)]
    [InlineData(BrowserEventKinds.Click, "/home", null)]
    public async Task InvalidEvent_IsRejected_AndNothingRecorded(string kind, string path, string? target)
    {
        await using var app = await TestApp.StartAsync(endpoints: MapEvents);

        var response = await PostAsync(app, new[]
        {
            new BrowserJourneyEvent(BrowserEventKinds.Page, "/valid"),
            new BrowserJourneyEvent(kind, path, target),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var exported = await app.ExportedAsync(e => e.Any(a => a.Source.Name == TestApp.AspNetCoreSource));
        Assert.Empty(JourneyEvents(exported));
    }

    [Fact]
    public async Task OversizedBatch_IsRejected()
    {
        await using var app = await TestApp.StartAsync(o => o.MaxBrowserEventsPerBatch = 2, endpoints: MapEvents);

        var events = Enumerable.Range(0, 3).Select(i => new BrowserJourneyEvent(BrowserEventKinds.Page, $"/p{i}")).ToArray();
        var response = await PostAsync(app, events);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CustomPath_IsUsed()
    {
        await using var app = await TestApp.StartAsync(o => o.BrowserEventsPath = "/telemetry/journey", endpoints: MapEvents);

        var request = app.Request(HttpMethod.Post, "/telemetry/journey");
        request.Content = JsonContent.Create(new[] { new BrowserJourneyEvent(BrowserEventKinds.Page, "/home") });
        var response = await app.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
