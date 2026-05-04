namespace SampleApi.Api.Infrastructure;

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
