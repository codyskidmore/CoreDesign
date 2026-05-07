namespace CoreDesign.Identity.Server.Features.Token;

public static class TokenEndpoint
{
    private static readonly string[] ServerSupportedGrants = ["password"];

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/connect/token", Handle)
           .WithName("Token")
           .WithTags("OIDC");

    private static async Task<IResult> Handle(
        HttpContext ctx,
        SigningCredentials creds,
        IIdentityStore identityStore,
        IClientStore clientStore,
        IdentityOptions options)
    {
        if (!ctx.Request.HasFormContentType)
            return Results.BadRequest(new OidcError("invalid_request", "Content-Type must be application/x-www-form-urlencoded"));

        var form = await ctx.Request.ReadFormAsync();
        var grantType = form["grant_type"].ToString();
        var clientId = form["client_id"].ToString();

        if (string.IsNullOrEmpty(clientId))
            return Results.Json(new OidcError("invalid_client", "client_id is required"),
                statusCode: StatusCodes.Status401Unauthorized);

        var client = await clientStore.FindByClientIdAsync(clientId);
        if (client is null)
            return Results.Json(new OidcError("invalid_client", "Unknown client_id"),
                statusCode: StatusCodes.Status401Unauthorized);

        if (!ServerSupportedGrants.Contains(grantType))
            return Results.BadRequest(new OidcError("unsupported_grant_type", $"Grant type '{grantType}' is not supported"));

        if (!client.AllowedGrantTypes.Contains(grantType))
            return Results.BadRequest(new OidcError("unauthorized_client", $"Client is not authorized for grant type '{grantType}'"));

        var username = form["username"].ToString();
        var password = form["password"].ToString();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return Results.BadRequest(new OidcError("invalid_request", "username and password are required"));

        var identity = await identityStore.FindByCredentialsAsync(username, password);
        if (identity is null)
            return Results.BadRequest(new OidcError("invalid_grant", "Invalid username or password"));

        var jwt = TokenBuilder.Build(identity, creds, options);
        return Results.Ok(new TokenResponse
        {
            AccessToken = jwt,
            IdToken = jwt,
            TokenType = "Bearer",
            ExpiresIn = options.TokenLifetimeHours * 3600,
            Scope = "openid profile email"
        });
    }
}
