using Sample.Api.WeatherForecasts.Models;

namespace Sample.Api.Tests.WeatherForecasts;

public class MapperTests
{
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        var forecast = WeatherForecastFakers.WeatherForecast().Generate();

        var response = forecast.ToResponse();

        Assert.Equal(forecast.Id, response.Id);
        Assert.Equal(forecast.Location, response.Location);
        Assert.Equal(forecast.Date, response.Date);
        Assert.Equal(forecast.TemperatureC, response.TemperatureC);
        Assert.Equal(forecast.Summary, response.Summary);
    }

    [Fact]
    public void ToResponse_CalculatesTemperatureF()
    {
        var forecast = WeatherForecastFakers.WeatherForecast().Generate();
        forecast.TemperatureC = 0;

        var response = forecast.ToResponse();

        Assert.Equal(32, response.TemperatureF);
    }

    [Fact]
    public void ToNewEntity_MapsAllFields()
    {
        var request = WeatherForecastFakers.WeatherForecastRequest();

        var entity = request.ToNewEntity(Guid.NewGuid());

        Assert.Equal(request.Location, entity.Location);
        Assert.Equal(request.Date, entity.Date);
        Assert.Equal(request.TemperatureC, entity.TemperatureC);
        Assert.Equal(request.Summary, entity.Summary);
    }

    [Fact]
    public void UpdateFrom_UpdatesAllFields()
    {
        var forecast = WeatherForecastFakers.WeatherForecast().Generate();
        var request = WeatherForecastFakers.WeatherForecastRequest();

        forecast.UpdateFrom(request);

        Assert.Equal(request.Location, forecast.Location);
        Assert.Equal(request.Date, forecast.Date);
        Assert.Equal(request.TemperatureC, forecast.TemperatureC);
        Assert.Equal(request.Summary, forecast.Summary);
    }
}
