namespace Sample.Api.WeatherForecasts.Create;

public record Request(
    string Location,
    DateOnly Date,
    int TemperatureC,
    string? Summary
)
{
    public WeatherForecast ToNewEntity() => new()
    {
        Location = Location,
        Date = Date,
        TemperatureC = TemperatureC,
        Summary = Summary
    };
}
