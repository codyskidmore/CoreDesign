
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

        // Logout endpoint: always clears the local cookie.
        // Only attempt OIDC federated logout when the selected provider supports it.
        app.MapGet("/account/logout", async (HttpContext context, IAuthProviderConfigurator authProvider) =>
        {
            var redirectHome = new AuthenticationProperties { RedirectUri = "/" };

            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!authProvider.SupportsFederatedLogout)
                return Results.Redirect("/");

            await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, redirectHome);
            return Results.Empty;
        });

        app.MapRazorComponents<Components.App>()
            .AddInteractiveServerRenderMode();

        app.MapDefaultEndpoints();

        return app;
    }
}

