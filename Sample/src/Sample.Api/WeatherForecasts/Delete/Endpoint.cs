namespace Sample.Api.WeatherForecasts.Delete;

public static class Endpoint
{
    public const string Name = nameof(DeleteWeatherForecast);

    public static IEndpointRouteBuilder MapDeleteWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapDelete(Paths.WeatherForecasts.Delete, Handler.HandleAsync)
            .WithName(Name)
            .Produces(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status404NotFound)
            .RequireAuthorization(AuthorizationRoles.AdminOnlyPolicy);

        return app;
    }

    private class DeleteWeatherForecast;
}
