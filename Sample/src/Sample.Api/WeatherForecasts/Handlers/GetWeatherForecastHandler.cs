namespace Sample.Api.WeatherForecasts.Handlers;

public static class GetWeatherForecastHandler
{
    public static async Task<IResult> HandleAsync(
        Ulid id,
        IWeatherForecastService service,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await service.GetAsync(id, context.GetUserId(), ct);
        return result.Match<IResult>(
            forecast => TypedResults.Ok(forecast.ToResponse()),
            notFound => TypedResults.NotFound(notFound.Message));
    }
}
