namespace SampleApi.Api.Infrastructure;

public static class AuthorizationPolicyConfiguration
{
    public static void AddAuthorizationPolicyConfiguration(this IHostApplicationBuilder builder)
    {
        var environmentName = builder.Environment.EnvironmentName;
        var services = builder.Services;

        services.AddAuthorization(options =>
        {
            switch (environmentName)
            {
                case "Development":
                case "AzureDev":
                    options.AddPolicy(AuthorizationRoles.AdminOnlyPolicy,
                        policy => policy.RequireRole(AuthorizationRoles.DevAdmin));
                    options.AddPolicy(AuthorizationRoles.UserOrAdminPolicy,
                        policy => policy.RequireRole(AuthorizationRoles.DevAppUsers, AuthorizationRoles.DevAdmin));
                    break;

                case "UAT":
                    options.AddPolicy(AuthorizationRoles.AdminOnlyPolicy,
                        policy => policy.RequireRole(AuthorizationRoles.UatAdmin));
                    options.AddPolicy(AuthorizationRoles.UserOrAdminPolicy,
                        policy => policy.RequireRole(AuthorizationRoles.UatUsers, AuthorizationRoles.UatAdmin));
                    break;

                case "Production":
                    options.AddPolicy(AuthorizationRoles.AdminOnlyPolicy,
                        policy => policy.RequireRole(AuthorizationRoles.ProdAdmin));
                    options.AddPolicy(AuthorizationRoles.UserOrAdminPolicy,
                        policy => policy.RequireRole(AuthorizationRoles.ProdUsers, AuthorizationRoles.ProdAdmin));
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unknown environment '{environmentName}' for role configuration");
            }

            options.FallbackPolicy = options.DefaultPolicy;
        });
    }
}
