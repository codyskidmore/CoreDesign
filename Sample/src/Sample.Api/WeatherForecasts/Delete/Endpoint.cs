namespace Sample.Api.WeatherForecasts.Delete;

public static class Endpoint
{
    public const string Name = nameof(DeleteWeatherForecast);

    public static IEndpointRouteBuilder MapDeleteWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapDelete(Paths.WeatherForecasts.Delete, async (
                Ulid id,
                IDeleteForecastHandler handler,
                IOutputCacheStore outputCacheStore,
                CancellationToken ct) =>
            {
                var result = await handler.DeleteAsync(id, ct);
                return result.Match(
                    success =>
                    {
                        _ = outputCacheStore.EvictByTagAsync(nameof(CacheConfig.WeatherForecastCache), ct);
                        return TypedResults.Ok();
                    },
                    error => Results.NotFound(error.Message));
            })
            .WithName(Name)
            .Produces(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status404NotFound)
            .RequireAuthorization(Permissions.WeatherWrite);

        return app;
    }

    private class DeleteWeatherForecast;
}
