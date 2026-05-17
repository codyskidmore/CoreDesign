namespace Sample.Api.WeatherForecasts.Shared;

public class WeatherForecast : BaseEntity
{
    public required string Location { get; set; }
    public DateOnly Date { get; set; }
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }
}
