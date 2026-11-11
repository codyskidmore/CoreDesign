using CoreDesign.Identity.Client;

namespace Sample.Api.Infrastructure;

public static class App
{
    public static WebApplication AddContextConfiguration(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseCors();
        app.UseLocalBearerTokenInjection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseUserJourney();
        app.UseOutputCache();
        app.MapEndpoints();
        app.MapUserJourneyEvents();
        app.ConfigureScalar();

        return app;
    }
}
