namespace SampleApi.Api.WeatherForecasts.Endpoints;

public static class UpdateWeatherForecastEndpoint
{
    public const string Name = nameof(UpdateWeatherForecast);

    public static IEndpointRouteBuilder MapUpdateWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapPut(Paths.WeatherForecasts.Update, UpdateWeatherForecastHandler.HandleAsync)
            .WithName(Name)
            .Produces<WeatherForecastResponse>()
            .Produces<string>(StatusCodes.Status404NotFound)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .RequireAuthorization(AuthorizationRoles.AdminOnlyPolicy);

        return app;
    }

    private class UpdateWeatherForecast;
}
