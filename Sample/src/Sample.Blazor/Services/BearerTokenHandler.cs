using System.Net.Http.Headers;

namespace Sample.Blazor.Services;

/// <summary>
/// Delegating handler that attaches the OIDC access_token stored in the auth cookie
/// as a Bearer token on every outbound request to Sample.Api.
/// </summary>
public sealed class BearerTokenHandler(IHttpContextAccessor httpContextAccessor)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is not null)
        {
            var accessToken = await httpContext.GetTokenAsync("access_token");

            if (!string.IsNullOrEmpty(accessToken))
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

