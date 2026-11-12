using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CoreDesign.UserJourney;

/// <summary>Kinds of event a browser app can send.</summary>
public static class BrowserEventKinds
{
    /// <summary>The user opened a page (a client-side route change).</summary>
    public const string Page = "page";

    /// <summary>The user clicked an element marked with a <c>data-journey</c> name.</summary>
    public const string Click = "click";
}

/// <summary>One page view or click sent by a browser app.</summary>
/// <param name="Kind"><see cref="BrowserEventKinds.Page"/> or <see cref="BrowserEventKinds.Click"/>.</param>
/// <param name="Path">The page path. Any query string or fragment is removed before recording.</param>
/// <param name="Target">For clicks, the element's <c>data-journey</c> name.</param>
/// <param name="ItemType">Optional kind of item the page showed, as for <see cref="Journey.Viewed"/>.</param>
/// <param name="Item">Optional item the page showed.</param>
public sealed record BrowserJourneyEvent(string Kind, string Path, string? Target = null, string? ItemType = null, string? Item = null);

internal static class BrowserEventHandler
{
    private const int MaxPathLength = 512;
    private const int MaxNameLength = 128;
    private const int MaxItemLength = 256;

    public static IResult Handle(HttpContext context, BrowserJourneyEvent[]? events, IOptions<UserJourneyOptions> options)
    {
        var settings = options.Value;
        if (events is null || events.Length == 0)
            return Results.BadRequest("No events.");
        if (events.Length > settings.MaxBrowserEventsPerBatch)
            return Results.BadRequest($"At most {settings.MaxBrowserEventsPerBatch} events per request.");

        foreach (var journeyEvent in events)
        {
            if (Validate(journeyEvent) is { } error)
                return Results.BadRequest(error);
        }

        foreach (var journeyEvent in events)
            Record(journeyEvent, context, settings);

        return Results.Accepted();
    }

    private static string? Validate(BrowserJourneyEvent journeyEvent)
    {
        if (journeyEvent.Kind is not (BrowserEventKinds.Page or BrowserEventKinds.Click))
            return $"Kind must be '{BrowserEventKinds.Page}' or '{BrowserEventKinds.Click}'.";
        if (string.IsNullOrWhiteSpace(journeyEvent.Path) || !journeyEvent.Path.StartsWith('/') || journeyEvent.Path.Length > MaxPathLength)
            return $"Path must start with '/' and be at most {MaxPathLength} characters.";
        if (journeyEvent.Kind == BrowserEventKinds.Click && string.IsNullOrWhiteSpace(journeyEvent.Target))
            return "Click events need a Target.";
        if (journeyEvent.Target?.Length > MaxNameLength || journeyEvent.ItemType?.Length > MaxNameLength)
            return $"Target and ItemType must be at most {MaxNameLength} characters.";
        if (journeyEvent.Item?.Length > MaxItemLength)
            return $"Item must be at most {MaxItemLength} characters.";
        return null;
    }

    private static void Record(BrowserJourneyEvent journeyEvent, HttpContext context, UserJourneyOptions settings)
    {
        var path = StripQueryAndFragment(journeyEvent.Path);
        var name = journeyEvent.Kind == BrowserEventKinds.Page ? $"Page {path}" : $"Click {journeyEvent.Target}";

        using var activity = UserJourneyTelemetry.Source.StartActivity(name, ActivityKind.Internal);
        if (activity is null)
            return;

        activity.SetTag(UserJourneyTags.BrowserEventKind, journeyEvent.Kind);
        activity.SetTag(UserJourneyTags.BrowserPath, path);
        if (journeyEvent.Target is not null)
            activity.SetTag(UserJourneyTags.BrowserTarget, journeyEvent.Target);
        if (!string.IsNullOrWhiteSpace(journeyEvent.Item))
        {
            activity.SetTag(UserJourneyTags.ViewedItemType, journeyEvent.ItemType);
            activity.SetTag(UserJourneyTags.ViewedItem, journeyEvent.Item);
        }

        UserJourneyTelemetry.TagUser(activity, context.User, settings);
    }

    private static string StripQueryAndFragment(string path)
    {
        var end = path.IndexOfAny(['?', '#']);
        return end >= 0 ? path[..end] : path;
    }
}
