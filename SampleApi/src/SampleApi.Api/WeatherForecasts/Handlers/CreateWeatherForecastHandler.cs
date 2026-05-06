namespace SampleApi.Api.WeatherForecasts.Handlers;

public static class CreateWeatherForecastHandler
{
    public static async Task<IResult> HandleAsync(
        [FromBody] WeatherForecastRequest request,
        IWeatherForecastService service,
        HttpContext context,
        IOutputCacheStore outputCacheStore,
        CancellationToken ct)
    {
        var result = await service.CreateAsync(context.GetUserId(), request, ct);
        return result.Match<IResult>(
            forecast =>
            {
                var response = forecast.ToResponse();
                outputCacheStore.EvictByTagAsync(nameof(CacheConfig.WeatherForecastCache), ct);
                return Results.CreatedAtRoute(GetWeatherForecastEndpoint.Name,
                    new { id = response.Id }, response);
            },
            error => Results.BadRequest(error));
    }
}
