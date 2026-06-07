using Sample.Api.Data;
using Sample.Api.WeatherForecasts.Create;
using Sample.Api.WeatherForecasts.Shared;
using CoreDesign.Data.Interfaces;
using Moq;

namespace Sample.Api.Tests.WeatherForecasts.Create;

public class CreateHandlerTests
{
    private readonly Mock<ICudRepository<SampleDbContext, WeatherForecast>> _mockCud = new();

    [Fact]
    public async Task CreateAsync_WhenRepositorySucceeds_ReturnsWeatherForecast()
    {
        _mockCud.Setup(r => r.InsertAsync(It.IsAny<WeatherForecast>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new CreateForecastHandler(_mockCud.Object);

        var result = await handler.CreateAsync(WeatherForecastFakers.CreateRequest(), CancellationToken.None);

        Assert.True(result.IsT0);
    }

    [Fact]
    public async Task CreateAsync_WhenRepositoryFails_ReturnsBadRequest()
    {
        _mockCud.Setup(r => r.InsertAsync(It.IsAny<WeatherForecast>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new CreateForecastHandler(_mockCud.Object);

        var result = await handler.CreateAsync(WeatherForecastFakers.CreateRequest(), CancellationToken.None);

        Assert.True(result.IsT1);
    }

    [Fact]
    public void Request_ToNewEntity_MapsAllFields()
    {
        var req = WeatherForecastFakers.CreateRequest();
        var entity = req.ToNewEntity();

        Assert.Equal(req.Location, entity.Location);
        Assert.Equal(req.Date, entity.Date);
        Assert.Equal(req.TemperatureC, entity.TemperatureC);
        Assert.Equal(req.Summary, entity.Summary);
    }

    [Fact]
    public void Response_From_CalculatesTemperatureF()
    {
        var entity = WeatherForecastFakers.WeatherForecast().Generate();
        entity.TemperatureC = 0;

        var response = Response.From(entity);

        Assert.Equal(32, response.TemperatureF);
    }
}
