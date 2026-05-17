using System.Linq.Expressions;
using Sample.Api.Data;
using Sample.Api.WeatherForecasts.GetById;
using Sample.Api.WeatherForecasts.Shared;
using CoreDesign.Data.Interfaces;
using Moq;

namespace Sample.Api.Tests.WeatherForecasts.GetById;

public class GetByIdHandlerTests
{
    private readonly Mock<IReadRepository<SampleDbContext, WeatherForecast>> _mockRead = new();

    [Fact]
    public async Task GetByIdAsync_WhenForecastExists_ReturnsForecast()
    {
        var forecast = WeatherForecastFakers.WeatherForecast().Generate();
        _mockRead.Setup(r => r.GetAsync(
                It.IsAny<Expression<Func<WeatherForecast, bool>>>(),
                It.IsAny<Func<IQueryable<WeatherForecast>, IQueryable<WeatherForecast>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecast);
        var handler = new GetForecastHandler(_mockRead.Object);

        var result = await handler.GetByIdAsync(forecast.Id, CancellationToken.None);

        Assert.True(result.IsT0);
    }

    [Fact]
    public async Task GetByIdAsync_WhenForecastNotFound_ReturnsNotFound()
    {
        _mockRead.Setup(r => r.GetAsync(
                It.IsAny<Expression<Func<WeatherForecast, bool>>>(),
                It.IsAny<Func<IQueryable<WeatherForecast>, IQueryable<WeatherForecast>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<WeatherForecast>(null!));
        var handler = new GetForecastHandler(_mockRead.Object);

        var result = await handler.GetByIdAsync(Ulid.NewUlid(), CancellationToken.None);

        Assert.True(result.IsT1);
    }
}
