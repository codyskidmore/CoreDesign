namespace Sample.Api.Infrastructure;

public static class AuthorizationRoles
{
    public const string DevAdmin = "DevAdmin";
    public const string DevAppUsers = "DevAppUsers";
    public const string UatAdmin = "UATAdmin";
    public const string UatUsers = "UATUsers";
    public const string ProdAdmin = "AdminUsers";
    public const string ProdUsers = "AppUsers";

    public const string AdminOnlyPolicy = "AdminOnly";
    public const string UserOrAdminPolicy = "UserOrAdmin";
}

public static class Authorization
{
    public static void AddAuthorizationPolicyConfiguration(this IHostApplicationBuilder builder)
    {
        var environmentName = Enum.Parse<EnvironmentName>(builder.Environment.EnvironmentName);
        var services = builder.Services;

        services.AddAuthorization(options =>
        {
            switch (environmentName)
            {
                case EnvironmentName.Development:
                case EnvironmentName.AzureDev:
                    options.AddPolicy(AuthorizationRoles.AdminOnlyPolicy,
                        policy => policy.RequireRole(AuthorizationRoles.DevAdmin));
                    options.AddPolicy(AuthorizationRoles.UserOrAdminPolicy,
                        policy => policy.RequireRole(AuthorizationRoles.DevAppUsers, AuthorizationRoles.DevAdmin));
                    break;

                case EnvironmentName.UAT:
                    options.AddPolicy(AuthorizationRoles.AdminOnlyPolicy,
                        policy => policy.RequireRole(AuthorizationRoles.UatAdmin));
                    options.AddPolicy(AuthorizationRoles.UserOrAdminPolicy,
                        policy => policy.RequireRole(AuthorizationRoles.UatUsers, AuthorizationRoles.UatAdmin));
                    break;

                case EnvironmentName.Production:
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
