namespace SampleApi.Api.WeatherForecasts.Endpoints;

public static class GetAllWeatherForecastsEndpoint
{
    public const string Name = nameof(GetAllWeatherForecasts);

    public static IEndpointRouteBuilder MapGetAllWeatherForecasts(this IEndpointRouteBuilder app)
    {
        app.MapGet(Paths.WeatherForecasts.GetAll, GetAllWeatherForecastsHandler.HandleAsync)
            .WithName(Name)
            .Produces<IEnumerable<WeatherForecastResponse>>()
            .Produces(StatusCodes.Status404NotFound)
            .CacheOutput(nameof(CacheConfig.WeatherForecastCache))
            .RequireAuthorization(AuthorizationRoles.UserOrAdminPolicy);

        return app;
    }

    private class GetAllWeatherForecasts;
}
