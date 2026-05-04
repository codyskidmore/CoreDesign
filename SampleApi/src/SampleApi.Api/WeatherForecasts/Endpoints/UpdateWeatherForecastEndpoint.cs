namespace SampleApi.Api.WeatherForecasts.Endpoints;

public static class UpdateWeatherForecastEndpoint
{
    public const string Name = nameof(UpdateWeatherForecast);

    public static IEndpointRouteBuilder MapUpdateWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapPut(Paths.WeatherForecasts.Update, async (
                Ulid id,
                [FromBody] WeatherForecastRequest request,
                IWeatherForecastService service,
                HttpContext context,
                IOutputCacheStore outputCacheStore,
                CancellationToken ct) =>
            {
                var result = await service.UpdateAsync(id, context.GetUserId(), request, ct);
                return result.Match<IResult>(
                    updated =>
                    {
                        outputCacheStore.EvictByTagAsync(nameof(CacheConfig.WeatherForecastCache), ct).GetAwaiter().GetResult();
                        return TypedResults.Ok(updated.ToResponse());
                    },
                    notFound => Results.NotFound(notFound.Message),
                    badRequest => Results.BadRequest(badRequest.Message)
                );
            })
            .WithName(Name)
            .Produces<WeatherForecastResponse>()
            .Produces<string>(StatusCodes.Status404NotFound)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .RequireAuthorization(AuthorizationRoles.AdminOnlyPolicy);

        return app;
    }

    private class UpdateWeatherForecast;
}
