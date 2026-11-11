using CoreDesign.UserJourney;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

// Registration helpers live in the namespaces of the types they extend, like ASP.NET Core's own.
namespace Microsoft.Extensions.DependencyInjection
{
    public static class UserJourneyServiceCollectionExtensions
    {
        /// <summary>
        /// Registers CoreDesign.UserJourney and adds its ActivitySource to OpenTelemetry tracing. Call it
        /// before <c>AddServiceDefaults()</c> or <c>UseAzureMonitor()</c> so it runs before their exporters.
        /// Then add <c>app.UseUserJourney()</c> after authentication and authorization.
        /// </summary>
        public static IServiceCollection AddUserJourney(this IServiceCollection services, Action<UserJourneyOptions>? configure = null)
        {
            var builder = services.AddOptions<UserJourneyOptions>();
            if (configure is not null)
                builder.Configure(configure);

            services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(UserJourneyTelemetry.ActivitySourceName));
            return services;
        }
    }
}

namespace Microsoft.AspNetCore.Builder
{
    public static class UserJourneyApplicationBuilderExtensions
    {
        /// <summary>
        /// Tags each request with the signed-in user and client IP, drops excluded paths and removes query
        /// strings. Place it after <c>UseAuthentication()</c> and <c>UseAuthorization()</c>.
        /// </summary>
        public static IApplicationBuilder UseUserJourney(this IApplicationBuilder app) =>
            app.UseMiddleware<UserJourneyMiddleware>();

        /// <summary>
        /// Maps the endpoint browser apps post page views and clicks to (default
        /// <c>POST /user-journey/events</c>). It requires an authenticated user, because a journey belongs
        /// to a signed-in user; anonymous traffic belongs in an aggregate analytics tool.
        /// </summary>
        public static RouteHandlerBuilder MapUserJourneyEvents(this IEndpointRouteBuilder endpoints)
        {
            var options = endpoints.ServiceProvider.GetRequiredService<IOptions<UserJourneyOptions>>().Value;
            return endpoints.MapPost(options.BrowserEventsPath, BrowserEventHandler.Handle)
                .RequireAuthorization()
                .ExcludeFromDescription();
        }
    }
}
