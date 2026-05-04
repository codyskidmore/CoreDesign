namespace SampleApi.Api.WeatherForecasts.Endpoints;

public static class GetWeatherForecastEndpoint
{
    public const string Name = nameof(GetWeatherForecastEndpoint);

    public static IEndpointRouteBuilder MapGetWeatherForecast(this IEndpointRouteBuilder app)
    {
        app.MapGet(Paths.WeatherForecasts.GetById, async (
                Ulid id,
                IWeatherForecastService service,
                HttpContext context,
                CancellationToken ct) =>
            {
                var result = await service.GetAsync(id, context.GetUserId(), ct);
                return result.Match<IResult>(
                    forecast => TypedResults.Ok(forecast.ToResponse()),
                    notFound => TypedResults.NotFound(notFound.Message));
            })
            .WithName(Name)
            .Produces<WeatherForecastResponse>()
            .Produces<string>(StatusCodes.Status404NotFound)
            .CacheOutput(nameof(CacheConfig.WeatherForecastCache))
            .RequireAuthorization(AuthorizationRoles.UserOrAdminPolicy);

        return app;
    }

    private class GetWeatherForecastById;
}
