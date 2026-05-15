namespace Sample.Api.WeatherForecasts.GetAll;

public static class Handler
{
    public static async Task<IResult> HandleAsync(
        IReadRepository<SampleDbContext, WeatherForecast> repository,
        HttpContext context,
        CancellationToken ct)
    {
        var forecasts = await repository.GetAllAsync(ct);
        if (!forecasts.Any())
            return Results.NotFound("No weather forecasts found.");

        return Results.Ok(forecasts.Select(Response.From).ToList());
    }
}
