using System.Diagnostics;
using CoreDesign.UserJourney.Blazor;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace CoreDesign.UserJourney.Tests;

public class BlazorTests
{
    private const string TestSource = "CoreDesign.UserJourney.Tests.Blazor";

    private static (TracerProvider Provider, List<Activity> Exported, CircuitUserRegistry Registry) CreatePipeline()
    {
        var registry = new CircuitUserRegistry();
        var exported = new List<Activity>();
        var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(TestSource)
            .AddProcessor(new UserJourneyBlazorProcessor(registry)) // before the exporter, as documented
            .AddInMemoryExporter(exported)
            .Build()!;
        return (provider, exported, registry);
    }

    private static void RunEvent(string circuitId, string? existingUser = null)
    {
        using var source = new ActivitySource(TestSource);
        using var activity = source.StartActivity("Event OnClick");
        activity!.SetTag(BlazorTraceTags.CircuitId, circuitId);
        if (existingUser is not null)
            activity.SetTag(UserJourneyTags.EndUserId, existingUser);
    }

    [Fact]
    public void EventOnKnownCircuit_IsTaggedWithItsUser()
    {
        var (provider, exported, registry) = CreatePipeline();
        using (provider)
        {
            registry.Register("circuit-1", "user-1");
            RunEvent("circuit-1");
        }

        Assert.Equal("user-1", Assert.Single(exported).GetTagItem(UserJourneyTags.EndUserId));
    }

    [Fact]
    public void EventOnUnknownCircuit_IsLeftAlone()
    {
        var (provider, exported, _) = CreatePipeline();
        using (provider)
            RunEvent("anonymous-circuit");

        Assert.Null(Assert.Single(exported).GetTagItem(UserJourneyTags.EndUserId));
    }

    [Fact]
    public void ExistingUserTag_IsNotOverwritten()
    {
        var (provider, exported, registry) = CreatePipeline();
        using (provider)
        {
            registry.Register("circuit-1", "user-1");
            RunEvent("circuit-1", existingUser: "already-set");
        }

        Assert.Equal("already-set", Assert.Single(exported).GetTagItem(UserJourneyTags.EndUserId));
    }

    [Fact]
    public void ActivityWithoutCircuit_IsLeftAlone()
    {
        var (provider, exported, registry) = CreatePipeline();
        using (provider)
        {
            registry.Register("circuit-1", "user-1");
            using var source = new ActivitySource(TestSource);
            using var activity = source.StartActivity("GET /home");
        }

        Assert.Null(Assert.Single(exported).GetTagItem(UserJourneyTags.EndUserId));
    }

    [Fact]
    public void Registry_ForgetsClosedCircuits()
    {
        var registry = new CircuitUserRegistry();
        registry.Register("circuit-1", "user-1");
        registry.Remove("circuit-1");

        Assert.False(registry.TryGetUser("circuit-1", out _));
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void AddUserJourneyBlazor_RegistersCircuitHandlerOnce()
    {
        var services = new ServiceCollection();
        services.AddUserJourneyBlazor();
        services.AddUserJourneyBlazor();

        Assert.Single(services, d => d.ServiceType == typeof(CircuitHandler));
        Assert.Single(services, d => d.ServiceType == typeof(CircuitUserRegistry));
    }
}
