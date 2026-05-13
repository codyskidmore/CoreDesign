namespace Sample.Api.WeatherForecasts.Models;

public record WeatherForecastRequest(
    string Location,
    DateOnly Date,
    int TemperatureC,
    string? Summary
);
