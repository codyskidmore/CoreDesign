using Bogus;
using CoreDesign.Identity.Server;

namespace CoreDesign.Identity.Server.Tests.Fakers;

public static class IdentityFakers
{
    public static Faker<IdentityRecord> IdentityRecord() =>
        new Faker<IdentityRecord>()
            .RuleFor(x => x.UserId, f => Guid.NewGuid().ToString())
            .RuleFor(x => x.Username, f => f.Internet.UserName())
            .RuleFor(x => x.Password, f => f.Internet.Password())
            .RuleFor(x => x.Email, f => f.Internet.Email())
            .RuleFor(x => x.Name, f => f.Name.FullName())
            .RuleFor(x => x.GivenName, f => f.Name.FirstName())
            .RuleFor(x => x.FamilyName, f => f.Name.LastName())
            .RuleFor(x => x.Roles, f => f.Make(2, () => f.PickRandom("admin", "user", "viewer")).ToList())
            .RuleFor(x => x.CustomClaims, _ => new Dictionary<string, string>());
}
