using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for agent_mail_mentions table operations.
/// Handles persistence of @mention records in agent mail messages.
/// </summary>
public class AgentMentionRepository : IAgentMentionRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AgentMentionRepository> _logger;

    private const string CollectionName = "agent_mail";
    private const string MentionsTable = "agent_mail_mentions";

    public AgentMentionRepository(VibeDbContext context, ILogger<AgentMentionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument?> GetMentionByIdAsync(int clientId, string mentionId)
    {
        var mentions = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MentionsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return mentions.FirstOrDefault(d =>
        {
            var data = TryDeserialize<MentionData>(d.Data);
            return data?.Id == mentionId;
        });
    }

    public async Task<List<VibeDocument>> GetMentionsForAgentAsync(
        int clientId,
        int agentId,
        bool unreadOnly = false,
        DateTimeOffset? since = null,
        string? threadId = null,
        int limit = 50,
        int offset = 0)
    {
        var mentions = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MentionsTable
                     && d.DeletedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var filtered = mentions
            .Select(d => new { Doc = d, Data = TryDeserialize<MentionData>(d.Data) })
            .Where(x => x.Data != null && x.Data.MentionedAgentId == agentId)
            .Where(x => !unreadOnly || x.Data!.ReadAt == null)
            .Where(x => since == null || x.Doc.CreatedAt > since)
            .Where(x => threadId == null || x.Data!.ThreadId == threadId)
            .Skip(offset)
            .Take(Math.Min(limit, 100))
            .Select(x => x.Doc)
            .ToList();

        return filtered;
    }

    public async Task<int> GetMentionCountForAgentAsync(
        int clientId,
        int agentId,
        bool unreadOnly = false,
        DateTimeOffset? since = null,
        string? threadId = null)
    {
        var mentions = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MentionsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return mentions
            .Select(d => TryDeserialize<MentionData>(d.Data))
            .Where(data => data != null && data.MentionedAgentId == agentId)
            .Where(data => !unreadOnly || data!.ReadAt == null)
            .Where(data => since == null || DateTimeOffset.TryParse(data!.CreatedAt, out var createdAt) && createdAt > since)
            .Where(data => threadId == null || data!.ThreadId == threadId)
            .Count();
    }

    public async Task<int> GetUnreadMentionCountAsync(int clientId, int agentId)
    {
        var mentions = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MentionsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return mentions
            .Select(d => TryDeserialize<MentionData>(d.Data))
            .Count(data => data != null && data.MentionedAgentId == agentId && data.ReadAt == null);
    }

    public async Task<bool> MentionExistsAsync(int clientId, int messageId, int mentionedAgentId)
    {
        var mentions = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MentionsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return mentions.Any(d =>
        {
            var data = TryDeserialize<MentionData>(d.Data);
            return data != null && data.MessageId == messageId && data.MentionedAgentId == mentionedAgentId;
        });
    }

    public async Task<VibeDocument> CreateMentionAsync(int clientId, int messageId, int mentionedAgentId)
    {
        var now = DateTimeOffset.UtcNow;
        var mentionId = Guid.NewGuid().ToString();

        var mentionData = new MentionData
        {
            Id = mentionId,
            MessageId = messageId,
            MentionedAgentId = mentionedAgentId,
            CreatedAt = now.ToString("o"),
            ReadAt = null
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = 0,
            Collection = CollectionName,
            TableName = MentionsTable,
            Data = JsonSerializer.Serialize(mentionData),
            CreatedAt = now,
            CreatedBy = 0
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("AGENT_MENTION_CREATED: MentionId={MentionId}, MessageId={MessageId}, MentionedAgentId={MentionedAgentId}",
            mentionId, messageId, mentionedAgentId);

        return document;
    }

    public async Task<int> CreateMentionsBatchAsync(int clientId, int messageId, IEnumerable<int> mentionedAgentIds)
    {
        var now = DateTimeOffset.UtcNow;
        var count = 0;

        foreach (var agentId in mentionedAgentIds.Distinct())
        {
            // Check for duplicate
            if (await MentionExistsAsync(clientId, messageId, agentId))
                continue;

            var mentionId = Guid.NewGuid().ToString();
            var mentionData = new MentionData
            {
                Id = mentionId,
                MessageId = messageId,
                MentionedAgentId = agentId,
                CreatedAt = now.ToString("o"),
                ReadAt = null
            };

            var document = new VibeDocument
            {
                ClientId = clientId,
                OwnerUserId = 0,
                Collection = CollectionName,
                TableName = MentionsTable,
                Data = JsonSerializer.Serialize(mentionData),
                CreatedAt = now,
                CreatedBy = 0
            };

            _context.Documents.Add(document);
            count++;
        }

        if (count > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("AGENT_MENTIONS_BATCH_CREATED: MessageId={MessageId}, Count={Count}", messageId, count);
        }

        return count;
    }

    public async Task<bool> MarkAsReadAsync(int clientId, string mentionId)
    {
        var mentions = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MentionsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        var mention = mentions.FirstOrDefault(d =>
        {
            var data = TryDeserialize<MentionData>(d.Data);
            return data?.Id == mentionId;
        });

        if (mention == null)
            return false;

        var data = TryDeserialize<MentionData>(mention.Data);
        if (data == null)
            return false;

        data.ReadAt = DateTimeOffset.UtcNow.ToString("o");
        mention.Data = JsonSerializer.Serialize(data);
        mention.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("AGENT_MENTION_READ: MentionId={MentionId}", mentionId);

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(int clientId, int agentId, DateTimeOffset? before = null)
    {
        var mentions = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MentionsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        var count = 0;

        foreach (var mention in mentions)
        {
            var data = TryDeserialize<MentionData>(mention.Data);
            if (data == null || data.MentionedAgentId != agentId || data.ReadAt != null)
                continue;

            if (before.HasValue && DateTimeOffset.TryParse(data.CreatedAt, out var createdAt) && createdAt > before)
                continue;

            data.ReadAt = now.ToString("o");
            mention.Data = JsonSerializer.Serialize(data);
            mention.UpdatedAt = now;
            count++;
        }

        if (count > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("AGENT_MENTIONS_BULK_READ: AgentId={AgentId}, Count={Count}", agentId, count);
        }

        return count;
    }

    public async Task<int> DeleteMentionsForMessageAsync(int clientId, int messageId)
    {
        var mentions = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MentionsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        var count = 0;

        foreach (var mention in mentions)
        {
            var data = TryDeserialize<MentionData>(mention.Data);
            if (data?.MessageId == messageId)
            {
                mention.DeletedAt = now;
                count++;
            }
        }

        if (count > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("AGENT_MENTIONS_DELETED: MessageId={MessageId}, Count={Count}", messageId, count);
        }

        return count;
    }

    public async Task<bool> DeleteMentionAsync(int clientId, string mentionId)
    {
        var mention = await GetMentionByIdAsync(clientId, mentionId);
        if (mention == null)
            return false;

        mention.DeletedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("AGENT_MENTION_DELETED: MentionId={MentionId}", mentionId);

        return true;
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

    /// <summary>
    /// Internal mention data structure for JSON storage.
    /// </summary>
    private class MentionData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("message_id")]
        public int MessageId { get; set; }

        [JsonPropertyName("mentioned_agent_id")]
        public int MentionedAgentId { get; set; }

        [JsonPropertyName("thread_id")]
        public string? ThreadId { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("read_at")]
        public string? ReadAt { get; set; }
    }
}
