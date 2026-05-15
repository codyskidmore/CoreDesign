using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Sample.Api.Data;
using Sample.Api.WeatherForecasts.Delete;
using Sample.Api.WeatherForecasts.Shared;
using CoreDesign.Data.Interfaces;
using Moq;

namespace Sample.Api.Tests.WeatherForecasts.Delete;

public class DeleteHandlerTests
{
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
        _mockCud.Setup(r => r.DeleteAsync(It.IsAny<Ulid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Handler.HandleAsync(
            Ulid.NewUlid(), _mockCud.Object, BuildContext(), _mockCache.Object, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, ((IStatusCodeHttpResult)result).StatusCode);
    }

    [Fact]
    public async Task HandleAsync_WhenForecastNotFound_ReturnsNotFound()
    {
        _mockCud.Setup(r => r.DeleteAsync(It.IsAny<Ulid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await Handler.HandleAsync(
            Ulid.NewUlid(), _mockCud.Object, BuildContext(), _mockCache.Object, CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, ((IStatusCodeHttpResult)result).StatusCode);
    }
}
