namespace SampleApi.Api.WeatherForecasts.Endpoints;

public static class GetAllWeatherForecastsEndpoint
{
    public const string Name = nameof(GetAllWeatherForecasts);

    public static IEndpointRouteBuilder MapGetAllWeatherForecasts(this IEndpointRouteBuilder app)
    {
        app.MapGet(Paths.WeatherForecasts.GetAll, async (
                IWeatherForecastService service,
                HttpContext context,
                CancellationToken ct) =>
            {
                var result = await service.GetAllAsync(context.GetUserId(), ct);
                return result.Match<IResult>(
                    forecasts => Results.Ok(forecasts.Select(f => f.ToResponse()).ToList()),
                    _ => Results.NotFound("No weather forecasts found.")
                );
            })
            .WithName(Name)
            .Produces<IEnumerable<WeatherForecastResponse>>()
            .Produces(StatusCodes.Status404NotFound)
            .CacheOutput(nameof(CacheConfig.WeatherForecastCache))
            .RequireAuthorization(AuthorizationRoles.UserOrAdminPolicy);

        return app;
    }

    private class GetAllWeatherForecasts;
}
