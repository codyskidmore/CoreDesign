using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CoreDesign.ExceptionHandling.Tests.Helpers;

public static class TestHost
{
    public static async Task<(HttpClient Client, WebApplication App)> StartAsync(
        IProblemDetailsMapper? mapper = null,
        string environmentName = "Production",
        bool registerMapperAfterHandling = false)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = environmentName });
        builder.WebHost.UseTestServer();

        void RegisterMapper()
        {
            if (mapper is not null)
                builder.Services.AddSingleton(mapper);
        }

        if (!registerMapperAfterHandling) RegisterMapper();
        builder.Services.AddCoreDesignExceptionHandling();
        if (registerMapperAfterHandling) RegisterMapper();

        var app = builder.Build();
        app.UseExceptionHandler();

        app.MapGet("/throw/{name}", (string name) => Throw(name));

        // Binding failure: minimal APIs throw BadHttpRequestException for an unparseable route
        // value when RouteHandlerOptions.ThrowOnBadRequest is on (the Development default).
        app.MapGet("/bind/{id}", (int id) => Results.Ok(id));

        await app.StartAsync();
        return (app.GetTestClient(), app);
    }

    private static IResult Throw(string name) => name switch
    {
        "not-found" => throw new EntityNotFoundException("missing"),
        "domain"    => throw new DomainException("bad state"),
        "secret"    => throw new SecretException("shh"),
        "exact"     => throw new ExactOnlyException("exact"),
        "exact-sub" => throw new ExactOnlySubException("sub"),
        "too-large" => throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge),
        "bad-mapped" => throw new MappedBadHttpRequestException("mapped bad request"),
        _           => throw new InvalidOperationException("boom")
    };
}
