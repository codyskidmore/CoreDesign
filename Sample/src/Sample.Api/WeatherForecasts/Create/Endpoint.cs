using Microsoft.AspNetCore.Http.HttpResults;

namespace Sample.Api.WeatherForecasts.Create;

public static class Endpoint
{
    public const string Name = nameof(CreateWeatherForecast);

    public static IEndpointRouteBuilder MapCreateWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapPost(Paths.WeatherForecasts.Create, Handler.HandleAsync)
            .WithName(Name)
            .Produces<Response>()
            .Produces<BadRequest<string>>()
            .RequireAuthorization(AuthorizationRoles.AdminOnlyPolicy);

        return app;
    }

    private class CreateWeatherForecast;
}
