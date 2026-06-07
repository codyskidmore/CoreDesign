using Microsoft.AspNetCore.Http.HttpResults;
using GetByIdEndpoint = Sample.Api.WeatherForecasts.GetById.Endpoint;

namespace Sample.Api.WeatherForecasts.Create;

public static class Endpoint
{
    public const string Name = nameof(CreateWeatherForecast);

    public static IEndpointRouteBuilder MapCreateWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapPost(Paths.WeatherForecasts.Create, async (
                [FromBody] Request request,
                ICreateForecastHandler handler,
                IOutputCacheStore outputCacheStore,
                CancellationToken ct) =>
            {
                var result = await handler.CreateAsync(request, ct);
                return result.Match(
                    forecast =>
                    {
                        _ = outputCacheStore.EvictByTagAsync(nameof(CacheConfig.WeatherForecastCache), ct);
                        return Results.CreatedAtRoute(GetByIdEndpoint.Name, new { id = forecast.Id }, Response.From(forecast));
                    },
                    error => Results.BadRequest(error.Message));
            })
            .WithName(Name)
            .Produces<Response>()
            .Produces<BadRequest<string>>()
            .RequireAuthorization(Permissions.WeatherWrite);

        return app;
    }

    private class CreateWeatherForecast;
}
