using Microsoft.AspNetCore.Http.HttpResults;

namespace SampleApi.Api.WeatherForecasts.Endpoints;

public static class CreateWeatherForecastEndpoint
{
    public const string Name = nameof(CreateWeatherForecast);

    public static IEndpointRouteBuilder MapCreateWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapPost(Paths.WeatherForecasts.Create, async (
                [FromBody] WeatherForecastRequest request,
                IWeatherForecastService service,
                HttpContext context,
                IOutputCacheStore outputCacheStore,
                CancellationToken ct) =>
            {
                var result = await service.CreateAsync(context.GetUserId(), request, ct);
                return result.Match<IResult>(
                    forecast =>
                    {
                        var response = forecast.ToResponse();
                        outputCacheStore.EvictByTagAsync(nameof(CacheConfig.WeatherForecastCache), ct);
                        return Results.CreatedAtRoute(GetWeatherForecastEndpoint.Name,
                            new { id = response.Id }, response);
                    },
                    error => Results.BadRequest(error));
            })
            .WithName(Name)
            .Produces<WeatherForecastResponse>()
            .Produces<BadRequest<string>>()
            .RequireAuthorization(AuthorizationRoles.AdminOnlyPolicy);

        return app;
    }

    private class CreateWeatherForecast;
}
