using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for agent_mail_reactions table operations.
/// Abstracts data access from business logic following Clean Architecture.
/// </summary>
public class MessageReactionRepository : IMessageReactionRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<MessageReactionRepository> _logger;

    private const string CollectionName = "agent_mail";
    private const string ReactionsTable = "agent_mail_reactions";

    public MessageReactionRepository(VibeDbContext context, ILogger<MessageReactionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument?> GetReactionAsync(int clientId, int messageId, int agentId, string reactionType)
    {
        var reactions = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == ReactionsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return reactions.FirstOrDefault(d =>
        {
            var data = TryDeserialize<ReactionData>(d.Data);
            return data?.MessageId == messageId
                && data.AgentId == agentId
                && string.Equals(data.ReactionType, reactionType, StringComparison.Ordinal);
        });
    }

    public async Task<List<VibeDocument>> GetReactionsByMessageAsync(int clientId, int messageId)
    {
        var reactions = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == ReactionsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return reactions.Where(d =>
        {
            var data = TryDeserialize<ReactionData>(d.Data);
            return data?.MessageId == messageId;
        })
        .OrderBy(d => d.CreatedAt)
        .ToList();
    }

    public async Task<List<VibeDocument>> GetReactionsByAgentAsync(int clientId, int messageId, int agentId)
    {
        var reactions = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == ReactionsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return reactions.Where(d =>
        {
            var data = TryDeserialize<ReactionData>(d.Data);
            return data?.MessageId == messageId && data.AgentId == agentId;
        })
        .ToList();
    }

    public async Task<VibeDocument> CreateReactionAsync(int clientId, int messageId, int agentId, string reactionType)
    {
        var now = DateTimeOffset.UtcNow;

        // Get next ID from sequence
        var nextId = await GetNextReactionIdAsync(clientId);

        var reactionData = new
        {
            id = nextId,
            message_id = messageId,
            agent_id = agentId,
            reaction_type = reactionType,
            created_at = now.ToString("o")
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = 0, // Reactions don't have a specific owner
            Collection = CollectionName,
            TableName = ReactionsTable,
            Data = JsonSerializer.Serialize(reactionData),
            CreatedAt = now,
            CreatedBy = 0
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogDebug("AGENT_MAIL_REACTION_CREATED: ReactionId={ReactionId}, MessageId={MessageId}, AgentId={AgentId}, Type={Type}",
            nextId, messageId, agentId, reactionType);

        return document;
    }

    public async Task<bool> DeleteReactionAsync(int clientId, int messageId, int agentId, string reactionType)
    {
        var reaction = await GetReactionAsync(clientId, messageId, agentId, reactionType);
        if (reaction == null)
            return false;

        // Soft delete
        reaction.DeletedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("AGENT_MAIL_REACTION_DELETED: MessageId={MessageId}, AgentId={AgentId}, Type={Type}",
            messageId, agentId, reactionType);

        return true;
    }

    public async Task<Dictionary<string, int>> GetReactionCountsAsync(int clientId, int messageId)
    {
        var reactions = await GetReactionsByMessageAsync(clientId, messageId);

        return reactions
            .Select(d => TryDeserialize<ReactionData>(d.Data))
            .Where(r => r != null)
            .GroupBy(r => r!.ReactionType)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<int> GetNextReactionIdAsync(int clientId)
    {
        var seqName = $"vibe.seq_{clientId}_{CollectionName}_reactions";

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
            var allReactions = await _context.Documents
                .Where(d => d.ClientId == clientId
                         && d.Collection == CollectionName
                         && d.TableName == ReactionsTable
                         && d.DeletedAt == null)
                .ToListAsync();

            var maxId = allReactions
                .Select(d => TryDeserialize<ReactionData>(d.Data)?.Id ?? 0)
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

    private class ReactionData
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message_id")]
        public int MessageId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("agent_id")]
        public int AgentId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("reaction_type")]
        public string ReactionType { get; set; } = "";
    }
}
