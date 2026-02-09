using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for push notification device tokens.
/// Stores device tokens in Vibe documents for multi-tenant isolation.
/// </summary>
public class DeviceTokenRepository : IDeviceTokenRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<DeviceTokenRepository> _logger;

    private const string CollectionName = "agent_mail";
    private const string TableName = "device_tokens";

    public DeviceTokenRepository(VibeDbContext context, ILogger<DeviceTokenRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument?> GetByIdAsync(int clientId, string tokenId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents.FirstOrDefault(d =>
        {
            var data = TryDeserialize<DeviceTokenData>(d.Data);
            return data?.Id == tokenId;
        });
    }

    public async Task<VibeDocument?> GetByTokenHashAsync(int clientId, int userId, string tokenHash)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.OwnerUserId == userId
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents.FirstOrDefault(d =>
        {
            var data = TryDeserialize<DeviceTokenData>(d.Data);
            return data?.DeviceTokenHash == tokenHash;
        });
    }

    public async Task<VibeDocument?> GetByDeviceIdAsync(int clientId, int userId, string deviceId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.OwnerUserId == userId
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents.FirstOrDefault(d =>
        {
            var data = TryDeserialize<DeviceTokenData>(d.Data);
            return data?.DeviceId == deviceId;
        });
    }

    public async Task<List<VibeDocument>> GetByUserAsync(int clientId, int userId, bool activeOnly = true)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.OwnerUserId == userId
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        if (!activeOnly) return documents;

        return documents.Where(d =>
        {
            var data = TryDeserialize<DeviceTokenData>(d.Data);
            return data?.IsActive == true;
        }).ToList();
    }

    public async Task<VibeDocument> CreateAsync(
        int clientId,
        int userId,
        string deviceToken,
        string tokenHash,
        string platform,
        string? deviceId = null,
        string? deviceName = null,
        string? appBundleId = null,
        bool isSandbox = false)
    {
        var now = DateTimeOffset.UtcNow;
        var nowStr = now.ToString("o");
        var id = Guid.NewGuid().ToString("N");

        var tokenData = new DeviceTokenData
        {
            Id = id,
            UserId = userId,
            DeviceToken = deviceToken,
            DeviceTokenHash = tokenHash,
            DeviceId = deviceId,
            DeviceName = deviceName,
            Platform = platform,
            AppBundleId = appBundleId,
            IsSandbox = isSandbox,
            IsActive = true,
            FailureCount = 0,
            CreatedAt = nowStr,
            UpdatedAt = nowStr
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = userId,
            Collection = CollectionName,
            TableName = TableName,
            Data = JsonSerializer.Serialize(tokenData),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "DEVICE_TOKEN_CREATED: ClientId={ClientId}, UserId={UserId}, TokenId={TokenId}, Platform={Platform}",
            clientId, userId, id, platform);

        return document;
    }

    public async Task<bool> UpdateAsync(
        int clientId,
        string tokenId,
        string? newDeviceToken = null,
        string? newTokenHash = null,
        bool? isActive = null,
        string? lastFailureReason = null)
    {
        var doc = await GetByIdAsync(clientId, tokenId);
        if (doc == null) return false;

        var data = TryDeserialize<DeviceTokenData>(doc.Data);
        if (data == null) return false;

        var now = DateTimeOffset.UtcNow;
        data.UpdatedAt = now.ToString("o");

        if (!string.IsNullOrEmpty(newDeviceToken))
        {
            data.DeviceToken = newDeviceToken;
        }

        if (!string.IsNullOrEmpty(newTokenHash))
        {
            data.DeviceTokenHash = newTokenHash;
        }

        if (isActive.HasValue)
        {
            data.IsActive = isActive.Value;
        }

        if (!string.IsNullOrEmpty(lastFailureReason))
        {
            data.LastFailureReason = lastFailureReason;
            data.LastFailureAt = now.ToString("o");
            data.FailureCount++;
        }

        doc.Data = JsonSerializer.Serialize(data);
        doc.UpdatedAt = now;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> UpdateLastUsedAsync(int clientId, string tokenId)
    {
        var doc = await GetByIdAsync(clientId, tokenId);
        if (doc == null) return false;

        var data = TryDeserialize<DeviceTokenData>(doc.Data);
        if (data == null) return false;

        var now = DateTimeOffset.UtcNow;
        data.LastUsedAt = now.ToString("o");
        data.UpdatedAt = now.ToString("o");

        doc.Data = JsonSerializer.Serialize(data);
        doc.UpdatedAt = now;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RecordFailureAsync(int clientId, string tokenId, string failureReason)
    {
        return await UpdateAsync(clientId, tokenId, lastFailureReason: failureReason);
    }

    public async Task<bool> DeleteAsync(int clientId, string tokenId)
    {
        var doc = await GetByIdAsync(clientId, tokenId);
        if (doc == null) return false;

        doc.DeletedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "DEVICE_TOKEN_DELETED: ClientId={ClientId}, TokenId={TokenId}",
            clientId, tokenId);

        return true;
    }

    public async Task<int> DeleteByUserAsync(int clientId, int userId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.OwnerUserId == userId
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        foreach (var doc in documents)
        {
            doc.DeletedAt = now;
        }

        await _context.SaveChangesAsync();
        return documents.Count;
    }

    public async Task<bool> DeleteByDeviceIdAsync(int clientId, int userId, string deviceId)
    {
        var doc = await GetByDeviceIdAsync(clientId, userId, deviceId);
        if (doc == null) return false;

        doc.DeletedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<int> CountActiveTokensAsync(int clientId, int userId)
    {
        var documents = await GetByUserAsync(clientId, userId, activeOnly: true);
        return documents.Count;
    }

    #region Helpers

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Internal Data Model

    private class DeviceTokenData
    {
        public string Id { get; set; } = "";
        public int UserId { get; set; }
        public string DeviceToken { get; set; } = "";
        public string DeviceTokenHash { get; set; } = "";
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string Platform { get; set; } = "";
        public string? AppBundleId { get; set; }
        public bool IsSandbox { get; set; }
        public bool IsActive { get; set; } = true;
        public string? LastUsedAt { get; set; }
        public int FailureCount { get; set; }
        public string? LastFailureAt { get; set; }
        public string? LastFailureReason { get; set; }
        public string? CreatedAt { get; set; }
        public string? UpdatedAt { get; set; }
    }

    #endregion
}
