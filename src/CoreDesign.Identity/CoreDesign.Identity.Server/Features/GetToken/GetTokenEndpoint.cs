using Microsoft.IdentityModel.Tokens;

namespace CoreDesign.Identity.Server.Features.GetToken;

public static class GetTokenEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/get-token", Handle)
           .WithName("GetToken")
           .WithTags("OIDC");

    private static async Task<IResult> Handle(
        TokenGenerationRequest request,
        IIdentityStore identityStore,
        SigningCredentials creds,
        IdentityOptions options)
    {
        var identity = await identityStore.FindByCredentialsAsync(request.Username, request.Password);
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

public record TokenGenerationRequest(string Username, string Password);
