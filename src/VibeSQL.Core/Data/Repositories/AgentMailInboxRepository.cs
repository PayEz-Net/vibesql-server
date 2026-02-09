using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for agent_mail_inbox table operations.
/// Abstracts data access from business logic following Clean Architecture.
/// </summary>
public class AgentMailInboxRepository : IAgentMailInboxRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AgentMailInboxRepository> _logger;

    private const string CollectionName = "agent_mail";
    private const string InboxTable = "agent_mail_inbox";

    public AgentMailInboxRepository(VibeDbContext context, ILogger<AgentMailInboxRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<VibeDocument>> GetInboxEntriesAsync(int clientId, int agentId, bool unreadOnly = false)
    {
        var entries = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == InboxTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return entries.Where(d =>
        {
            var data = TryDeserialize<InboxData>(d.Data);
            if (data?.AgentId != agentId) return false;
            if (unreadOnly && !string.IsNullOrEmpty(data.ReadAt)) return false;
            return true;
        })
        .OrderByDescending(d => d.CreatedAt)
        .ToList();
    }

    public async Task<VibeDocument?> GetInboxEntryAsync(int clientId, int inboxId)
    {
        var entries = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == InboxTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return entries.FirstOrDefault(d =>
        {
            var data = TryDeserialize<InboxData>(d.Data);
            return data?.Id == inboxId;
        });
    }

    public async Task<VibeDocument> CreateInboxEntryAsync(int clientId, int messageId, int agentId, string recipientType)
    {
        var now = DateTimeOffset.UtcNow;

        // Get next ID from sequence
        var nextId = await GetNextInboxIdAsync(clientId);

        var inboxData = new
        {
            id = nextId,
            message_id = messageId,
            agent_id = agentId,
            recipient_type = recipientType
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = 0, // Inbox entries don't have a specific owner
            Collection = CollectionName,
            TableName = InboxTable,
            Data = JsonSerializer.Serialize(inboxData),
            CreatedAt = now,
            CreatedBy = 0
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogDebug("AGENT_MAIL_INBOX_CREATED: InboxId={InboxId}, MessageId={MessageId}, AgentId={AgentId}",
            nextId, messageId, agentId);

        return document;
    }

    public async Task<bool> MarkAsReadAsync(int clientId, int inboxId, int? userId = null)
    {
        var entry = await GetInboxEntryAsync(clientId, inboxId);
        if (entry == null) return false;

        var inboxData = TryDeserialize<Dictionary<string, object>>(entry.Data) ?? new Dictionary<string, object>();
        inboxData["read_at"] = DateTimeOffset.UtcNow.ToString("o");

        entry.Data = JsonSerializer.Serialize(inboxData);
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.UpdatedBy = userId;

        await _context.SaveChangesAsync();

        _logger.LogInformation("AGENT_MAIL_MARKED_READ: InboxId={InboxId}, ClientId={ClientId}", inboxId, clientId);

        return true;
    }

    public async Task<VibeDocument?> GetInboxEntryByMessageIdAsync(int clientId, int agentId, int messageId)
    {
        var entries = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == InboxTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return entries.FirstOrDefault(d =>
        {
            var data = TryDeserialize<InboxData>(d.Data);
            return data?.AgentId == agentId && data?.MessageId == messageId;
        });
    }

    public async Task<bool> MarkMessageAsReadAsync(int clientId, int agentId, int messageId, int? userId = null)
    {
        var entry = await GetInboxEntryByMessageIdAsync(clientId, agentId, messageId);
        if (entry == null)
        {
            _logger.LogWarning("AGENT_MAIL_MARK_READ_FAILED: MessageId={MessageId}, AgentId={AgentId} - Inbox entry not found", 
                messageId, agentId);
            return false;
        }

        var inboxData = TryDeserialize<Dictionary<string, object>>(entry.Data) ?? new Dictionary<string, object>();
        inboxData["read_at"] = DateTimeOffset.UtcNow.ToString("o");

        entry.Data = JsonSerializer.Serialize(inboxData);
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.UpdatedBy = userId;

        await _context.SaveChangesAsync();

        var inboxId = TryDeserialize<InboxData>(entry.Data)?.Id ?? 0;
        _logger.LogInformation("AGENT_MAIL_MARKED_READ: InboxId={InboxId}, MessageId={MessageId}, AgentId={AgentId}, ClientId={ClientId}", 
            inboxId, messageId, agentId, clientId);

        return true;
    }

    public async Task<int> GetUnreadCountAsync(int clientId, int agentId)
    {
        var entries = await GetInboxEntriesAsync(clientId, agentId, unreadOnly: true);
        return entries.Count;
    }

    private async Task<int> GetNextInboxIdAsync(int clientId)
    {
        var seqName = $"vibe.seq_{clientId}_{CollectionName}_inbox";

        try
        {
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            try
            {
                // Ensure sequence exists
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = $"CREATE SEQUENCE IF NOT EXISTS {seqName} START WITH 1 INCREMENT BY 1";
                await createCmd.ExecuteNonQueryAsync();

                // Get next value
                using var nextCmd = connection.CreateCommand();
                nextCmd.CommandText = $"SELECT nextval('{seqName}')";
                var result = await nextCmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get sequence value, falling back to max+1");
            var allEntries = await _context.Documents
                .Where(d => d.ClientId == clientId
                         && d.Collection == CollectionName
                         && d.TableName == InboxTable
                         && d.DeletedAt == null)
                .ToListAsync();

            var maxId = allEntries
                .Select(d => TryDeserialize<InboxData>(d.Data)?.Id ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            return maxId + 1;
        }
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private class InboxData
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("agent_id")]
        public int AgentId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message_id")]
        public int MessageId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("read_at")]
        public string? ReadAt { get; set; }
    }
}
