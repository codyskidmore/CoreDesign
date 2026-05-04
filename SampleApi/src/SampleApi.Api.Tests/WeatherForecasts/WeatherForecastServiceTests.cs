using System.Linq.Expressions;
using SampleApi.Api.Data;
using SampleApi.Api.WeatherForecasts.Models;
using SampleApi.Api.WeatherForecasts.Services;
using CoreDesign.Data.Interfaces;
using CoreDesign.Shared.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;

namespace SampleApi.Api.Tests.WeatherForecasts;

public class WeatherForecastServiceTests
{
    private readonly Mock<ICudRepository<SampleApiDbContext, WeatherForecast>> _mockCud = new();
    private readonly Mock<IReadRepository<SampleApiDbContext, WeatherForecast>> _mockRead = new();
    private readonly Mock<ILogger<WeatherForecastService>> _mockLogger = new();
    private readonly WeatherForecastService _service;

    public WeatherForecastServiceTests()
    {
        _service = new WeatherForecastService(_mockCud.Object, _mockRead.Object, _mockLogger.Object);
    }

    // GetAllAsync

    [Fact]
    public async Task GetAllAsync_WhenForecastsExist_ReturnsForecasts()
    {
        var forecasts = WeatherForecastFakers.WeatherForecast().Generate(3);
        _mockRead.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecasts);

        var result = await _service.GetAllAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(3, result.AsT0.Count());
    }

    [Fact]
    public async Task GetAllAsync_WhenNoForecasts_ReturnsNotFound()
    {
        _mockRead.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WeatherForecast>());

        var result = await _service.GetAllAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.IsType<NotFoundMessage>(result.AsT1);
    }

    // GetAsync

    [Fact]
    public async Task GetAsync_WhenForecastExists_ReturnsForecast()
    {
        var forecast = WeatherForecastFakers.WeatherForecast().Generate();
        _mockRead.Setup(r => r.GetAsync(
                It.IsAny<Expression<Func<WeatherForecast, bool>>>(),
                It.IsAny<Func<IQueryable<WeatherForecast>, IQueryable<WeatherForecast>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecast);

        var result = await _service.GetAsync(forecast.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(forecast.Id, result.AsT0.Id);
    }

    [Fact]
    public async Task GetAsync_WhenForecastNotFound_ReturnsNotFound()
    {
        _mockRead.Setup(r => r.GetAsync(
                It.IsAny<Expression<Func<WeatherForecast, bool>>>(),
                It.IsAny<Func<IQueryable<WeatherForecast>, IQueryable<WeatherForecast>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<WeatherForecast>(null!));

        var result = await _service.GetAsync(Ulid.NewUlid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.IsType<NotFoundMessage>(result.AsT1);
    }

    // CreateAsync

    [Fact]
    public async Task CreateAsync_WhenRepositorySucceeds_ReturnsForecast()
    {
        var request = WeatherForecastFakers.WeatherForecastRequest();
        _mockCud.Setup(r => r.InsertAsync(It.IsAny<WeatherForecast>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.CreateAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(request.Location, result.AsT0.Location);
        Assert.Equal(request.TemperatureC, result.AsT0.TemperatureC);
    }

    [Fact]
    public async Task CreateAsync_WhenRepositoryFails_ReturnsError()
    {
        var request = WeatherForecastFakers.WeatherForecastRequest();
        _mockCud.Setup(r => r.InsertAsync(It.IsAny<WeatherForecast>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CreateAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.IsType<ErrorMessage>(result.AsT1);
    }

    // DeleteAsync

    [Fact]
    public async Task DeleteAsync_WhenForecastExists_ReturnsTrue()
    {
        _mockCud.Setup(r => r.DeleteAsync(It.IsAny<Ulid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DeleteAsync(Ulid.NewUlid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.True(result.AsT0);
    }

    [Fact]
    public async Task DeleteAsync_WhenForecastNotFound_ReturnsNotFound()
    {
        _mockCud.Setup(r => r.DeleteAsync(It.IsAny<Ulid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.DeleteAsync(Ulid.NewUlid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.IsType<NotFoundMessage>(result.AsT1);
    }

    // UpdateAsync

    [Fact]
    public async Task UpdateAsync_WhenForecastExists_ReturnsUpdatedForecast()
    {
        var forecast = WeatherForecastFakers.WeatherForecast().Generate();
        var request = WeatherForecastFakers.WeatherForecastRequest();
        _mockRead.Setup(r => r.GetAsync(
                It.IsAny<Expression<Func<WeatherForecast, bool>>>(),
                It.IsAny<Func<IQueryable<WeatherForecast>, IQueryable<WeatherForecast>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecast);
        _mockCud.Setup(r => r.UpdateAsync(It.IsAny<WeatherForecast>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.UpdateAsync(forecast.Id, Guid.NewGuid(), request, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(request.Location, result.AsT0.Location);
    }

    [Fact]
    public async Task UpdateAsync_WhenForecastNotFound_ReturnsNotFound()
    {
        _mockRead.Setup(r => r.GetAsync(
                It.IsAny<Expression<Func<WeatherForecast, bool>>>(),
                It.IsAny<Func<IQueryable<WeatherForecast>, IQueryable<WeatherForecast>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<WeatherForecast>(null!));

        var result = await _service.UpdateAsync(Ulid.NewUlid(), Guid.NewGuid(), WeatherForecastFakers.WeatherForecastRequest(), CancellationToken.None);

        Assert.True(result.IsT1);
        Assert.IsType<NotFoundMessage>(result.AsT1);
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
        _mockCud.Setup(r => r.UpdateAsync(It.IsAny<WeatherForecast>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.UpdateAsync(forecast.Id, Guid.NewGuid(), WeatherForecastFakers.WeatherForecastRequest(), CancellationToken.None);

        Assert.True(result.IsT2);
        Assert.IsType<BadRequestMessage>(result.AsT2);
    }
}
