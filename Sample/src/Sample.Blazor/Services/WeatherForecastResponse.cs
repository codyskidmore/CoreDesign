namespace Sample.Blazor.Services;

public record WeatherForecastResponse(
    string Id,
    string Location,
    DateOnly Date,
    int TemperatureC,
    int TemperatureF,
    string? Summary
);

