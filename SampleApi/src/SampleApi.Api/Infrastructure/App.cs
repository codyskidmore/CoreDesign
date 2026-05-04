using CoreDesign.Identity.Client;

namespace SampleApi.Api.Infrastructure;

public static class App
{
    public static WebApplication AddContextConfiguration(this WebApplication app)
    {
        app.UseHttpsRedirection();
        app.UseCors();
        app.UseLocalBearerTokenInjection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseOutputCache();
        app.MapEndpoints();
        app.ConfigureScalar();

        return app;
    }
}
