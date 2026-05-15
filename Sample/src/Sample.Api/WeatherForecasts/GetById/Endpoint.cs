namespace Sample.Api.WeatherForecasts.GetById;

public static class Endpoint
{
    public const string Name = nameof(GetWeatherForecastEndpoint);

    public static IEndpointRouteBuilder MapGetWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapGet(Paths.WeatherForecasts.GetById, Handler.HandleAsync)
            .WithName(Name)
            .Produces<Response>()
            .Produces<string>(StatusCodes.Status404NotFound)
            .CacheOutput(nameof(CacheConfig.WeatherForecastCache))
            .RequireAuthorization(AuthorizationRoles.UserOrAdminPolicy);

        return app;
    }

    private class GetWeatherForecastEndpoint;
}
