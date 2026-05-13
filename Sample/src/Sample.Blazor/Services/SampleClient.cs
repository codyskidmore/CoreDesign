using System.Net.Http.Json;

namespace Sample.Blazor.Services;

/// <summary>
/// Typed HTTP client for communicating with <c>Sample.Api</c>.
/// The Bearer token is injected automatically by <see cref="BearerTokenHandler"/>.
/// </summary>
public sealed class SampleClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<WeatherForecastResponse>> GetWeatherForecastsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<WeatherForecastResponse[]>(
            "/weatherforecasts",
            cancellationToken);

        return result ?? [];
    }
}

