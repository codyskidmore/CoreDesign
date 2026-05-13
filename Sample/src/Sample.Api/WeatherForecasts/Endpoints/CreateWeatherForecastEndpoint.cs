using Microsoft.AspNetCore.Http.HttpResults;

namespace Sample.Api.WeatherForecasts.Endpoints;

public static class CreateWeatherForecastEndpoint
{
    public const string Name = nameof(CreateWeatherForecast);

    public static IEndpointRouteBuilder MapCreateWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapPost(Paths.WeatherForecasts.Create, CreateWeatherForecastHandler.HandleAsync)
            .WithName(Name)
            .Produces<WeatherForecastResponse>()
            .Produces<BadRequest<string>>()
            .RequireAuthorization(AuthorizationRoles.AdminOnlyPolicy);

        return app;
    }

    private class CreateWeatherForecast;
}
