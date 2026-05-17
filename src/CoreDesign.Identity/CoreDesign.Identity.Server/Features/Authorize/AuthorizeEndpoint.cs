using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Hosting;

namespace CoreDesign.Identity.Server.Features.Authorize;

public static class AuthorizeEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapMethods("/connect/authorize", ["GET", "POST"], Handle)
            .WithName("Authorize")
            .WithTags("OIDC")
            .AllowAnonymous();
    }

    private static async Task<IResult> Handle(
        HttpContext ctx,
        IClientStore clientStore,
        IIdentityStore identityStore,
        IWebHostEnvironment env)
    {
        var request = await AuthorizeRequest.FromHttpAsync(ctx);
        var validationError = await ValidateRequestAsync(request, clientStore);
        if (validationError is not null)
            return validationError;

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return LoginPage(request, env);

        var identity = await identityStore.FindByCredentialsAsync(request.Username, request.Password);
        if (identity is null)
            return LoginPage(request, env, "Invalid username or password", StatusCodes.Status401Unauthorized);

        var code = AuthorizationCodeStore.Issue(new AuthorizationCodeTicket
        {
            ClientId            = request.ClientId,
            RedirectUri         = request.RedirectUri,
            UserId              = identity.UserId,
            Scope               = request.Scope,
            CodeChallenge       = request.CodeChallenge,
            CodeChallengeMethod = request.CodeChallengeMethod,
            Nonce               = request.Nonce
        });

        var redirectUri = BuildRedirect(request.RedirectUri, code, request.State);
        return Results.Redirect(redirectUri);
    }

    private static IResult LoginPage(
        AuthorizeRequest request,
        IWebHostEnvironment env,
        string? error = null,
        int statusCode = StatusCodes.Status200OK)
    {
        var enc = HtmlEncoder.Default;
        var template = TemplateLoader.Load("login.html", env);
        var errorAlert = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : TemplateLoader.Render(
                TemplateLoader.Load("login-error.html", env),
                new Dictionary<string, string>
                {
                    ["error_message"] = enc.Encode(error)
                });

        var html = TemplateLoader.Render(template, new Dictionary<string, string>
        {
            ["response_type"]         = enc.Encode(request.ResponseType),
            ["client_id"]             = enc.Encode(request.ClientId),
            ["redirect_uri"]          = enc.Encode(request.RedirectUri),
            ["scope"]                 = enc.Encode(request.Scope),
            ["state"]                 = enc.Encode(request.State ?? string.Empty),
            ["nonce"]                 = enc.Encode(request.Nonce ?? string.Empty),
            ["code_challenge"]        = enc.Encode(request.CodeChallenge ?? string.Empty),
            ["code_challenge_method"] = enc.Encode(request.CodeChallengeMethod ?? string.Empty),
            ["error_alert"]           = errorAlert
        });

        return Results.Content(html, "text/html; charset=utf-8", Encoding.UTF8, statusCode);
    }

    private static async Task<IResult?> ValidateRequestAsync(AuthorizeRequest request, IClientStore clientStore)
    {
        if (!string.Equals(request.ResponseType, "code", StringComparison.Ordinal))
            return Results.BadRequest(new OidcError("unsupported_response_type", "Only response_type=code is supported"));

        if (string.IsNullOrWhiteSpace(request.ClientId))
            return Results.BadRequest(new OidcError("invalid_request", "client_id is required"));

        if (string.IsNullOrWhiteSpace(request.RedirectUri))
            return Results.BadRequest(new OidcError("invalid_request", "redirect_uri is required"));

        if (string.IsNullOrWhiteSpace(request.Scope))
            return Results.BadRequest(new OidcError("invalid_request", "scope is required"));

        if (!request.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("openid", StringComparer.Ordinal))
            return Results.BadRequest(new OidcError("invalid_scope", "openid scope is required"));

        var client = await clientStore.FindByClientIdAsync(request.ClientId);
        if (client is null)
            return Results.BadRequest(new OidcError("invalid_client", "Unknown client_id"));

        if (!client.AllowedGrantTypes.Contains("authorization_code", StringComparer.Ordinal))
            return Results.BadRequest(new OidcError("unauthorized_client", "Client is not authorized for authorization_code grant"));

        if (!client.AllowedRedirectUris.Contains(request.RedirectUri, StringComparer.Ordinal))
            return Results.BadRequest(new OidcError("invalid_request", "redirect_uri is not registered for this client"));

        if (client.RequirePkce)
        {
            if (string.IsNullOrWhiteSpace(request.CodeChallenge))
                return Results.BadRequest(new OidcError("invalid_request", "code_challenge is required"));

            if (!string.Equals(request.CodeChallengeMethod, "S256", StringComparison.Ordinal))
                return Results.BadRequest(new OidcError("invalid_request", "code_challenge_method must be S256"));
        }

        return null;
    }

    private static string BuildRedirect(string redirectUri, string code, string? state)
    {
        var separator = redirectUri.Contains('?') ? "&" : "?";
        var statePart = string.IsNullOrEmpty(state) ? string.Empty : $"&state={Uri.EscapeDataString(state)}";
        return $"{redirectUri}{separator}code={Uri.EscapeDataString(code)}{statePart}";
    }
}
