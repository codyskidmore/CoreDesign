namespace Sample.Api.WeatherForecasts.Handlers;

public static class GetAllWeatherForecastsHandler
{
    public static async Task<IResult> HandleAsync(
        IWeatherForecastService service,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await service.GetAllAsync(context.GetUserId(), ct);
        return result.Match<IResult>(
            forecasts => Results.Ok(forecasts.Select(f => f.ToResponse()).ToList()),
            _ => Results.NotFound("No weather forecasts found.")
        );
    }
}
