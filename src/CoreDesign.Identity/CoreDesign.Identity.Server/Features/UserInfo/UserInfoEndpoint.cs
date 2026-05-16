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
        IIdentityStore identityStore,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("CoreDesign.Identity.Server.UserInfo");

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
                Permissions = identity.Permissions
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Bearer token validation failed for userinfo request from {RemoteIp}",
                ctx.Connection.RemoteIpAddress);
            return Results.Unauthorized();
        }
    }
}
