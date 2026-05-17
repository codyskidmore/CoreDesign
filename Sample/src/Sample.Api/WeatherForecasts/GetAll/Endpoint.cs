namespace Sample.Api.WeatherForecasts.GetAll;

public static class Endpoint
{
    public const string Name = nameof(GetAllWeatherForecasts);

    public static IEndpointRouteBuilder MapGetAllWeatherForecasts(this IEndpointRouteBuilder app)
    {
        app.MapGet(Paths.WeatherForecasts.GetAll, async (
                IGetAllForecastsHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.GetAllAsync(ct);
                return result.Match(
                    forecasts => Results.Ok(forecasts.Select(Response.From).ToList()),
                    error     => Results.NotFound(error.Message));
            })
            .WithName(Name)
            .Produces<IEnumerable<Response>>()
            .Produces(StatusCodes.Status404NotFound)
            .CacheOutput(nameof(CacheConfig.WeatherForecastCache))
            .RequireAuthorization(Permissions.WeatherRead);

        return app;
    }

    private class GetAllWeatherForecasts;
}
