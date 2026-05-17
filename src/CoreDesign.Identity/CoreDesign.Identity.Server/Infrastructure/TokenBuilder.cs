namespace CoreDesign.Identity.Server;

internal static class TokenBuilder
{
    internal static string Build(IdentityRecord identity, SigningCredentials creds, IdentityOptions options, string? audience = null, string? nonce = null)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, identity.UserId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Email, identity.Email),
            new(JwtRegisteredClaimNames.PreferredUsername, identity.Email),
            new(JwtRegisteredClaimNames.Name, identity.Name),
            new(JwtRegisteredClaimNames.GivenName, identity.GivenName),
            new(JwtRegisteredClaimNames.FamilyName, identity.FamilyName),
            new("oid", identity.UserId)
        };

        if (!string.IsNullOrEmpty(nonce))
            claims.Add(new Claim(JwtRegisteredClaimNames.Nonce, nonce));

        foreach (var permission in identity.Permissions)
            claims.Add(new Claim("permissions", permission));

        foreach (var (type, value) in identity.CustomClaims)
            claims.Add(new Claim(type, value));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = now.AddHours(options.TokenLifetimeHours),
            Issuer = options.Issuer,
            Audience = audience ?? options.Audience,
            SigningCredentials = creds
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
