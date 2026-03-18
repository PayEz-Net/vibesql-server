using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Authorization;

public record ResolvedPermission(
    VibePermissionLevel EffectiveLevel,
    HashSet<string> DeniedStatements,
    string[] MatchedRoles);

public interface IPermissionResolver
{
    Task<ResolvedPermission> ResolveAsync(
        string providerKey,
        IReadOnlyList<string> tokenRoles,
        string? vibeClientId = null,
        CancellationToken ct = default);
}

public sealed class PermissionResolver : IPermissionResolver
{
    private readonly EdgeDbContext _db;
    private readonly ILogger<PermissionResolver> _logger;

    public PermissionResolver(EdgeDbContext db, ILogger<PermissionResolver> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ResolvedPermission> ResolveAsync(
        string providerKey,
        IReadOnlyList<string> tokenRoles,
        string? vibeClientId = null,
        CancellationToken ct = default)
    {
        if (tokenRoles.Count == 0)
        {
            _logger.LogDebug("EDGE_AUTHZ: No roles in token for provider {ProviderKey}, defaulting to none", providerKey);
            return new ResolvedPermission(VibePermissionLevel.None, new HashSet<string>(StringComparer.OrdinalIgnoreCase), Array.Empty<string>());
        }

        var roleMappings = await _db.OidcProviderRoleMappings
            .AsNoTracking()
            .Where(m => m.ProviderKey == providerKey && tokenRoles.Contains(m.ExternalRole))
            .ToListAsync(ct);

        if (roleMappings.Count == 0)
        {
            _logger.LogWarning("EDGE_AUTHZ: No role mappings found for provider {ProviderKey}, roles: [{Roles}]",
                providerKey, string.Join(", ", tokenRoles));
            return new ResolvedPermission(VibePermissionLevel.None, new HashSet<string>(StringComparer.OrdinalIgnoreCase), Array.Empty<string>());
        }

        var highestLevel = VibePermissionLevel.None;
        var deniedStatements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedRoles = new List<string>();

        foreach (var mapping in roleMappings)
        {
            var level = mapping.GetPermissionLevel();
            if (level > highestLevel)
                highestLevel = level;

            matchedRoles.Add(mapping.ExternalRole);

            if (mapping.DeniedStatements is { Length: > 0 })
            {
                foreach (var denied in mapping.DeniedStatements)
                    deniedStatements.Add(denied);
            }
        }

        var hasClientMappings = await _db.OidcProviderClientMappings
            .AsNoTracking()
            .AnyAsync(c => c.ProviderKey == providerKey && c.IsActive, ct);

        if (hasClientMappings)
        {
            if (vibeClientId is null)
            {
                _logger.LogWarning(
                    "EDGE_AUTHZ: Provider {ProviderKey} has client mappings but no client ID in token (missing aud claim). Failing closed.",
                    providerKey);
                return new ResolvedPermission(VibePermissionLevel.None, deniedStatements, matchedRoles.ToArray());
            }

            var clientMapping = await _db.OidcProviderClientMappings
                .AsNoTracking()
                .Where(c => c.ProviderKey == providerKey && c.IsActive && c.VibeClientId == vibeClientId)
                .FirstOrDefaultAsync(ct);

            if (clientMapping is null)
            {
                _logger.LogWarning(
                    "EDGE_AUTHZ: No active client mapping for provider {ProviderKey}, client {ClientId}. Failing closed.",
                    providerKey, vibeClientId);
                return new ResolvedPermission(VibePermissionLevel.None, deniedStatements, matchedRoles.ToArray());
            }

            var maxPermission = clientMapping.GetMaxPermissionLevel();
            if (highestLevel > maxPermission)
            {
                _logger.LogInformation(
                    "EDGE_AUTHZ: Capping permission from {Original} to {Max} (client {ClientId} license ceiling)",
                    highestLevel, maxPermission, clientMapping.VibeClientId);
                highestLevel = maxPermission;
            }
        }

        _logger.LogDebug(
            "EDGE_AUTHZ: Resolved permission={Level}, denied=[{Denied}], matched_roles=[{Roles}]",
            highestLevel,
            string.Join(", ", deniedStatements),
            string.Join(", ", matchedRoles));

        return new ResolvedPermission(highestLevel, deniedStatements, matchedRoles.ToArray());
    }
}
