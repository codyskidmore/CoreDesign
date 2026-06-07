using Sample.Api.Data;
using Sample.Api.WeatherForecasts.Delete;
using Sample.Api.WeatherForecasts.Shared;
using CoreDesign.Data.Interfaces;
using Moq;

namespace Sample.Api.Tests.WeatherForecasts.Delete;

public class DeleteHandlerTests
{
    private readonly Mock<ICudRepository<SampleDbContext, WeatherForecast>> _mockCud = new();

    [Fact]
    public async Task DeleteAsync_WhenForecastExists_ReturnsSuccess()
    {
        _mockCud.Setup(r => r.SoftDeleteAsync(It.IsAny<Ulid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new DeleteForecastHandler(_mockCud.Object);

        var result = await handler.DeleteAsync(Ulid.NewUlid(), CancellationToken.None);

        Assert.True(result.IsT0);
    }

    [Fact]
    public async Task DeleteAsync_WhenForecastNotFound_ReturnsNotFound()
    {
        _mockCud.Setup(r => r.SoftDeleteAsync(It.IsAny<Ulid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new DeleteForecastHandler(_mockCud.Object);

        var result = await handler.DeleteAsync(Ulid.NewUlid(), CancellationToken.None);

        Assert.True(result.IsT1);
    }
}
