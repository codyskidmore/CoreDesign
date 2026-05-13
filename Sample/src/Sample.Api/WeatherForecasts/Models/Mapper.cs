namespace Sample.Api.WeatherForecasts.Models;

public static class Mapper
{
    public static WeatherForecastResponse ToResponse(this WeatherForecast forecast)
    {
        return new WeatherForecastResponse(
            forecast.Id,
            forecast.Location,
            forecast.Date,
            forecast.TemperatureC,
            32 + (int)(forecast.TemperatureC / 0.5556),
            forecast.Summary
        );
    }

    public static WeatherForecast ToNewEntity(this WeatherForecastRequest req, Guid userId)
    {
        return new WeatherForecast
        {
            Location = req.Location,
            Date = req.Date,
            TemperatureC = req.TemperatureC,
            Summary = req.Summary
        };
    }

    public static WeatherForecast UpdateFrom(this WeatherForecast forecast, WeatherForecastRequest req)
    {
        forecast.Location = req.Location;
        forecast.Date = req.Date;
        forecast.TemperatureC = req.TemperatureC;
        forecast.Summary = req.Summary;
        return forecast;
    }
}
