using GetByIdResponse = Sample.Api.WeatherForecasts.GetById.Response;

namespace Sample.Api.WeatherForecasts.Update;

public static class Endpoint
{
    public const string Name = nameof(UpdateWeatherForecast);

    public static IEndpointRouteBuilder MapUpdateWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapPut(Paths.WeatherForecasts.Update, HandleAsync)
            .WithName(Name)
            .Produces<GetByIdResponse>()
            .Produces<string>(StatusCodes.Status404NotFound)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .RequireAuthorization(Permissions.WeatherWrite);

        return app;
    }

    public static async Task<IResult> HandleAsync(
        Ulid id,
        [FromBody] Request request,
        IUpdateForecastHandler handler,
        HttpContext context,
        IOutputCacheStore outputCacheStore,
        CancellationToken ct)
    {
        var result = await handler.UpdateAsync(id, request, context.GetUserId(), ct);

        return result.Match(
            forecast =>
            {
                _ = outputCacheStore.EvictByTagAsync(nameof(CacheConfig.WeatherForecastCache), ct);
                return TypedResults.Ok(GetByIdResponse.From(forecast));
            },
            notFound   => Results.NotFound(notFound.Message),
            badRequest => Results.BadRequest(badRequest.Message)
        );
    }

    private class UpdateWeatherForecast;
}
