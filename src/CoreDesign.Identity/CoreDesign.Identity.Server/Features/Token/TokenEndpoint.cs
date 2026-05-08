using CoreDesign.Identity.Server.Features.Authorize;

namespace CoreDesign.Identity.Server.Features.Token;

public static class TokenEndpoint
{
    private static readonly string[] ServerSupportedGrants = ["password", "authorization_code"];

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
        var clientSecret = form["client_secret"].ToString();

        if (string.IsNullOrEmpty(clientId))
            return Results.Json(new OidcError("invalid_client", "client_id is required"),
                statusCode: StatusCodes.Status401Unauthorized);

        var client = await clientStore.FindByClientIdAsync(clientId);
        if (client is null)
            return Results.Json(new OidcError("invalid_client", "Unknown client_id"),
                statusCode: StatusCodes.Status401Unauthorized);

        if (!IsClientAuthenticated(client, clientSecret, out var authError, out var authStatusCode))
            return Results.Json(authError, statusCode: authStatusCode);

        if (!ServerSupportedGrants.Contains(grantType))
            return Results.BadRequest(new OidcError("unsupported_grant_type", $"Grant type '{grantType}' is not supported"));

        if (!client.AllowedGrantTypes.Contains(grantType))
            return Results.BadRequest(new OidcError("unauthorized_client", $"Client is not authorized for grant type '{grantType}'"));

        return grantType switch
        {
            "password" => await HandlePasswordGrant(form, identityStore, creds, options),
            "authorization_code" => await HandleAuthorizationCodeGrant(form, client, identityStore, creds, options),
            _ => Results.BadRequest(new OidcError("unsupported_grant_type", $"Grant type '{grantType}' is not supported"))
        };
    }

    private static async Task<IResult> HandlePasswordGrant(
        IFormCollection form,
        IIdentityStore identityStore,
        SigningCredentials creds,
        IdentityOptions options)
    {
        var username = form["username"].ToString();
        var password = form["password"].ToString();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return Results.BadRequest(new OidcError("invalid_request", "username and password are required"));

        var identity = await identityStore.FindByCredentialsAsync(username, password);
        if (identity is null)
            return Results.BadRequest(new OidcError("invalid_grant", "Invalid username or password"));

        return BuildTokenResponse(identity, creds, options);
    }

    private static async Task<IResult> HandleAuthorizationCodeGrant(
        IFormCollection form,
        ClientRecord client,
        IIdentityStore identityStore,
        SigningCredentials creds,
        IdentityOptions options)
    {
        var code = form["code"].ToString();
        var redirectUri = form["redirect_uri"].ToString();
        var codeVerifier = form["code_verifier"].ToString();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(redirectUri))
            return Results.BadRequest(new OidcError("invalid_request", "code and redirect_uri are required"));

        if (!AuthorizationCodeStore.TryConsume(code, out var ticket) || ticket is null)
            return Results.BadRequest(new OidcError("invalid_grant", "Invalid or expired authorization code"));

        if (!string.Equals(ticket.ClientId, client.ClientId, StringComparison.Ordinal))
            return Results.BadRequest(new OidcError("invalid_grant", "Authorization code was not issued to this client"));

        if (!string.Equals(ticket.RedirectUri, redirectUri, StringComparison.Ordinal))
            return Results.BadRequest(new OidcError("invalid_grant", "redirect_uri does not match the authorization request"));

        if (client.RequirePkce)
        {
            if (string.IsNullOrWhiteSpace(ticket.CodeChallenge) ||
                !AuthorizationCodeStore.ValidateCodeVerifier(codeVerifier, ticket.CodeChallenge, ticket.CodeChallengeMethod))
                return Results.BadRequest(new OidcError("invalid_grant", "Invalid code_verifier"));
        }

        var identity = await identityStore.FindByIdAsync(ticket.UserId);
        if (identity is null)
            return Results.BadRequest(new OidcError("invalid_grant", "User no longer exists"));

        return BuildTokenResponse(identity, creds, options);
    }

    private static IResult BuildTokenResponse(IdentityRecord identity, SigningCredentials creds, IdentityOptions options)
    {
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

    private static bool IsClientAuthenticated(
        ClientRecord client,
        string clientSecret,
        out OidcError? error,
        out int statusCode)
    {
        error = null;
        statusCode = StatusCodes.Status401Unauthorized;

        if (string.Equals(client.TokenEndpointAuthMethod, "none", StringComparison.Ordinal))
            return true;

        if (string.Equals(client.TokenEndpointAuthMethod, "client_secret_post", StringComparison.Ordinal))
        {
            if (string.IsNullOrEmpty(client.ClientSecret) || !string.Equals(client.ClientSecret, clientSecret, StringComparison.Ordinal))
            {
                error = new OidcError("invalid_client", "Invalid client authentication");
                return false;
            }

            return true;
        }

        error = new OidcError("invalid_client", "Unsupported token endpoint authentication method");
        return false;
    }
}
