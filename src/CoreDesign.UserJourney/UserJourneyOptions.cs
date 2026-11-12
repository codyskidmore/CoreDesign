using System.Security.Claims;

namespace CoreDesign.UserJourney;

/// <summary>Options for <c>AddUserJourney</c>.</summary>
public sealed class UserJourneyOptions
{
    /// <summary>The configuration section these options can be bound from.</summary>
    public const string SectionName = "UserJourney";

    /// <summary>
    /// Claims tried, in order, for the user's stable ID. The defaults cover Entra (<c>oid</c> and its long
    /// form), the CoreDesign identity server (<c>oid</c>) and generic OIDC providers (<c>sub</c>).
    /// </summary>
    public List<string> UserIdClaimTypes { get; set; } =
    [
        "oid",
        "http://schemas.microsoft.com/identity/claims/objectidentifier",
        "sub",
        ClaimTypes.NameIdentifier,
    ];

    /// <summary>Claims tried, in order, for the user's email or user name.</summary>
    public List<string> UserEmailClaimTypes { get; set; } = ["preferred_username", "email", ClaimTypes.Email];

    /// <summary>Claims tried, in order, for the user's display name.</summary>
    public List<string> UserNameClaimTypes { get; set; } = ["name", ClaimTypes.Name];

    /// <summary>Record the user's email as <see cref="UserJourneyTags.UserEmail"/>. Default true.</summary>
    public bool CaptureUserEmail { get; set; } = true;

    /// <summary>Record the user's display name as <see cref="UserJourneyTags.UserName"/>. Default true.</summary>
    public bool CaptureUserName { get; set; } = true;

    /// <summary>Record the client's IP address. Default true.</summary>
    public bool CaptureClientIp { get; set; } = true;

    /// <summary>
    /// Request headers that carry the real client IP, tried in order before the connection address, for
    /// example <c>CF-Connecting-IP</c> behind Cloudflare. Leave empty when <c>UseForwardedHeaders</c>
    /// already sets the connection address. Only list headers your proxy sets and overwrites, because a
    /// client can send any header.
    /// </summary>
    public List<string> ClientIpHeaders { get; set; } = [];

    /// <summary>
    /// Tag name for the client IP. Application Insights fills its built-in client IP field from its own
    /// ingestion pipeline, not from tags, so the IP is recorded as a custom dimension under this name.
    /// </summary>
    public string ClientIpTag { get; set; } = UserJourneyTags.ClientIp;

    /// <summary>
    /// Request paths whose telemetry is dropped entirely, such as container health probes. Matched as
    /// path prefixes, case-insensitively.
    /// </summary>
    public List<string> ExcludedPaths { get; set; } = ["/alive", "/health"];

    /// <summary>
    /// Remove query strings from recorded request URLs. Default true, because query strings often hold
    /// search terms, tokens and other personal data.
    /// </summary>
    public bool RemoveQueryStrings { get; set; } = true;

    /// <summary>Path of the endpoint mapped by <c>MapUserJourneyEvents</c> for browser apps.</summary>
    public string BrowserEventsPath { get; set; } = "/user-journey/events";

    /// <summary>Maximum events accepted in one browser batch. Larger batches are rejected.</summary>
    public int MaxBrowserEventsPerBatch { get; set; } = 50;
}
