using GetByIdEndpoint = Sample.Api.WeatherForecasts.GetById.Endpoint;

namespace Sample.Api.WeatherForecasts.Create;

public static class Handler
{
    public static async Task<IResult> HandleAsync(
        [FromBody] Request request,
        ICudRepository<SampleDbContext, WeatherForecast> repository,
        HttpContext context,
        IOutputCacheStore outputCacheStore,
        CancellationToken ct)
    {
        var entity = request.ToNewEntity();
        var success = await repository.InsertAsync(entity, context.GetUserId(), ct);
        if (!success)
            return Results.BadRequest("Failed to create weather forecast.");

        _ = outputCacheStore.EvictByTagAsync(nameof(CacheConfig.WeatherForecastCache), ct);
        var response = Response.From(entity);
        return Results.CreatedAtRoute(GetByIdEndpoint.Name, new { id = response.Id }, response);
    }
}
