namespace Sample.Api.WeatherForecasts.Endpoints;

public static class GetWeatherForecastEndpoint
{
    public const string Name = nameof(GetWeatherForecastEndpoint);

    public static IEndpointRouteBuilder MapGetWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapGet(Paths.WeatherForecasts.GetById, GetWeatherForecastHandler.HandleAsync)
            .WithName(Name)
            .Produces<WeatherForecastResponse>()
            .Produces<string>(StatusCodes.Status404NotFound)
            .CacheOutput(nameof(CacheConfig.WeatherForecastCache))
            .RequireAuthorization(AuthorizationRoles.UserOrAdminPolicy);

        return app;
    }

    private class GetWeatherForecastById;
}
