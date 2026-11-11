using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Options;
using OpenTelemetry;

namespace CoreDesign.UserJourney.Blazor;

/// <summary>Tag names ASP.NET Core writes on the traces it creates for Blazor Server.</summary>
public static class BlazorTraceTags
{
    /// <summary>The circuit (one browser tab's live connection) a Blazor event or navigation belongs to.</summary>
    public const string CircuitId = "aspnetcore.components.circuit.id";

    /// <summary>ActivitySource for Blazor component events and navigation.</summary>
    public const string ComponentsActivitySource = "Microsoft.AspNetCore.Components";

    /// <summary>ActivitySource for Blazor Server circuits.</summary>
    public const string ServerActivitySource = "Microsoft.AspNetCore.Components.Server";
}

/// <summary>Maps each open Blazor circuit to the signed-in user who owns it.</summary>
public sealed class CircuitUserRegistry
{
    private readonly ConcurrentDictionary<string, string> _users = new();

    public void Register(string circuitId, string userId) => _users[circuitId] = userId;

    public void Remove(string circuitId) => _users.TryRemove(circuitId, out _);

    public bool TryGetUser(string circuitId, out string? userId) => _users.TryGetValue(circuitId, out userId);

    /// <summary>Number of circuits currently mapped to a user.</summary>
    public int Count => _users.Count;
}

/// <summary>Records the signed-in user when a circuit opens and forgets it when the circuit closes.</summary>
internal sealed class UserJourneyCircuitHandler(
    AuthenticationStateProvider authenticationStateProvider,
    CircuitUserRegistry registry,
    IOptions<UserJourneyOptions> options) : CircuitHandler
{
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        if (UserJourneyTelemetry.GetUserId(state.User, options.Value) is { } userId)
            registry.Register(circuit.Id, userId);
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        registry.Remove(circuit.Id);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Adds the user ID to Blazor event and navigation traces, which run over SignalR and so never pass
/// through HTTP middleware. It works when the trace ends, so it must be registered before any exporter.
/// </summary>
internal sealed class UserJourneyBlazorProcessor(CircuitUserRegistry registry) : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        if (activity.GetTagItem(UserJourneyTags.EndUserId) is null
            && activity.GetTagItem(BlazorTraceTags.CircuitId) is string circuitId
            && registry.TryGetUser(circuitId, out var userId))
        {
            activity.SetTag(UserJourneyTags.EndUserId, userId);
        }
    }
}
