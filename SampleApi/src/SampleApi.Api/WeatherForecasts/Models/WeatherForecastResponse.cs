namespace SampleApi.Api.WeatherForecasts.Models;

public record WeatherForecastResponse(
    Ulid Id,
    string Location,
    DateOnly Date,
    int TemperatureC,
    int TemperatureF,
    string? Summary
);
