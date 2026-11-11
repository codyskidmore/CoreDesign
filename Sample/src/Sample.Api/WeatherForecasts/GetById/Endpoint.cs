namespace Sample.Api.WeatherForecasts.GetById;

public static class Endpoint
{
    public const string Name = nameof(GetWeatherForecastEndpoint);

    public static IEndpointRouteBuilder MapGetWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapGet(Paths.WeatherForecasts.GetById, async (
                Ulid id,
                IGetForecastHandler handler,
                CancellationToken ct) =>
            {
                var result = await handler.GetByIdAsync(id, ct);
                return result switch
                {
                    WeatherForecast forecast => Viewed(forecast),
                    NotFoundMessage error    => Results.NotFound(error.Message),
                };
            })
            .WithName(Name)
            .Produces<Response>()
            .Produces<string>(StatusCodes.Status404NotFound)
            .CacheOutput(nameof(CacheConfig.WeatherForecastCache))
            .RequireAuthorization(Permissions.WeatherRead);

        return app;
    }

    private static IResult Viewed(WeatherForecast forecast)
    {
        Journey.Viewed(nameof(WeatherForecast), forecast.Location);
        return Results.Ok(Response.From(forecast));
    }

    private class GetWeatherForecastEndpoint;
}
