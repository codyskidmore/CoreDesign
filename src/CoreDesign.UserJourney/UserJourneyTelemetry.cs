using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace CoreDesign.UserJourney;

/// <summary>The ActivitySource CoreDesign.UserJourney records browser events on.</summary>
public static class UserJourneyTelemetry
{
    /// <summary>Name of the ActivitySource; <c>AddUserJourney</c> adds it to OpenTelemetry tracing.</summary>
    public const string ActivitySourceName = "CoreDesign.UserJourney";

    internal static readonly ActivitySource Source = new(ActivitySourceName);

    /// <summary>Writes the signed-in user's ID, email and name to <paramref name="activity"/>.</summary>
    internal static void TagUser(Activity activity, ClaimsPrincipal user, UserJourneyOptions options)
    {
        if (user.Identity?.IsAuthenticated != true)
            return;

        if (FirstClaim(user, options.UserIdClaimTypes) is { } id)
            activity.SetTag(UserJourneyTags.EndUserId, id);

        if (options.CaptureUserEmail && FirstClaim(user, options.UserEmailClaimTypes) is { } email)
            activity.SetTag(UserJourneyTags.UserEmail, email);

        if (options.CaptureUserName && FirstClaim(user, options.UserNameClaimTypes) is { } name)
            activity.SetTag(UserJourneyTags.UserName, name);
    }

    /// <summary>Returns the user's stable ID from the first configured claim present, or null.</summary>
    public static string? GetUserId(ClaimsPrincipal user, UserJourneyOptions options) =>
        user.Identity?.IsAuthenticated == true ? FirstClaim(user, options.UserIdClaimTypes) : null;

    /// <summary>The client IP from the first configured header with a valid address, else the connection.</summary>
    internal static string? ResolveClientIp(HttpContext context, UserJourneyOptions options)
    {
        foreach (var header in options.ClientIpHeaders)
        {
            var value = context.Request.Headers[header].ToString();
            var first = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (first is not null && IPAddress.TryParse(first, out var parsed))
                return Normalize(parsed).ToString();
        }

        return context.Connection.RemoteIpAddress is { } remote ? Normalize(remote).ToString() : null;
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static string? FirstClaim(ClaimsPrincipal user, IEnumerable<string> claimTypes)
    {
        foreach (var type in claimTypes)
        {
            var value = user.FindFirst(type)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
