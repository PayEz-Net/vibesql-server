using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.DTOs.AgentMail;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for agent_mail_handoffs table operations.
/// Abstracts data access from business logic following Clean Architecture.
/// </summary>
public class AgentHandoffRepository : IAgentHandoffRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AgentHandoffRepository> _logger;

    private const string CollectionName = "agent_mail";
    private const string HandoffsTable = "agent_mail_handoffs";

    public AgentHandoffRepository(VibeDbContext context, ILogger<AgentHandoffRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument?> GetHandoffByIdAsync(int clientId, string handoffId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == HandoffsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents.FirstOrDefault(d =>
        {
            var data = TryDeserialize<HandoffDataModel>(d.Data);
            return data?.Id == handoffId;
        });
    }

    public async Task<(List<VibeDocument> Handoffs, int Total)> GetHandoffsAsync(
        int clientId,
        string? status = null,
        string? toSessionId = null,
        string? fromSessionId = null,
        string? taskId = null,
        int limit = 20,
        int offset = 0)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == HandoffsTable
                     && d.DeletedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        // Filter in memory based on JSON data
        var filtered = documents.Where(d =>
        {
            var data = TryDeserialize<HandoffDataModel>(d.Data);
            if (data == null) return false;

            // Status filter
            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                if (!string.Equals(data.Status, status, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // To session filter
            if (!string.IsNullOrEmpty(toSessionId))
            {
                var toIdentity = DeserializeElement<AgentIdentityDto>(data.To);
                if (toIdentity?.SessionId != toSessionId)
                    return false;
            }

            // From session filter
            if (!string.IsNullOrEmpty(fromSessionId))
            {
                var fromIdentity = DeserializeElement<AgentIdentityDto>(data.From);
                if (fromIdentity?.SessionId != fromSessionId)
                    return false;
            }

            // Task ID filter
            if (!string.IsNullOrEmpty(taskId))
            {
                if (data.TaskId != taskId)
                    return false;
            }

            return true;
        }).ToList();

        var total = filtered.Count;
        var paged = filtered.Skip(offset).Take(limit).ToList();

        return (paged, total);
    }

    public async Task<List<VibeDocument>> GetHandoffsByTaskIdAsync(int clientId, string taskId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == HandoffsTable
                     && d.DeletedAt == null)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();

        return documents.Where(d =>
        {
            var data = TryDeserialize<HandoffDataModel>(d.Data);
            return data?.TaskId == taskId;
        }).ToList();
    }

    public async Task<VibeDocument> CreateHandoffAsync(
        int clientId,
        string handoffId,
        string fromSessionId,
        string? toSessionId,
        string taskId,
        string dataJson,
        string? previousHandoffId = null)
    {
        var now = DateTimeOffset.UtcNow;

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = null,
            Collection = CollectionName,
            TableName = HandoffsTable,
            Data = dataJson,
            CreatedAt = now,
            CreatedBy = null
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "HANDOFF_CREATED: HandoffId={HandoffId}, TaskId={TaskId}, From={From}, To={To}, ClientId={ClientId}",
            handoffId, taskId, fromSessionId, toSessionId ?? "(open)", clientId);

        return document;
    }

    public async Task<bool> UpdateHandoffAsync(int clientId, string handoffId, string dataJson)
    {
        var document = await GetHandoffByIdAsync(clientId, handoffId);
        if (document == null)
            return false;

        document.Data = dataJson;
        document.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("HANDOFF_UPDATED: HandoffId={HandoffId}, ClientId={ClientId}", handoffId, clientId);

        return true;
    }

    public async Task<List<VibeDocument>> GetPendingHandoffsForRecipientAsync(int clientId, string sessionId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == HandoffsTable
                     && d.DeletedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return documents.Where(d =>
        {
            var data = TryDeserialize<HandoffDataModel>(d.Data);
            if (data == null || data.Status != HandoffStatus.Pending)
                return false;

            var toIdentity = DeserializeElement<AgentIdentityDto>(data.To);
            return toIdentity?.SessionId == sessionId;
        }).ToList();
    }

    public async Task<List<VibeDocument>> GetOpenHandoffsAsync(int clientId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == HandoffsTable
                     && d.DeletedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return documents.Where(d =>
        {
            var data = TryDeserialize<HandoffDataModel>(d.Data);
            if (data == null || data.Status != HandoffStatus.Pending)
                return false;

            // Open handoff has no specific recipient
            return !data.To.HasValue || data.To.Value.ValueKind == JsonValueKind.Null;
        }).ToList();
    }

    public async Task<int> CountPendingHandoffsFromSenderAsync(int clientId, string sessionId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == HandoffsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents.Count(d =>
        {
            var data = TryDeserialize<HandoffDataModel>(d.Data);
            if (data == null || data.Status != HandoffStatus.Pending)
                return false;

            var fromIdentity = DeserializeElement<AgentIdentityDto>(data.From);
            return fromIdentity?.SessionId == sessionId;
        });
    }

    public async Task<int> MarkExpiredHandoffsAsync(int clientId)
    {
        var now = DateTimeOffset.UtcNow;
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == HandoffsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        var expiredCount = 0;
        foreach (var doc in documents)
        {
            var data = TryDeserialize<HandoffDataModel>(doc.Data);
            if (data == null || data.Status != HandoffStatus.Pending)
                continue;

            if (string.IsNullOrEmpty(data.ExpiresAt))
                continue;

            if (DateTimeOffset.TryParse(data.ExpiresAt, out var expiresAt) && expiresAt <= now)
            {
                data.Status = HandoffStatus.Expired;
                doc.Data = JsonSerializer.Serialize(data);
                doc.UpdatedAt = now;
                expiredCount++;

                _logger.LogInformation("HANDOFF_EXPIRED: HandoffId={HandoffId}", data.Id);
            }
        }

        if (expiredCount > 0)
        {
            await _context.SaveChangesAsync();
        }

        return expiredCount;
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return null;
        }
    }

    private static T? DeserializeElement<T>(JsonElement? element) where T : class
    {
        if (!element.HasValue || element.Value.ValueKind == JsonValueKind.Null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(element.Value.GetRawText());
        }
        catch
        {
            return null;
        }
    }
}
