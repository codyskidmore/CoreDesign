namespace CoreDesign.Identity.Server.Features.Token;

public static class TokenEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/connect/token", Handle)
           .WithName("Token")
           .WithTags("OIDC");

    private static async Task<IResult> Handle(
        HttpContext ctx,
        SigningCredentials creds,
        IIdentityStore identityStore,
        IdentityOptions options)
    {
        if (!ctx.Request.HasFormContentType)
            return Results.BadRequest(new OidcError("invalid_request", "Content-Type must be application/x-www-form-urlencoded"));

        var form = await ctx.Request.ReadFormAsync();
        var grantType = form["grant_type"].ToString();

        if (grantType != "password")
            return Results.BadRequest(new OidcError("unsupported_grant_type", $"Grant type '{grantType}' is not supported"));

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
