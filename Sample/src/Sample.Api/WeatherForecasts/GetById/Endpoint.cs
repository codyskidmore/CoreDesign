namespace Sample.Api.WeatherForecasts.GetById;

public static class Endpoint
{
    public const string Name = nameof(GetWeatherForecastEndpoint);

    public static IEndpointRouteBuilder MapGetWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapGet(Paths.WeatherForecasts.GetById, HandleAsync)
            .WithName(Name)
            .Produces<Response>()
            .Produces<string>(StatusCodes.Status404NotFound)
            .CacheOutput(nameof(CacheConfig.WeatherForecastCache))
            .RequireAuthorization(Permissions.WeatherRead);

        return app;
    }

    public static async Task<IResult> HandleAsync(
        Ulid id,
        IGetForecastHandler handler,
        CancellationToken ct)
    {
        var result = await handler.GetByIdAsync(id, ct);

        return result.Match(
            forecast => TypedResults.Ok(Response.From(forecast)),
            error    => Results.NotFound(error.Message)
        );
    }

    private class GetWeatherForecastEndpoint;
}
