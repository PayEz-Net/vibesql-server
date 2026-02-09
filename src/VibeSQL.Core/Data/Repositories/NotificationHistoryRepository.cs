using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for notification history.
/// Stores notification records in Vibe documents for multi-tenant isolation.
/// </summary>
public class NotificationHistoryRepository : INotificationHistoryRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<NotificationHistoryRepository> _logger;

    private const string CollectionName = "agent_mail";
    private const string TableName = "notification_history";

    public NotificationHistoryRepository(VibeDbContext context, ILogger<NotificationHistoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument?> GetByIdAsync(int clientId, string notificationId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents.FirstOrDefault(d =>
        {
            var data = TryDeserialize<NotificationHistoryData>(d.Data);
            return data?.Id == notificationId;
        });
    }

    public async Task<(List<VibeDocument> Notifications, int Total)> GetHistoryAsync(
        int clientId,
        int userId,
        int? agentId = null,
        string? status = null,
        string? eventType = null,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        int limit = 50,
        int offset = 0)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.OwnerUserId == userId
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        var filtered = documents.AsEnumerable();

        // Filter by agent
        if (agentId.HasValue)
        {
            filtered = filtered.Where(d =>
            {
                var data = TryDeserialize<NotificationHistoryData>(d.Data);
                return data?.AgentId == agentId.Value;
            });
        }

        // Filter by status
        if (!string.IsNullOrEmpty(status))
        {
            filtered = filtered.Where(d =>
            {
                var data = TryDeserialize<NotificationHistoryData>(d.Data);
                return data != null && string.Equals(data.Status, status, StringComparison.OrdinalIgnoreCase);
            });
        }

        // Filter by event type
        if (!string.IsNullOrEmpty(eventType))
        {
            filtered = filtered.Where(d =>
            {
                var data = TryDeserialize<NotificationHistoryData>(d.Data);
                return data != null && string.Equals(data.EventType, eventType, StringComparison.OrdinalIgnoreCase);
            });
        }

        // Filter by date range
        if (since.HasValue)
        {
            filtered = filtered.Where(d =>
            {
                var data = TryDeserialize<NotificationHistoryData>(d.Data);
                if (data?.CreatedAt == null) return false;
                return DateTimeOffset.TryParse(data.CreatedAt, out var createdAt) && createdAt >= since.Value;
            });
        }

        if (until.HasValue)
        {
            filtered = filtered.Where(d =>
            {
                var data = TryDeserialize<NotificationHistoryData>(d.Data);
                if (data?.CreatedAt == null) return false;
                return DateTimeOffset.TryParse(data.CreatedAt, out var createdAt) && createdAt <= until.Value;
            });
        }

        var filteredList = filtered.ToList();
        var total = filteredList.Count;

        var result = filteredList
            .OrderByDescending(d =>
            {
                var data = TryDeserialize<NotificationHistoryData>(d.Data);
                return data?.CreatedAt ?? "";
            })
            .Skip(offset)
            .Take(limit)
            .ToList();

        return (result, total);
    }

    public async Task<VibeDocument> CreateAsync(
        int clientId,
        int userId,
        int agentId,
        string? deviceTokenId,
        string eventType,
        string status,
        string payloadJson,
        int? messageId = null,
        string? threadId = null,
        string? fromAgent = null,
        string? platform = null,
        string? deviceId = null)
    {
        var now = DateTimeOffset.UtcNow;
        var nowStr = now.ToString("o");
        var id = Guid.NewGuid().ToString("N");

        var historyData = new NotificationHistoryData
        {
            Id = id,
            AgentId = agentId,
            UserId = userId,
            DeviceTokenId = deviceTokenId,
            EventType = eventType,
            Status = status,
            PayloadJson = payloadJson,
            MessageId = messageId,
            ThreadId = threadId,
            FromAgent = fromAgent,
            Platform = platform,
            DeviceId = deviceId,
            Attempts = 0,
            CreatedAt = nowStr
        };

        // Set timestamp based on status
        if (status == "sent")
        {
            historyData.SentAt = nowStr;
        }
        else if (status == "delivered")
        {
            historyData.SentAt = nowStr;
            historyData.DeliveredAt = nowStr;
        }

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = userId,
            Collection = CollectionName,
            TableName = TableName,
            Data = JsonSerializer.Serialize(historyData),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogDebug(
            "NOTIFICATION_HISTORY_CREATED: ClientId={ClientId}, NotificationId={NotificationId}, EventType={EventType}",
            clientId, id, eventType);

        return document;
    }

    public async Task<bool> UpdateStatusAsync(
        int clientId,
        string notificationId,
        string status,
        string? failureReason = null)
    {
        var doc = await GetByIdAsync(clientId, notificationId);
        if (doc == null) return false;

        var data = TryDeserialize<NotificationHistoryData>(doc.Data);
        if (data == null) return false;

        var now = DateTimeOffset.UtcNow;
        var nowStr = now.ToString("o");

        data.Status = status;
        data.Attempts++;

        switch (status.ToLowerInvariant())
        {
            case "sent":
                data.SentAt = nowStr;
                break;
            case "delivered":
                data.DeliveredAt = nowStr;
                break;
            case "failed":
                data.FailedAt = nowStr;
                data.FailureReason = failureReason;
                break;
        }

        doc.Data = JsonSerializer.Serialize(data);
        doc.UpdatedAt = now;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<int> CountSinceAsync(int clientId, int userId, DateTimeOffset since)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.OwnerUserId == userId
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null
                     && d.CreatedAt >= since)
            .CountAsync();

        return documents;
    }

    public async Task<VibeDocument?> GetLatestAsync(int clientId, int userId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.OwnerUserId == userId
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();

        return documents;
    }

    public async Task<int> DeleteOlderThanAsync(int clientId, DateTimeOffset cutoff)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null
                     && d.CreatedAt < cutoff)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        foreach (var doc in documents)
        {
            doc.DeletedAt = now;
        }

        await _context.SaveChangesAsync();

        if (documents.Any())
        {
            _logger.LogInformation(
                "NOTIFICATION_HISTORY_CLEANUP: ClientId={ClientId}, DeletedCount={Count}, Cutoff={Cutoff}",
                clientId, documents.Count, cutoff);
        }

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

    private class NotificationHistoryData
    {
        public string Id { get; set; } = "";
        public int AgentId { get; set; }
        public int UserId { get; set; }
        public string? DeviceTokenId { get; set; }
        public string EventType { get; set; } = "";
        public string Status { get; set; } = "pending";
        public string? PayloadJson { get; set; }
        public int? MessageId { get; set; }
        public string? ThreadId { get; set; }
        public string? FromAgent { get; set; }
        public string? Platform { get; set; }
        public string? DeviceId { get; set; }
        public string? SentAt { get; set; }
        public string? DeliveredAt { get; set; }
        public string? FailedAt { get; set; }
        public string? FailureReason { get; set; }
        public int Attempts { get; set; }
        public string? CreatedAt { get; set; }
    }

    #endregion
}
