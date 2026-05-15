namespace Sample.Api.WeatherForecasts.Create;

public record Response(
    Ulid Id,
    string Location,
    DateOnly Date,
    int TemperatureC,
    int TemperatureF,
    string? Summary
)
{
    public static Response From(WeatherForecast f) =>
        new(f.Id, f.Location, f.Date, f.TemperatureC, 32 + (int)(f.TemperatureC / 0.5556), f.Summary);
}
