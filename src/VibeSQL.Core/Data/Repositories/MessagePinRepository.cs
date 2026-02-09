using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for agent_mail_pins table operations.
/// Abstracts data access from business logic following Clean Architecture.
/// </summary>
public class MessagePinRepository : IMessagePinRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<MessagePinRepository> _logger;

    private const string CollectionName = "agent_mail";
    private const string PinsTable = "agent_mail_pins";

    public MessagePinRepository(VibeDbContext context, ILogger<MessagePinRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument?> GetPinByIdAsync(int clientId, string pinId)
    {
        var pins = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == PinsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return pins.FirstOrDefault(d =>
        {
            var data = TryDeserialize<PinData>(d.Data);
            return data?.Id == pinId;
        });
    }

    public async Task<List<VibeDocument>> GetPinsForAgentAsync(
        int clientId, 
        int agentId, 
        string? pinType = null, 
        string? channelId = null, 
        int limit = 50, 
        int offset = 0)
    {
        var pins = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == PinsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        var filtered = pins.Where(d =>
        {
            var data = TryDeserialize<PinData>(d.Data);
            if (data == null) return false;
            if (data.PinnedByAgentId != agentId) return false;
            
            // Filter by pin type
            if (!string.IsNullOrEmpty(pinType) && pinType != "all")
            {
                if (!data.PinType.Equals(pinType, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            
            // Filter by channel (only for shared pins)
            if (!string.IsNullOrEmpty(channelId))
            {
                if (data.ChannelId != channelId)
                    return false;
            }
            
            return true;
        })
        .OrderByDescending(d => d.CreatedAt)
        .Skip(offset)
        .Take(limit)
        .ToList();

        return filtered;
    }

    public async Task<int> GetPinCountForAgentAsync(
        int clientId, 
        int agentId, 
        string? pinType = null, 
        string? channelId = null)
    {
        var pins = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == PinsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return pins.Count(d =>
        {
            var data = TryDeserialize<PinData>(d.Data);
            if (data == null) return false;
            if (data.PinnedByAgentId != agentId) return false;
            
            if (!string.IsNullOrEmpty(pinType) && pinType != "all")
            {
                if (!data.PinType.Equals(pinType, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            
            if (!string.IsNullOrEmpty(channelId))
            {
                if (data.ChannelId != channelId)
                    return false;
            }
            
            return true;
        });
    }

    public async Task<VibeDocument?> FindExistingPinAsync(
        int clientId, 
        int messageId, 
        int agentId, 
        string pinType, 
        string? channelId)
    {
        var pins = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == PinsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return pins.FirstOrDefault(d =>
        {
            var data = TryDeserialize<PinData>(d.Data);
            if (data == null) return false;
            
            return data.MessageId == messageId 
                && data.PinnedByAgentId == agentId
                && data.PinType.Equals(pinType, StringComparison.OrdinalIgnoreCase)
                && data.ChannelId == channelId;
        });
    }

    public async Task<VibeDocument> CreatePinAsync(
        int clientId,
        int messageId,
        int agentId,
        string pinType,
        string? channelId,
        string? note,
        int? position)
    {
        var now = DateTimeOffset.UtcNow;
        var pinId = Guid.NewGuid().ToString();

        var pinData = new
        {
            id = pinId,
            message_id = messageId,
            pinned_by_agent_id = agentId,
            pin_type = pinType,
            channel_id = channelId,
            note = note,
            position = position,
            created_at = now.ToString("o")
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = 0, // System-level, ownership tracked via pinned_by_agent_id
            Collection = CollectionName,
            TableName = PinsTable,
            Data = JsonSerializer.Serialize(pinData),
            CreatedAt = now,
            CreatedBy = 0
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "MESSAGE_PIN_CREATED: PinId={PinId}, MessageId={MessageId}, AgentId={AgentId}, Type={Type}, ClientId={ClientId}",
            pinId, messageId, agentId, pinType, clientId);

        return document;
    }

    public async Task<bool> UpdatePinAsync(int clientId, string pinId, string? note, int? position)
    {
        var pinDoc = await GetPinByIdAsync(clientId, pinId);
        if (pinDoc == null) return false;

        var data = TryDeserialize<PinData>(pinDoc.Data);
        if (data == null) return false;

        // Update fields
        var updatedData = new
        {
            id = data.Id,
            message_id = data.MessageId,
            pinned_by_agent_id = data.PinnedByAgentId,
            pin_type = data.PinType,
            channel_id = data.ChannelId,
            note = note ?? data.Note,
            position = position ?? data.Position,
            created_at = data.CreatedAt
        };

        pinDoc.Data = JsonSerializer.Serialize(updatedData);
        pinDoc.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("MESSAGE_PIN_UPDATED: PinId={PinId}, ClientId={ClientId}", pinId, clientId);

        return true;
    }

    public async Task<bool> DeletePinAsync(int clientId, int messageId, int agentId, string pinType, string? channelId)
    {
        var pinDoc = await FindExistingPinAsync(clientId, messageId, agentId, pinType, channelId);
        if (pinDoc == null) return false;

        pinDoc.DeletedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "MESSAGE_PIN_DELETED: MessageId={MessageId}, AgentId={AgentId}, Type={Type}, ClientId={ClientId}",
            messageId, agentId, pinType, clientId);

        return true;
    }

    public async Task<bool> DeletePinByIdAsync(int clientId, string pinId)
    {
        var pinDoc = await GetPinByIdAsync(clientId, pinId);
        if (pinDoc == null) return false;

        pinDoc.DeletedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("MESSAGE_PIN_DELETED: PinId={PinId}, ClientId={ClientId}", pinId, clientId);

        return true;
    }

    public async Task<int> CountPersonalPinsAsync(int clientId, int agentId)
    {
        var pins = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == PinsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return pins.Count(d =>
        {
            var data = TryDeserialize<PinData>(d.Data);
            return data?.PinnedByAgentId == agentId 
                && data?.PinType.Equals("personal", StringComparison.OrdinalIgnoreCase) == true;
        });
    }

    public async Task<int> CountSharedPinsForChannelAsync(int clientId, string channelId)
    {
        var pins = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == PinsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return pins.Count(d =>
        {
            var data = TryDeserialize<PinData>(d.Data);
            return data?.ChannelId == channelId 
                && data?.PinType.Equals("shared", StringComparison.OrdinalIgnoreCase) == true;
        });
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

    private class PinData
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("message_id")]
        public int MessageId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pinned_by_agent_id")]
        public int PinnedByAgentId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pin_type")]
        public string PinType { get; set; } = "personal";

        [System.Text.Json.Serialization.JsonPropertyName("channel_id")]
        public string? ChannelId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("note")]
        public string? Note { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("position")]
        public int? Position { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }
    }
}
