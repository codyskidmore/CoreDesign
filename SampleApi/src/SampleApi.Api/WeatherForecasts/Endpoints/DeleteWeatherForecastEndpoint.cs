namespace SampleApi.Api.WeatherForecasts.Endpoints;

public static class DeleteWeatherForecastEndpoint
{
    public const string Name = nameof(DeleteWeatherForecast);

    public static IEndpointRouteBuilder MapDeleteWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapDelete(Paths.WeatherForecasts.Delete, async (
                Ulid id,
                IWeatherForecastService service,
                HttpContext context,
                IOutputCacheStore outputCacheStore,
                CancellationToken ct) =>
            {
                var result = await service.DeleteAsync(id, context.GetUserId(), ct);
                return result.Match<IResult>(
                    success =>
                    {
                        outputCacheStore.EvictByTagAsync(nameof(CacheConfig.WeatherForecastCache), ct);
                        return TypedResults.Ok();
                    },
                    _ => Results.NotFound($"Weather forecast id {id} not found for deletion"));
            })
            .WithName(Name)
            .Produces(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status404NotFound)
            .RequireAuthorization(AuthorizationRoles.AdminOnlyPolicy);

        return app;
    }

    private class DeleteWeatherForecast;
}
