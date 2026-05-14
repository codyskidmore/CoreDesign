namespace CoreDesign.Identity.Server.Features.Authorize;

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
            ResponseType        = Read("response_type"),
            ClientId            = Read("client_id"),
            RedirectUri         = Read("redirect_uri"),
            Scope               = Read("scope"),
            State               = ReadOptional("state"),
            Nonce               = ReadOptional("nonce"),
            CodeChallenge       = ReadOptional("code_challenge"),
            CodeChallengeMethod = ReadOptional("code_challenge_method"),
            Username            = ReadOptional("username"),
            Password            = ReadOptional("password")
        };
    }
}
