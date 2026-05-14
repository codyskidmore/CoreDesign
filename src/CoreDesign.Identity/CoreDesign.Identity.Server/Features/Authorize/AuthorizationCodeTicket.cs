namespace CoreDesign.Identity.Server.Features.Authorize;

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
