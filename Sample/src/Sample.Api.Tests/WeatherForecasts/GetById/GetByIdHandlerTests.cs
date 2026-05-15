using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sample.Api.Data;
using Sample.Api.WeatherForecasts.GetById;
using Sample.Api.WeatherForecasts.Shared;
using CoreDesign.Data.Interfaces;
using Moq;

namespace Sample.Api.Tests.WeatherForecasts.GetById;

public class GetByIdHandlerTests
{
    private readonly Mock<IReadRepository<SampleDbContext, WeatherForecast>> _mockRead = new();

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

        var result = await Handler.HandleAsync(
            forecast.Id, _mockRead.Object, BuildContext(), CancellationToken.None);

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
            Ulid.NewUlid(), _mockRead.Object, BuildContext(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, ((IStatusCodeHttpResult)result).StatusCode);
    }
}
