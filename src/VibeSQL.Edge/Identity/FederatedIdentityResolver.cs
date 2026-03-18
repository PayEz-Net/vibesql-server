using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Identity;

public record ResolvedIdentity(
    int VibeUserId,
    string ProviderKey,
    string ExternalSubject,
    bool WasProvisioned);

public interface IFederatedIdentityResolver
{
    Task<ResolvedIdentity?> ResolveAsync(
        string providerKey,
        ExtractedClaims claims,
        OidcProvider provider,
        CancellationToken ct = default);
}

public sealed class FederatedIdentityResolver : IFederatedIdentityResolver
{
    private readonly EdgeDbContext _db;
    private readonly IUserProvisioningService _provisioner;
    private readonly ILogger<FederatedIdentityResolver> _logger;

    public FederatedIdentityResolver(
        EdgeDbContext db,
        IUserProvisioningService provisioner,
        ILogger<FederatedIdentityResolver> logger)
    {
        _db = db;
        _provisioner = provisioner;
        _logger = logger;
    }

    public async Task<ResolvedIdentity?> ResolveAsync(
        string providerKey,
        ExtractedClaims claims,
        OidcProvider provider,
        CancellationToken ct = default)
    {
        var existing = await _db.FederatedIdentities
            .FirstOrDefaultAsync(f =>
                f.ProviderKey == providerKey &&
                f.ExternalSubject == claims.Subject, ct);

        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                _logger.LogWarning("EDGE_IDENTITY: Federated identity {Id} for {Subject}@{Provider} is deactivated",
                    existing.Id, claims.Subject, providerKey);
                return null;
            }

            var needsSave = false;
            if (existing.LastSeenAt < DateTimeOffset.UtcNow.AddMinutes(-5))
            {
                existing.LastSeenAt = DateTimeOffset.UtcNow;
                needsSave = true;
            }
            if (claims.Email is not null && existing.Email != claims.Email)
            {
                existing.Email = claims.Email;
                needsSave = true;
            }

            if (needsSave)
                await _db.SaveChangesAsync(ct);

            _logger.LogDebug("EDGE_IDENTITY: Resolved {Subject}@{Provider} -> vibe_user_id {UserId}",
                claims.Subject, providerKey, existing.VibeUserId);

            return new ResolvedIdentity(existing.VibeUserId, providerKey, claims.Subject, false);
        }

        if (!provider.AutoProvision)
        {
            _logger.LogWarning("EDGE_IDENTITY: Unknown identity {Subject}@{Provider} and auto_provision=false",
                claims.Subject, providerKey);
            return null;
        }

        var provisioned = await _provisioner.ProvisionAsync(providerKey, claims, ct);

        _logger.LogInformation("EDGE_IDENTITY: Auto-provisioned {Subject}@{Provider} -> vibe_user_id {UserId}",
            claims.Subject, providerKey, provisioned.VibeUserId);

        return new ResolvedIdentity(provisioned.VibeUserId, providerKey, claims.Subject, true);
    }
}
