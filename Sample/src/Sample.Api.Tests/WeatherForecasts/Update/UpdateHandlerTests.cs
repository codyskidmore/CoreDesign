using System.Linq.Expressions;
using Sample.Api.Data;
using Sample.Api.WeatherForecasts.Shared;
using Sample.Api.WeatherForecasts.Update;
using CoreDesign.Data.Interfaces;
using Moq;

namespace Sample.Api.Tests.WeatherForecasts.Update;

public class UpdateHandlerTests
{
    private readonly Mock<IReadRepository<SampleDbContext, WeatherForecast>> _mockRead = new();
    private readonly Mock<ICudRepository<SampleDbContext, WeatherForecast>> _mockCud = new();

    [Fact]
    public async Task UpdateAsync_WhenForecastExists_ReturnsUpdatedForecast()
    {
        var forecast = WeatherForecastFakers.WeatherForecast().Generate();
        _mockRead.Setup(r => r.GetAsync(
                It.IsAny<Expression<Func<WeatherForecast, bool>>>(),
                It.IsAny<Func<IQueryable<WeatherForecast>, IQueryable<WeatherForecast>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecast);
        _mockCud.Setup(r => r.UpdateAsync(It.IsAny<WeatherForecast>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new UpdateForecastHandler(_mockRead.Object, _mockCud.Object);

        var result = await handler.UpdateAsync(forecast.Id, WeatherForecastFakers.UpdateRequest(), CancellationToken.None);

        Assert.True(result.IsT0);
    }

    [Fact]
    public async Task UpdateAsync_WhenForecastNotFound_ReturnsNotFound()
    {
        _mockRead.Setup(r => r.GetAsync(
                It.IsAny<Expression<Func<WeatherForecast, bool>>>(),
                It.IsAny<Func<IQueryable<WeatherForecast>, IQueryable<WeatherForecast>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<WeatherForecast?>(null));
        var handler = new UpdateForecastHandler(_mockRead.Object, _mockCud.Object);

        var result = await handler.UpdateAsync(Ulid.NewUlid(), WeatherForecastFakers.UpdateRequest(), CancellationToken.None);

        Assert.True(result.IsT1);
    }

    [Fact]
    public async Task UpdateAsync_WhenRepositoryFails_ReturnsBadRequest()
    {
        var forecast = WeatherForecastFakers.WeatherForecast().Generate();
        _mockRead.Setup(r => r.GetAsync(
                It.IsAny<Expression<Func<WeatherForecast, bool>>>(),
                It.IsAny<Func<IQueryable<WeatherForecast>, IQueryable<WeatherForecast>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecast);
        _mockCud.Setup(r => r.UpdateAsync(It.IsAny<WeatherForecast>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new UpdateForecastHandler(_mockRead.Object, _mockCud.Object);

        var result = await handler.UpdateAsync(forecast.Id, WeatherForecastFakers.UpdateRequest(), CancellationToken.None);

        Assert.True(result.IsT2);
    }

    [Fact]
    public void Request_Apply_UpdatesAllFields()
    {
        var entity = WeatherForecastFakers.WeatherForecast().Generate();
        var req = WeatherForecastFakers.UpdateRequest();

        req.Apply(entity);

        Assert.Equal(req.Location, entity.Location);
        Assert.Equal(req.Date, entity.Date);
        Assert.Equal(req.TemperatureC, entity.TemperatureC);
        Assert.Equal(req.Summary, entity.Summary);
    }
}
