using GetByIdResponse = Sample.Api.WeatherForecasts.GetById.Response;

namespace Sample.Api.WeatherForecasts.Update;

public static class Endpoint
{
    public const string Name = nameof(UpdateWeatherForecast);

    public static IEndpointRouteBuilder MapUpdateWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapPut(Paths.WeatherForecasts.Update, Handler.HandleAsync)
            .WithName(Name)
            .Produces<GetByIdResponse>()
            .Produces<string>(StatusCodes.Status404NotFound)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .RequireAuthorization(AuthorizationRoles.AdminOnlyPolicy);

        return app;
    }

    private class UpdateWeatherForecast;
}
