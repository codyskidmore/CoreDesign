namespace Sample.Api.WeatherForecasts.GetById;

public static class Handler
{
    public static async Task<IResult> HandleAsync(
        Ulid id,
        IReadRepository<SampleDbContext, WeatherForecast> repository,
        HttpContext context,
        CancellationToken ct)
    {
        var forecast = await repository.GetAsync(f => f.Id == id, query => query, ct);
        if (forecast is null)
            return TypedResults.NotFound($"Weather forecast not found for id: {id}");

        return TypedResults.Ok(Response.From(forecast));
    }
}
