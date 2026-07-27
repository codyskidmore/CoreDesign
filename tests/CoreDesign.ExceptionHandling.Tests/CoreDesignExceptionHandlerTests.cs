using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoreDesign.ExceptionHandling.Tests.Helpers;

namespace CoreDesign.ExceptionHandling.Tests;

public class CoreDesignExceptionHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task MappedException_ReturnsMappedStatusAndProblemDetails()
    {
        var (client, app) = await TestHost.StartAsync(new GeneratedProblemDetailsMapperStandIn());
        await using var _ = app;

        var response = await client.GetAsync("/throw/not-found");
        var body = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Not found", body!.Title);
        Assert.Equal("missing", body.Detail);
    }

    [Fact]
    public async Task UnmappedException_Production_FallsBackTo500WithNoDetail()
    {
        var (client, app) = await TestHost.StartAsync(new GeneratedProblemDetailsMapperStandIn(), environmentName: "Production");
        await using var _ = app;

        var response = await client.GetAsync("/throw/unknown");
        var body = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(body!.Detail);
    }

    [Fact]
    public async Task UnmappedException_Development_FallsBackTo500WithExceptionDetail()
    {
        var (client, app) = await TestHost.StartAsync(new GeneratedProblemDetailsMapperStandIn(), environmentName: "Development");
        await using var _ = app;

        var response = await client.GetAsync("/throw/unknown");
        var body = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.NotNull(body!.Detail);
        Assert.Contains("InvalidOperationException", body.Detail);
    }

    [Fact]
    public async Task ZeroConfig_NoMapperRegistered_StillReturns500ForAnyException()
    {
        var (client, app) = await TestHost.StartAsync(mapper: null);
        await using var _ = app;

        var response = await client.GetAsync("/throw/not-found");
        var body = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("An unexpected error occurred", body!.Title);
    }

    [Fact]
    public async Task GeneratedMapper_RegisteredAfterAddCoreDesignExceptionHandling_StillWins()
    {
        var (client, app) = await TestHost.StartAsync(
            new GeneratedProblemDetailsMapperStandIn(),
            registerMapperAfterHandling: true);
        await using var _ = app;

        var response = await client.GetAsync("/throw/not-found");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
