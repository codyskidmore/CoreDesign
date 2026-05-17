using Sample.Api.Data;
using Sample.Api.WeatherForecasts.GetAll;
using Sample.Api.WeatherForecasts.Shared;
using CoreDesign.Data.Interfaces;
using Moq;

namespace Sample.Api.Tests.WeatherForecasts.GetAll;

public class GetAllHandlerTests
{
    private readonly Mock<IReadRepository<SampleDbContext, WeatherForecast>> _mockRead = new();

    [Fact]
    public async Task GetAllAsync_WhenForecastsExist_ReturnsForecasts()
    {
        var forecasts = WeatherForecastFakers.WeatherForecast().Generate(3);
        _mockRead.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecasts);
        var handler = new GetAllForecastsHandler(_mockRead.Object);

        var result = await handler.GetAllAsync(CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(3, result.AsT0.Count);
    }

    [Fact]
    public async Task GetAllAsync_WhenNoForecasts_ReturnsNotFound()
    {
        _mockRead.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var handler = new GetAllForecastsHandler(_mockRead.Object);

        var result = await handler.GetAllAsync(CancellationToken.None);

        Assert.True(result.IsT1);
    }
}
