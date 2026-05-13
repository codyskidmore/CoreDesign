using System.Collections.Concurrent;
using System.Security.Cryptography;
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
            ClientId = request.ClientId,
            RedirectUri = request.RedirectUri,
            UserId = identity.UserId,
            Scope = request.Scope,
            CodeChallenge = request.CodeChallenge,
            CodeChallengeMethod = request.CodeChallengeMethod,
            Nonce = request.Nonce
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
            : $"""
    <div class="id-error" role="alert">
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor"
           aria-hidden="true">
        <path fill-rule="evenodd"
              d="M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0zm-8-5a.75.75 0 0 1 .75.75v4.5a.75.75 0 0 1-1.5 0v-4.5A.75.75 0 0 1 10 5zm0 10a1 1 0 1 0 0-2 1 1 0 0 0 0 2z"
              clip-rule="evenodd"/>
      </svg>
      <span>{enc.Encode(error)}</span>
    </div>
""";

        var html = TemplateLoader.Render(template, new Dictionary<string, string>
        {
            ["response_type"]        = enc.Encode(request.ResponseType),
            ["client_id"]            = enc.Encode(request.ClientId),
            ["redirect_uri"]         = enc.Encode(request.RedirectUri),
            ["scope"]                = enc.Encode(request.Scope),
            ["state"]                = enc.Encode(request.State ?? string.Empty),
            ["nonce"]                = enc.Encode(request.Nonce ?? string.Empty),
            ["code_challenge"]       = enc.Encode(request.CodeChallenge ?? string.Empty),
            ["code_challenge_method"]= enc.Encode(request.CodeChallengeMethod ?? string.Empty),
            ["error_alert"]          = errorAlert
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

internal static class AuthorizationCodeStore
{
    private static readonly ConcurrentDictionary<string, AuthorizationCodeTicket> Tickets = new();
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public static string Issue(AuthorizationCodeTicket ticket)
    {
        var code = Guid.NewGuid().ToString("N");
        ticket.ExpiresAtUtc = DateTime.UtcNow.Add(Lifetime);
        Tickets[code] = ticket;
        return code;
    }

    public static bool TryConsume(string code, out AuthorizationCodeTicket? ticket)
    {
        ticket = null;
        if (!Tickets.TryRemove(code, out var current))
            return false;

        if (current.ExpiresAtUtc < DateTime.UtcNow)
            return false;

        ticket = current;
        return true;
    }

    public static bool ValidateCodeVerifier(string codeVerifier, string codeChallenge, string? method)
    {
        if (string.IsNullOrWhiteSpace(codeVerifier) || !IsValidCodeVerifier(codeVerifier))
            return false;

        if (!string.Equals(method, "S256", StringComparison.Ordinal))
            return false;

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return string.Equals(Base64UrlEncoder.Encode(hash), codeChallenge, StringComparison.Ordinal);
    }

    private static bool IsValidCodeVerifier(string codeVerifier)
    {
        if (codeVerifier.Length < 43 || codeVerifier.Length > 128)
            return false;

        foreach (var ch in codeVerifier)
        {
            if ((ch >= 'A' && ch <= 'Z') ||
                (ch >= 'a' && ch <= 'z') ||
                (ch >= '0' && ch <= '9') ||
                ch is '-' or '.' or '_' or '~')
                continue;

            return false;
        }

        return true;
    }
}

internal sealed class AuthorizationCodeTicket
{
    public string ClientId { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string? CodeChallenge { get; init; }
    public string? CodeChallengeMethod { get; init; }
    public string? Nonce { get; init; }
    public DateTime ExpiresAtUtc { get; set; }
}

internal sealed class AuthorizeRequest
{
    public string ResponseType { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string? State { get; init; }
    public string? Nonce { get; init; }
    public string? CodeChallenge { get; init; }
    public string? CodeChallengeMethod { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }

    public static async Task<AuthorizeRequest> FromHttpAsync(HttpContext ctx)
    {
        IQueryCollection query = ctx.Request.Query;
        IFormCollection? form = null;
        if (ctx.Request.HasFormContentType)
            form = await ctx.Request.ReadFormAsync();

        string Read(string key)
        {
            if (form is not null && form.TryGetValue(key, out var formValue) && !string.IsNullOrWhiteSpace(formValue))
                return formValue.ToString();

            if (query.TryGetValue(key, out var queryValue) && !string.IsNullOrWhiteSpace(queryValue))
                return queryValue.ToString();

            return string.Empty;
        }

        string? ReadOptional(string key)
        {
            var value = Read(key);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return new AuthorizeRequest
        {
            ResponseType     = Read("response_type"),
            ClientId         = Read("client_id"),
            RedirectUri      = Read("redirect_uri"),
            Scope            = Read("scope"),
            State            = ReadOptional("state"),
            Nonce            = ReadOptional("nonce"),
            CodeChallenge    = ReadOptional("code_challenge"),
            CodeChallengeMethod = ReadOptional("code_challenge_method"),
            Username         = ReadOptional("username"),
            Password         = ReadOptional("password")
        };
    }
}
