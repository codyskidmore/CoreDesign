namespace Sample.Api.WeatherForecasts.GetAll;

public static class Endpoint
{
    public const string Name = nameof(GetAllWeatherForecasts);

    public static IEndpointRouteBuilder MapGetAllWeatherForecasts(this IEndpointRouteBuilder app)
    {
        app.MapGet(Paths.WeatherForecasts.GetAll, Handler.HandleAsync)
            .WithName(Name)
            .Produces<IEnumerable<Response>>()
            .Produces(StatusCodes.Status404NotFound)
            .CacheOutput(nameof(CacheConfig.WeatherForecastCache))
            .RequireAuthorization(AuthorizationRoles.UserOrAdminPolicy);

        return app;
    }

    private class GetAllWeatherForecasts;
}
