using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sample.Api.Data;
using Sample.Api.WeatherForecasts.GetAll;
using Sample.Api.WeatherForecasts.Shared;
using CoreDesign.Data.Interfaces;
using Moq;

namespace Sample.Api.Tests.WeatherForecasts.GetAll;

public class GetAllHandlerTests
{
    private readonly Mock<IReadRepository<SampleDbContext, WeatherForecast>> _mockRead = new();

    private static HttpContext BuildContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("oid", Guid.NewGuid().ToString())]));
        return ctx;
    }

    [Fact]
    public async Task HandleAsync_WhenForecastsExist_ReturnsOk()
    {
        var forecasts = WeatherForecastFakers.WeatherForecast().Generate(3);
        _mockRead.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecasts);

        var result = await Handler.HandleAsync(_mockRead.Object, BuildContext(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, ((IStatusCodeHttpResult)result).StatusCode);
    }

    [Fact]
    public async Task HandleAsync_WhenNoForecasts_ReturnsNotFound()
    {
        _mockRead.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await Handler.HandleAsync(_mockRead.Object, BuildContext(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, ((IStatusCodeHttpResult)result).StatusCode);
    }
}
