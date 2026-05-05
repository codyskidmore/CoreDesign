using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

namespace CoreDesign.Identity.Server.Features.UserInfo;

public static class UserInfoEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/connect/userinfo", Handle)
           .WithName("UserInfo")
           .WithTags("OIDC");

    private static async Task<IResult> Handle(
        HttpContext ctx,
        RsaSecurityKey rsaKey,
        IdentityOptions options,
        IIdentityStore identityStore)
    {
        var auth = ctx.Request.Headers.Authorization.ToString();
        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Results.Unauthorized();

        var token = auth["Bearer ".Length..];

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateLifetime = true,
            IssuerSigningKey = rsaKey
        };

        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, validationParams, out _);
            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var identity = await identityStore.FindByIdAsync(sub!);

            if (identity is null)
                return Results.Unauthorized();

            return Results.Ok(new UserinfoResponse
            {
                Sub = identity.UserId,
                Email = identity.Email,
                Name = identity.Name,
                GivenName = identity.GivenName,
                FamilyName = identity.FamilyName,
                Roles = identity.Roles
            });
        }
        catch
        {
            return Results.Unauthorized();
        }
    }
}

public class UserinfoResponse
{
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("given_name")]
    public string GivenName { get; set; } = string.Empty;

    [JsonPropertyName("family_name")]
    public string FamilyName { get; set; } = string.Empty;

    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = [];
}
