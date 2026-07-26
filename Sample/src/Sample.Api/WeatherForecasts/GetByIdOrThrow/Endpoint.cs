using GetByIdResponse = Sample.Api.WeatherForecasts.GetById.Response;

namespace Sample.Api.WeatherForecasts.GetByIdOrThrow;

public static class Endpoint
{
    public const string Name = nameof(GetWeatherForecastOrThrowEndpoint);

    public static IEndpointRouteBuilder MapGetWeatherForecastOrThrow(this IEndpointRouteBuilder app)
    {
        app.MapGet(Paths.WeatherForecasts.GetByIdOrThrow, async (
                Ulid id,
                IGetForecastOrThrowHandler handler,
                CancellationToken ct) =>
            {
                var forecast = await handler.GetByIdAsync(id, ct);
                return TypedResults.Ok(GetByIdResponse.From(forecast));
            })
            .WithName(Name)
            .Produces<GetByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .CacheOutput(nameof(CacheConfig.WeatherForecastCache))
            .RequireAuthorization(Permissions.WeatherRead);

        return app;
    }

    private class GetWeatherForecastOrThrowEndpoint;
}
