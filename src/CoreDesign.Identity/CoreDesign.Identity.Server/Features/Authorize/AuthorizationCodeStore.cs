using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace CoreDesign.Identity.Server.Features.Authorize;

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
