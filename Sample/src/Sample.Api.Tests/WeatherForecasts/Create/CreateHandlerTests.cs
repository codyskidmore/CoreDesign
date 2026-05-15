using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Sample.Api.Data;
using Sample.Api.WeatherForecasts.Create;
using Sample.Api.WeatherForecasts.Shared;
using CoreDesign.Data.Interfaces;
using Moq;

namespace Sample.Api.Tests.WeatherForecasts.Create;

public class CreateHandlerTests
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
    public async Task HandleAsync_WhenRepositorySucceeds_ReturnsCreated()
    {
        _mockCud.Setup(r => r.InsertAsync(It.IsAny<WeatherForecast>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Handler.HandleAsync(
            WeatherForecastFakers.CreateRequest(),
            _mockCud.Object,
            BuildContext(),
            _mockCache.Object,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, ((IStatusCodeHttpResult)result).StatusCode);
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryFails_ReturnsBadRequest()
    {
        _mockCud.Setup(r => r.InsertAsync(It.IsAny<WeatherForecast>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await Handler.HandleAsync(
            WeatherForecastFakers.CreateRequest(),
            _mockCud.Object,
            BuildContext(),
            _mockCache.Object,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, ((IStatusCodeHttpResult)result).StatusCode);
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
