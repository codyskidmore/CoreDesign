namespace Sample.Api.WeatherForecasts.Handlers;

public static class UpdateWeatherForecastHandler
{
    public static async Task<IResult> HandleAsync(
        Ulid id,
        [FromBody] WeatherForecastRequest request,
        IWeatherForecastService service,
        HttpContext context,
        IOutputCacheStore outputCacheStore,
        CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, context.GetUserId(), request, ct);
        return result.Match<IResult>(
            updated =>
            {
                outputCacheStore.EvictByTagAsync(nameof(CacheConfig.WeatherForecastCache), ct).GetAwaiter().GetResult();
                return TypedResults.Ok(updated.ToResponse());
            },
            notFound => Results.NotFound(notFound.Message),
            badRequest => Results.BadRequest(badRequest.Message)
        );
    }
}
