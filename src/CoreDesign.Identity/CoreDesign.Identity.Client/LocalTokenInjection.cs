using Microsoft.Extensions.Hosting;

namespace CoreDesign.Identity.Client;

internal static class LocalTokenInjection
{
    /// <summary>An explicit setting wins; when unset, injection is on in Development only.</summary>
    public static bool IsEnabled(bool? setting, IHostEnvironment environment) =>
        setting ?? environment.IsDevelopment();
}
