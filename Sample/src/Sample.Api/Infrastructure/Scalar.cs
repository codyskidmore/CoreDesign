using Scalar.AspNetCore;

namespace Sample.Api.Infrastructure;

public static class Scalar
{
    public static WebApplication ConfigureScalar(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.MapOpenApi().AllowAnonymous();

        app.MapScalarApiReference(options =>
        {
            options.Servers = [];
            options.WithTheme(ScalarTheme.BluePlanet)
                .WithDefaultHttpClient(ScalarTarget.JavaScript, ScalarClient.HttpClient);
        }).AllowAnonymous();

        return app;
    }
}
