using GetByIdResponse = Sample.Api.WeatherForecasts.GetById.Response;

namespace Sample.Api.WeatherForecasts.Update;

public static class Endpoint
{
    public const string Name = nameof(UpdateWeatherForecast);

    public static IEndpointRouteBuilder MapUpdateWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapPut(Paths.WeatherForecasts.Update, async (
                Ulid id,
                [FromBody] Request request,
                IUpdateForecastHandler handler,
                IOutputCacheStore outputCacheStore,
                CancellationToken ct) =>
            {
                var result = await handler.UpdateAsync(id, request, ct);
                if (result is WeatherForecast)
                    _ = outputCacheStore.EvictByTagAsync(nameof(CacheConfig.WeatherForecastCache), ct);

                return result switch
                {
                    WeatherForecast forecast     => Results.Ok(GetByIdResponse.From(forecast)),
                    NotFoundMessage notFound     => Results.NotFound(notFound.Message),
                    BadRequestMessage badRequest => Results.BadRequest(badRequest.Message),
                };
            })
            .WithName(Name)
            .Produces<GetByIdResponse>()
            .Produces<string>(StatusCodes.Status404NotFound)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .RequireAuthorization(Permissions.WeatherWrite);

        return app;
    }

    private class UpdateWeatherForecast;
}
