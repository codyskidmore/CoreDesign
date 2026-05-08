
using SampleApi.Aspire.ServiceDefaults;

namespace Sample.Blazor.Infrastructure;

public static class App
{
    public static WebApplication AddContextConfiguration(this WebApplication app)
    {
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseAntiforgery();
        app.UseAuthentication();
        app.UseAuthorization();

        // Login endpoint: issues an OIDC challenge and redirects back after sign-in.
        // Accessible without authentication so it cannot create a redirect loop.
        app.MapGet("/account/login", (string? returnUrl, HttpContext context) =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
                [OpenIdConnectDefaults.AuthenticationScheme]));

        // Logout endpoint: signs out of both the cookie and the identity provider.
        app.MapGet("/account/logout", () =>
            Results.SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                [
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    OpenIdConnectDefaults.AuthenticationScheme
                ]));

        app.MapRazorComponents<Components.App>()
            .AddInteractiveServerRenderMode();

        app.MapDefaultEndpoints();

        return app;
    }
}

