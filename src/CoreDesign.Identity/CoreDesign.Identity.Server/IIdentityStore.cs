namespace CoreDesign.Identity.Server;

public interface IIdentityStore
{
    Task<IdentityRecord?> FindByCredentialsAsync(string username, string password);
    Task<IdentityRecord?> FindByIdAsync(string userId);
}
