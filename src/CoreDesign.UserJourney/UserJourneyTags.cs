namespace CoreDesign.UserJourney;

/// <summary>Telemetry tag names written by CoreDesign.UserJourney.</summary>
public static class UserJourneyTags
{
    /// <summary>
    /// The user's stable ID (OpenTelemetry semantic convention). Application Insights maps it to
    /// <c>user_AuthenticatedId</c>.
    /// </summary>
    public const string EndUserId = "enduser.id";

    /// <summary>The user's email or user name.</summary>
    public const string UserEmail = "user.email";

    /// <summary>The user's display name.</summary>
    public const string UserName = "user.name";

    /// <summary>Default tag for the real client IP address.</summary>
    public const string ClientIp = "user_journey.client_ip";

    /// <summary>The kind of item a page showed, for example <c>Product</c>.</summary>
    public const string ViewedItemType = "view.type";

    /// <summary>The item a page showed, for example a product name or ID.</summary>
    public const string ViewedItem = "view.item";

    /// <summary>Browser event kind: <c>page</c> or <c>click</c>.</summary>
    public const string BrowserEventKind = "user_journey.event";

    /// <summary>The page path a browser event happened on, without query string.</summary>
    public const string BrowserPath = "user_journey.path";

    /// <summary>The element a browser click event targeted (its <c>data-journey</c> name).</summary>
    public const string BrowserTarget = "user_journey.target";

    /// <summary>Request URL query string (OpenTelemetry semantic convention); removed when configured.</summary>
    internal const string UrlQuery = "url.query";

    /// <summary>Full request URL (OpenTelemetry semantic convention); its query is removed when configured.</summary>
    internal const string UrlFull = "url.full";
}
