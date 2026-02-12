using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace VibeSQL.Edge.Authentication;

public interface IProviderRegistry
{
    ProviderRecord? GetByIssuer(string issuer);
    ProviderRecord? GetByKey(string providerKey);
    IReadOnlyList<ProviderRecord> GetAll();
    void Replace(IReadOnlyList<ProviderRecord> providers);
}

public sealed class ProviderRecord
{
    public required string ProviderKey { get; init; }
    public required string Issuer { get; init; }
    public required string SchemeId { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsBootstrap { get; init; }
    public DateTimeOffset? DisabledAt { get; init; }
    public int DisableGraceMinutes { get; init; }
    public string SubjectClaimPath { get; init; } = "sub";
    public string RoleClaimPath { get; init; } = "roles";
    public string EmailClaimPath { get; init; } = "email";
    public bool AutoProvision { get; init; }
    public string? ProvisionDefaultRole { get; init; }
}

public sealed class ProviderRegistry : IProviderRegistry
{
    private volatile ProviderSnapshot _snapshot = new(
        Array.Empty<ProviderRecord>(),
        new Dictionary<string, ProviderRecord>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, ProviderRecord>(StringComparer.OrdinalIgnoreCase));

    public ProviderRecord? GetByIssuer(string issuer)
    {
        _snapshot.IssuerIndex.TryGetValue(issuer, out var record);
        return record;
    }

    public ProviderRecord? GetByKey(string providerKey)
    {
        _snapshot.KeyIndex.TryGetValue(providerKey, out var record);
        return record;
    }

    public IReadOnlyList<ProviderRecord> GetAll() => _snapshot.Providers;

    public void Replace(IReadOnlyList<ProviderRecord> providers)
    {
        var issuerIndex = new Dictionary<string, ProviderRecord>(providers.Count, StringComparer.OrdinalIgnoreCase);
        var keyIndex = new Dictionary<string, ProviderRecord>(providers.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var p in providers)
        {
            issuerIndex[p.Issuer] = p;
            keyIndex[p.ProviderKey] = p;
        }

        Interlocked.Exchange(ref _snapshot, new ProviderSnapshot(providers, issuerIndex, keyIndex));
    }

    private sealed record ProviderSnapshot(
        IReadOnlyList<ProviderRecord> Providers,
        Dictionary<string, ProviderRecord> IssuerIndex,
        Dictionary<string, ProviderRecord> KeyIndex);
}

public static class MultiProviderSelector
{
    public const string PolicyScheme = "MultiProvider";
    public const string RejectScheme = "Reject";

    private const int MaxTokenBytes = 16 * 1024;

    public static string? SelectScheme(HttpContext context, IProviderRegistry registry, ILogger logger)
    {
        var token = ExtractBearerToken(context);
        if (token is null)
            return null;

        if (token.Length > MaxTokenBytes)
        {
            logger.LogWarning("EDGE_AUTH: Oversized bearer token ({Length} bytes), rejecting", token.Length);
            return null;
        }

        var issuer = ReadIssuerFromUnvalidatedJwt(token, logger);
        if (issuer is null)
        {
            logger.LogWarning("EDGE_AUTH: Could not read issuer from bearer token");
            return null;
        }

        var provider = registry.GetByIssuer(issuer);
        if (provider is null)
        {
            logger.LogDebug("EDGE_AUTH: No provider registered for issuer {Issuer}", issuer);
            return null;
        }

        if (!provider.IsActive)
        {
            logger.LogWarning("EDGE_AUTH: Provider {ProviderKey} is disabled, rejecting token from issuer {Issuer}",
                provider.ProviderKey, issuer);
            return null;
        }

        context.Items[EdgeContextKeys.ProviderKey] = provider.ProviderKey;
        return provider.SchemeId;
    }

    internal static string? ExtractBearerToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.FirstOrDefault();
        if (header is null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        return header["Bearer ".Length..].Trim();
    }

    internal static string? ReadIssuerFromUnvalidatedJwt(string token, ILogger? logger = null)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
                return null;

            var jwt = handler.ReadJwtToken(token);
            return jwt.Issuer;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "EDGE_AUTH: Failed to read issuer from unvalidated JWT");
            return null;
        }
    }
}
