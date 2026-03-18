using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace VibeSQL.Edge.Tests.Integration;

public static class TestJwtGenerator
{
    private static readonly RSA Rsa = RSA.Create(2048);

    public static RsaSecurityKey SecurityKey { get; } = new(Rsa);

    public static JsonWebKey JsonWebKey
    {
        get
        {
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(SecurityKey);
            jwk.Use = "sig";
            jwk.Alg = SecurityAlgorithms.RsaSha256;
            jwk.Kid = "test-key-1";
            return jwk;
        }
    }

    public static string GenerateToken(
        string issuer,
        string audience,
        string subject,
        string[]? roles = null,
        string? email = null,
        TimeSpan? lifetime = null,
        string? kid = null)
    {
        var credentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.RsaSha256)
        {
            Key = { KeyId = kid ?? "test-key-1" }
        };

        var claims = new List<Claim>
        {
            new("sub", subject),
            new("aud", audience)
        };

        if (email is not null)
            claims.Add(new Claim("email", email));

        if (roles is not null)
        {
            foreach (var role in roles)
                claims.Add(new Claim("roles", role));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(30)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerateExpiredToken(string issuer, string audience, string subject)
    {
        return GenerateToken(issuer, audience, subject, lifetime: TimeSpan.FromMinutes(-5));
    }
}
