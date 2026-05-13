namespace Sample.Api.WeatherForecasts.Handlers;

public static class DeleteWeatherForecastHandler
{
    public static async Task<IResult> HandleAsync(
        Ulid id,
        IWeatherForecastService service,
        HttpContext context,
        IOutputCacheStore outputCacheStore,
        CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, context.GetUserId(), ct);
        return result.Match<IResult>(
            success =>
            {
                outputCacheStore.EvictByTagAsync(nameof(CacheConfig.WeatherForecastCache), ct);
                return TypedResults.Ok();
            },
            _ => Results.NotFound($"Weather forecast id {id} not found for deletion"));
    }
}
