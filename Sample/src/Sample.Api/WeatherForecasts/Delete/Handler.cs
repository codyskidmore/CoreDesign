namespace Sample.Api.WeatherForecasts.Delete;

public static class Handler
{
    public static async Task<IResult> HandleAsync(
        Ulid id,
        ICudRepository<SampleDbContext, WeatherForecast> repository,
        HttpContext context,
        IOutputCacheStore outputCacheStore,
        CancellationToken ct)
    {
        var deleted = await repository.DeleteAsync(id, context.GetUserId(), ct);
        if (!deleted)
            return Results.NotFound($"Weather forecast id {id} not found for deletion");

        _ = outputCacheStore.EvictByTagAsync(nameof(CacheConfig.WeatherForecastCache), ct);
        return TypedResults.Ok();
    }
}
