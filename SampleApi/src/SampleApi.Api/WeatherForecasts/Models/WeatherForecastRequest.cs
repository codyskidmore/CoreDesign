namespace SampleApi.Api.WeatherForecasts.Models;

public record WeatherForecastRequest(
    string Location,
    DateOnly Date,
    int TemperatureC,
    string? Summary
);
