using GetByIdResponse = Sample.Api.WeatherForecasts.GetById.Response;

namespace Sample.Api.WeatherForecasts.Update;

public static class Handler
{
    public static async Task<IResult> HandleAsync(
        Ulid id,
        [FromBody] Request request,
        IReadRepository<SampleDbContext, WeatherForecast> readRepository,
        ICudRepository<SampleDbContext, WeatherForecast> cudRepository,
        HttpContext context,
        IOutputCacheStore outputCacheStore,
        CancellationToken ct)
    {
        var forecast = await readRepository.GetAsync(f => f.Id == id, query => query, ct);
        if (forecast is null)
            return Results.NotFound($"Weather forecast with id {id} not found.");

        request.Apply(forecast);
        var success = await cudRepository.UpdateAsync(forecast, context.GetUserId(), ct);
        if (!success)
            return Results.BadRequest("Failed to update weather forecast.");

        outputCacheStore.EvictByTagAsync(nameof(CacheConfig.WeatherForecastCache), ct).GetAwaiter().GetResult();
        return TypedResults.Ok(GetByIdResponse.From(forecast));
    }
}
