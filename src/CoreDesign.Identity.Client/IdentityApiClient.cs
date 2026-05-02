using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreDesign.Identity.Client;

public sealed class IdentityApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IdentityApiOptions _options;
    private readonly ILogger<IdentityApiClient>? _logger;
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public IdentityApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<IdentityApiOptions> options,
        ILogger<IdentityApiClient>? logger = null)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
        {
            _logger?.LogDebug("Using cached bearer token");
            return _cachedToken;
        }

        await _lock.WaitAsync();
        try
        {
            if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
                return _cachedToken;

            _logger?.LogInformation("Fetching new bearer token from Identity API at {BaseUrl}/connect/token", _options.BaseUrl);

            var client = _httpClientFactory.CreateClient("dcne-identity");
            var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("username", _options.Username),
                new KeyValuePair<string, string>("password", _options.Password)
            ]));

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogError(
                    "Failed to fetch bearer token. Status: {StatusCode}, Content: {ErrorContent}",
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<IdentityTokenPayload>()
                ?? throw new InvalidOperationException("Empty token response from Identity API");

            _cachedToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);

            _logger?.LogInformation(
                "Successfully fetched bearer token. Expires in {ExpiresIn} seconds",
                tokenResponse.ExpiresIn);

            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }
}

file record IdentityTokenPayload(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn
);
