using System.Security.Claims;
using CoreDesign.Identity.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoreDesign.Identity.Client.Tests;

public class PermissionAuthorizationHandlerTests
{
    private static AuthorizationHandlerContext BuildContext(
        PermissionRequirement requirement,
        IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        return new AuthorizationHandlerContext([requirement], principal, null);
    }

    [Fact]
    public async Task HandleRequirementAsync_Succeeds_WhenPermissionsClaimMatches()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement("items:read");
        var context = BuildContext(requirement, [new Claim("permissions", "items:read")]);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_Succeeds_WhenOneOfMultipleClaimsMatches()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement("items:write");
        var context = BuildContext(requirement, [
            new Claim("permissions", "items:read"),
            new Claim("permissions", "items:write")
        ]);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_DoesNotSucceed_WhenPermissionsClaimAbsent()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement("items:read");
        var context = BuildContext(requirement, [new Claim("email", "user@example.com")]);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_DoesNotSucceed_WhenClaimValueDoesNotMatch()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement("items:write");
        var context = BuildContext(requirement, [new Claim("permissions", "items:read")]);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_DoesNotSucceed_WhenUserHasNoClaims()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement("items:read");
        var context = BuildContext(requirement, []);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}

public class PermissionAuthorizationPolicyProviderTests
{
    private static PermissionAuthorizationPolicyProvider BuildProvider(
        Action<AuthorizationOptions>? configure = null)
    {
        var options = new AuthorizationOptions();
        configure?.Invoke(options);
        return new PermissionAuthorizationPolicyProvider(Options.Create(options));
    }

    [Fact]
    public async Task GetPolicyAsync_ReturnsExistingPolicy_WhenAlreadyDefined()
    {
        var provider = BuildProvider(opts =>
            opts.AddPolicy("MyPolicy", p => p.RequireAuthenticatedUser()));

        var policy = await provider.GetPolicyAsync("MyPolicy");

        Assert.NotNull(policy);
    }

    [Fact]
    public async Task GetPolicyAsync_ReturnsPermissionPolicy_ForUnknownPolicyName()
    {
        var provider = BuildProvider();

        var policy = await provider.GetPolicyAsync("items:read");

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, r => r is PermissionRequirement req && req.Permission == "items:read");
    }

    [Fact]
    public async Task GetPolicyAsync_PermissionPolicy_RequiresAuthenticatedUser()
    {
        var provider = BuildProvider();

        var policy = await provider.GetPolicyAsync("items:write");

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }
}

public class AddPermissionAuthorizationTests
{
    [Fact]
    public void AddPermissionAuthorization_RegistersPolicyProvider()
    {
        var services = new ServiceCollection();
        services.AddPermissionAuthorization();
        var provider = services.BuildServiceProvider();

        var policyProvider = provider.GetService<IAuthorizationPolicyProvider>();

        Assert.IsType<PermissionAuthorizationPolicyProvider>(policyProvider);
    }

    [Fact]
    public void AddPermissionAuthorization_RegistersHandler()
    {
        var services = new ServiceCollection();
        services.AddPermissionAuthorization();
        var provider = services.BuildServiceProvider();

        var handlers = provider.GetServices<IAuthorizationHandler>().ToList();

        Assert.Contains(handlers, h => h is PermissionAuthorizationHandler);
    }

    [Fact]
    public void AddPermissionAuthorization_SetsFallbackPolicyToRequireAuthenticatedUser()
    {
        var services = new ServiceCollection();
        services.AddPermissionAuthorization();
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        Assert.NotNull(options.FallbackPolicy);
        Assert.Contains(options.FallbackPolicy!.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }
}
