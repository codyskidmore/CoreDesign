namespace CoreDesign.Identity.Server.Features.AuthLogin;

public static class AuthLoginEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/auth/login", Handle)
           .WithName("AuthLogin")
           .WithTags("Auth")
           .AllowAnonymous();

    private static async Task<IResult> Handle(
        AuthLoginRequest request,
        IIdentityStore identityStore,
        SigningCredentials creds,
        IdentityOptions options)
    {
        var identity = await identityStore.FindByCredentialsAsync(request.Username, request.Password);
        if (identity is null)
            return Results.Problem("Invalid username or password", statusCode: StatusCodes.Status401Unauthorized);

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

public record AuthLoginRequest(string Username, string Password);
