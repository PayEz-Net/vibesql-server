using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Identity;

public record ProvisionedUser(int VibeUserId, int FederatedIdentityId);

public interface IUserProvisioningService
{
    Task<ProvisionedUser> ProvisionAsync(string providerKey, ExtractedClaims claims, CancellationToken ct = default);
}

public sealed class UserProvisioningService : IUserProvisioningService
{
    private readonly EdgeDbContext _db;
    private readonly ILogger<UserProvisioningService> _logger;
    private static int _nextUserId = 9999;
    private static volatile bool _initialized;
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    public UserProvisioningService(EdgeDbContext db, ILogger<UserProvisioningService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ProvisionedUser> ProvisionAsync(string providerKey, ExtractedClaims claims, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var vibeUserId = Interlocked.Increment(ref _nextUserId);

        var identity = new FederatedIdentity
        {
            ProviderKey = providerKey,
            ExternalSubject = claims.Subject,
            VibeUserId = vibeUserId,
            Email = claims.Email,
            DisplayName = claims.Email,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        _db.FederatedIdentities.Add(identity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("EDGE_PROVISION: Created federated identity {Id} for {Subject}@{Provider} -> vibe_user_id {UserId}",
            identity.Id, claims.Subject, providerKey, vibeUserId);

        return new ProvisionedUser(vibeUserId, identity.Id);
    }

    internal static void ResetForTesting()
    {
        _initialized = false;
        Interlocked.Exchange(ref _nextUserId, 9999);
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized)
                return;

            var maxId = await _db.FederatedIdentities
                .AsNoTracking()
                .MaxAsync(f => (int?)f.VibeUserId, ct) ?? 9999;

            Interlocked.Exchange(ref _nextUserId, Math.Max(maxId, 9999));
            _initialized = true;

            _logger.LogInformation("EDGE_PROVISION: Initialized vibe_user_id counter at {NextId}", _nextUserId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EDGE_PROVISION: Failed to query max vibe_user_id, using fallback 10000");
        }
        finally
        {
            _initLock.Release();
        }
    }
}
