namespace CoreDesign.Identity.Server.Clients;

public interface IClientStore
{
    Task<ClientRecord?> FindByClientIdAsync(string clientId);
}
