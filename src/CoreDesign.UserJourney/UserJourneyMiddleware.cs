using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CoreDesign.UserJourney;

/// <summary>
/// Tags the request's trace with the signed-in user and client IP, drops excluded paths such as health
/// probes, and removes query strings. Runs after authentication so <see cref="HttpContext.User"/> is set.
/// </summary>
internal sealed class UserJourneyMiddleware(RequestDelegate next, IOptions<UserJourneyOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var settings = options.Value;
        var activity = Activity.Current;

        if (activity is not null && IsExcluded(context.Request.Path, settings))
        {
            // Exporters skip activities that are not recorded, so the probe never reaches the backend.
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            activity.IsAllDataRequested = false;
            await next(context);
            return;
        }

        if (activity is not null)
        {
            if (settings.CaptureClientIp && UserJourneyTelemetry.ResolveClientIp(context, settings) is { } ip)
                activity.SetTag(settings.ClientIpTag, ip);

            UserJourneyTelemetry.TagUser(activity, context.User, settings);
        }

        try
        {
            await next(context);
        }
        finally
        {
            if (activity is not null && settings.RemoveQueryStrings)
                RemoveQuery(activity);
        }
    }

    private static bool IsExcluded(PathString path, UserJourneyOptions settings)
    {
        foreach (var excluded in settings.ExcludedPaths)
        {
            if (path.StartsWithSegments(excluded, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void RemoveQuery(Activity activity)
    {
        activity.SetTag(UserJourneyTags.UrlQuery, null);

        if (activity.GetTagItem(UserJourneyTags.UrlFull) is string full && full.IndexOf('?') is var index and >= 0)
            activity.SetTag(UserJourneyTags.UrlFull, full[..index]);
    }
}
