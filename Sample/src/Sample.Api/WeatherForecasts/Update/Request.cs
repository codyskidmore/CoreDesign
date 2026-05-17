namespace Sample.Api.WeatherForecasts.Update;

public record Request(
    string Location,
    DateOnly Date,
    int TemperatureC,
    string? Summary
)
{
    public void Apply(WeatherForecast entity)
    {
        entity.Location = Location;
        entity.Date = Date;
        entity.TemperatureC = TemperatureC;
        entity.Summary = Summary;
    }
}
