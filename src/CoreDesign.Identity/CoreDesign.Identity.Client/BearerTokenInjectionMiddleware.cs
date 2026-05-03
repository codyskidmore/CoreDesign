using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CoreDesign.Identity.Client;

internal sealed class BearerTokenInjectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BearerTokenInjectionMiddleware> _logger;

    public BearerTokenInjectionMiddleware(RequestDelegate next, ILogger<BearerTokenInjectionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IdentityApiClient identityClient)
    {
        if (string.IsNullOrEmpty(context.Request.Headers.Authorization) &&
            IsLocalRequest(context) &&
            !IsPublicEndpoint(context.Request.Path))
        {
            try
            {
                var token = await identityClient.GetAccessTokenAsync();
                context.Request.Headers.Authorization = $"Bearer {token}";
                _logger.LogDebug("Injected bearer token for local request to {Path}", context.Request.Path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to inject bearer token for local request to {Path}", context.Request.Path);
            }
        }

        await _next(context);
    }

    private static bool IsLocalRequest(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "";
        return remoteIp is "127.0.0.1" or "::1"
            || remoteIp.StartsWith("127.")
            || remoteIp.StartsWith("::ffff:127.");
    }

    private static bool IsPublicEndpoint(PathString path)
    {
        var pathStr = path.Value?.ToLowerInvariant() ?? "";
        return pathStr.StartsWith("/openapi")
            || pathStr.StartsWith("/swagger")
            || pathStr.StartsWith("/scalar")
            || pathStr == "/"
            || pathStr.StartsWith("/health");
    }
}
