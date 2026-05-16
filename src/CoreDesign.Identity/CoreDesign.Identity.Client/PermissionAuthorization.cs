using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoreDesign.Identity.Client;

public record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Claims.Any(c => c.Type == "permissions" && c.Value == requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

public class PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var existing = await base.GetPolicyAsync(policyName);
        if (existing is not null)
            return existing;

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();
    }
}

public static class PermissionAuthorizationExtensions
{
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        // AddSingleton (not TryAdd) ensures this overrides the DefaultAuthorizationPolicyProvider
        // that AddAuthorization registers, so permission-string policies are resolved correctly.
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();

        // TryAddEnumerable adds to the handler collection without duplicating if called twice.
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAuthorizationHandler, PermissionAuthorizationHandler>());

        return services;
    }
}
