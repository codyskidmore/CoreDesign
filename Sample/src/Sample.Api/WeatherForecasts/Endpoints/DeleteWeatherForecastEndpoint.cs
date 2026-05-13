namespace Sample.Api.WeatherForecasts.Endpoints;

public static class DeleteWeatherForecastEndpoint
{
    public const string Name = nameof(DeleteWeatherForecast);

    public static IEndpointRouteBuilder MapDeleteWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapDelete(Paths.WeatherForecasts.Delete, DeleteWeatherForecastHandler.HandleAsync)
            .WithName(Name)
            .Produces(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status404NotFound)
            .RequireAuthorization(AuthorizationRoles.AdminOnlyPolicy);

        return app;
    }

    private class DeleteWeatherForecast;
}
