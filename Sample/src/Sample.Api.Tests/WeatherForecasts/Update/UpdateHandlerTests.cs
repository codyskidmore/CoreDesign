using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
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
    private readonly Mock<IOutputCacheStore> _mockCache = new();

    private static HttpContext BuildContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("oid", Guid.NewGuid().ToString())]));
        return ctx;
    }

    [Fact]
    public async Task HandleAsync_WhenForecastExists_ReturnsOk()
    {
        var forecast = WeatherForecastFakers.WeatherForecast().Generate();
        _mockRead.Setup(r => r.GetAsync(
                It.IsAny<Expression<Func<WeatherForecast, bool>>>(),
                It.IsAny<Func<IQueryable<WeatherForecast>, IQueryable<WeatherForecast>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecast);
        _mockCud.Setup(r => r.UpdateAsync(It.IsAny<WeatherForecast>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Handler.HandleAsync(
            forecast.Id, WeatherForecastFakers.UpdateRequest(),
            _mockRead.Object, _mockCud.Object,
            BuildContext(), _mockCache.Object, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, ((IStatusCodeHttpResult)result).StatusCode);
    }

    [Fact]
    public async Task HandleAsync_WhenForecastNotFound_ReturnsNotFound()
    {
        _mockRead.Setup(r => r.GetAsync(
                It.IsAny<Expression<Func<WeatherForecast, bool>>>(),
                It.IsAny<Func<IQueryable<WeatherForecast>, IQueryable<WeatherForecast>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<WeatherForecast>(null!));

        var result = await Handler.HandleAsync(
            Ulid.NewUlid(), WeatherForecastFakers.UpdateRequest(),
            _mockRead.Object, _mockCud.Object,
            BuildContext(), _mockCache.Object, CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, ((IStatusCodeHttpResult)result).StatusCode);
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryFails_ReturnsBadRequest()
    {
        var forecast = WeatherForecastFakers.WeatherForecast().Generate();
        _mockRead.Setup(r => r.GetAsync(
                It.IsAny<Expression<Func<WeatherForecast, bool>>>(),
                It.IsAny<Func<IQueryable<WeatherForecast>, IQueryable<WeatherForecast>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecast);
        _mockCud.Setup(r => r.UpdateAsync(It.IsAny<WeatherForecast>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await Handler.HandleAsync(
            forecast.Id, WeatherForecastFakers.UpdateRequest(),
            _mockRead.Object, _mockCud.Object,
            BuildContext(), _mockCache.Object, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, ((IStatusCodeHttpResult)result).StatusCode);
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
