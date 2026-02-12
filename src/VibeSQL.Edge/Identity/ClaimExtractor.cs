using System.Security.Claims;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Identity;

public record ExtractedClaims(
    string Subject,
    IReadOnlyList<string> Roles,
    string? Email);

public interface IClaimExtractor
{
    ExtractedClaims Extract(ClaimsPrincipal principal, OidcProvider provider);
}

public sealed class ClaimExtractor : IClaimExtractor
{
    public ExtractedClaims Extract(ClaimsPrincipal principal, OidcProvider provider)
    {
        var subject = FindClaim(principal, provider.SubjectClaimPath)
            ?? throw new InvalidOperationException(
                $"Subject claim '{provider.SubjectClaimPath}' not found in token from provider '{provider.ProviderKey}'");

        var roles = FindAllClaims(principal, provider.RoleClaimPath);
        var email = FindClaim(principal, provider.EmailClaimPath);

        return new ExtractedClaims(subject, roles, email);
    }

    internal static string? FindClaim(ClaimsPrincipal principal, string claimPath)
    {
        var claim = principal.FindFirst(claimPath);
        if (claim is not null)
            return claim.Value;

        foreach (var identity in principal.Identities)
        {
            claim = identity.Claims.FirstOrDefault(c =>
                c.Type.Equals(claimPath, StringComparison.OrdinalIgnoreCase));
            if (claim is not null)
                return claim.Value;
        }

        var knownMappings = GetKnownMappings(claimPath);
        foreach (var alt in knownMappings)
        {
            claim = principal.FindFirst(alt);
            if (claim is not null)
                return claim.Value;
        }

        return null;
    }

    internal static IReadOnlyList<string> FindAllClaims(ClaimsPrincipal principal, string claimPath)
    {
        var values = new List<string>();

        foreach (var identity in principal.Identities)
        {
            foreach (var claim in identity.Claims)
            {
                if (claim.Type.Equals(claimPath, StringComparison.OrdinalIgnoreCase))
                    values.Add(claim.Value);
            }
        }

        if (values.Count == 0)
        {
            var knownMappings = GetKnownMappings(claimPath);
            foreach (var alt in knownMappings)
            {
                foreach (var identity in principal.Identities)
                {
                    foreach (var claim in identity.Claims)
                    {
                        if (claim.Type.Equals(alt, StringComparison.OrdinalIgnoreCase))
                            values.Add(claim.Value);
                    }
                }
                if (values.Count > 0)
                    break;
            }
        }

        return values;
    }

    private static string[] GetKnownMappings(string claimPath) => claimPath.ToLowerInvariant() switch
    {
        "sub" => new[]
        {
            ClaimTypes.NameIdentifier,
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
            "oid"
        },
        "roles" => new[]
        {
            ClaimTypes.Role,
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
            "groups",
            "cognito:groups"
        },
        "email" => new[]
        {
            ClaimTypes.Email,
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
            "preferred_username",
            "upn"
        },
        _ => Array.Empty<string>()
    };
}
