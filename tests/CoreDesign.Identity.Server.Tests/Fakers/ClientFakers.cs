using Bogus;
using CoreDesign.Identity.Server.Clients;

namespace CoreDesign.Identity.Server.Tests.Fakers;

public static class ClientFakers
{
    public static Faker<ClientRecord> ClientRecord(string[]? grantTypes = null) =>
        new Faker<ClientRecord>()
            .CustomInstantiator(f => new ClientRecord
            {
                ClientId = f.Random.AlphaNumeric(12),
                ClientSecret = null,
                TokenEndpointAuthMethod = "none",
                AllowedGrantTypes = grantTypes?.ToList() ?? ["password"],
                AllowedRedirectUris = [$"https://{f.Internet.DomainName()}/callback"],
                AllowedPostLogoutRedirectUris = [$"https://{f.Internet.DomainName()}"],
                AllowedScopes = ["openid", "profile", "email"],
                RequirePkce = false
            });
}
