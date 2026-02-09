using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.DTOs.AgentChat;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for agent_chat_rooms table operations.
/// </summary>
public class AgentChatRoomRepository : IAgentChatRoomRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AgentChatRoomRepository> _logger;

    private const string CollectionName = "agent_chat";
    private const string RoomsTable = "agent_chat_rooms";

    public AgentChatRoomRepository(VibeDbContext context, ILogger<AgentChatRoomRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument?> GetRoomAsync(int clientId, int roomId)
    {
        var rooms = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == RoomsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return rooms.FirstOrDefault(d =>
        {
            var data = TryDeserialize<ChatRoomData>(d.Data);
            return data?.Id == roomId;
        });
    }

    public async Task<List<VibeDocument>> GetRoomsForAgentAsync(int clientId, int agentId, string? type = null, bool includeArchived = false)
    {
        // Get room IDs for agent from member repository would be better but for simplicity we filter here
        var rooms = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == RoomsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return rooms.Where(d =>
        {
            var data = TryDeserialize<ChatRoomData>(d.Data);
            if (data == null) return false;
            if (type != null && data.Type != type) return false;
            if (!includeArchived && data.Settings?.IsArchived == true) return false;
            return true;
        }).OrderByDescending(d =>
        {
            var data = TryDeserialize<ChatRoomData>(d.Data);
            return data?.LastMessageAt ?? data?.CreatedAt ?? "";
        }).ToList();
    }

    public async Task<VibeDocument?> FindDmRoomAsync(int clientId, int agentId1, int agentId2)
    {
        // DM room names are sorted agent IDs: dm_{min}_{max}
        var minId = Math.Min(agentId1, agentId2);
        var maxId = Math.Max(agentId1, agentId2);
        var dmName = $"dm_{minId}_{maxId}";

        var rooms = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == RoomsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return rooms.FirstOrDefault(d =>
        {
            var data = TryDeserialize<ChatRoomData>(d.Data);
            return data?.Type == ChatRoomType.DirectMessage && data?.Name == dmName;
        });
    }

    public async Task<VibeDocument> CreateRoomAsync(int clientId, int ownerUserId, int? ownerAgentId, string type, string? name, string? description, int? teamId)
    {
        var now = DateTimeOffset.UtcNow;
        var nextId = await GetNextIdAsync(clientId, "rooms");

        var roomData = new ChatRoomData
        {
            Id = nextId,
            Type = type,
            Name = name,
            Description = description,
            OwnerAgentId = ownerAgentId,
            TeamId = teamId,
            Settings = new ChatRoomSettingsDto(),
            CreatedAt = now.ToString("o"),
            UpdatedAt = now.ToString("o")
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = ownerUserId,
            Collection = CollectionName,
            TableName = RoomsTable,
            Data = JsonSerializer.Serialize(roomData),
            CreatedAt = now,
            CreatedBy = ownerUserId
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("AGENT_CHAT_ROOM_CREATED: RoomId={RoomId}, Type={Type}, ClientId={ClientId}",
            nextId, type, clientId);

        return document;
    }

    public async Task<bool> UpdateRoomAsync(int clientId, int roomId, string? name, string? description, string? settingsJson)
    {
        var room = await GetRoomAsync(clientId, roomId);
        if (room == null) return false;

        var data = TryDeserialize<ChatRoomData>(room.Data);
        if (data == null) return false;

        if (name != null) data.Name = name;
        if (description != null) data.Description = description;
        if (settingsJson != null) data.Settings = TryDeserialize<ChatRoomSettingsDto>(settingsJson);
        data.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");

        room.Data = JsonSerializer.Serialize(data);
        room.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateLastMessageAtAsync(int clientId, int roomId)
    {
        var room = await GetRoomAsync(clientId, roomId);
        if (room == null) return false;

        var data = TryDeserialize<ChatRoomData>(room.Data);
        if (data == null) return false;

        data.LastMessageAt = DateTimeOffset.UtcNow.ToString("o");
        data.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");

        room.Data = JsonSerializer.Serialize(data);
        room.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetArchiveStatusAsync(int clientId, int roomId, bool isArchived)
    {
        var room = await GetRoomAsync(clientId, roomId);
        if (room == null) return false;

        var data = TryDeserialize<ChatRoomData>(room.Data);
        if (data == null) return false;

        data.Settings ??= new ChatRoomSettingsDto();
        data.Settings.IsArchived = isArchived;
        data.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");

        room.Data = JsonSerializer.Serialize(data);
        room.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteRoomAsync(int clientId, int roomId)
    {
        var room = await GetRoomAsync(clientId, roomId);
        if (room == null) return false;

        room.DeletedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetRoomCountForAgentAsync(int clientId, int agentId)
    {
        var rooms = await GetRoomsForAgentAsync(clientId, agentId);
        return rooms.Count;
    }

    private async Task<int> GetNextIdAsync(int clientId, string entity)
    {
        var seqName = $"vibe.seq_{clientId}_{CollectionName}_{entity}";

        try
        {
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            try
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = $"CREATE SEQUENCE IF NOT EXISTS {seqName} START WITH 1 INCREMENT BY 1";
                await createCmd.ExecuteNonQueryAsync();

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
            var allDocs = await _context.Documents
                .Where(d => d.ClientId == clientId
                         && d.Collection == CollectionName
                         && d.TableName == RoomsTable
                         && d.DeletedAt == null)
                .ToListAsync();

            var maxId = allDocs
                .Select(d => TryDeserialize<ChatRoomData>(d.Data)?.Id ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            return maxId + 1;
        }
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return null; }
    }
}

/// <summary>
/// Repository for agent_chat_messages table operations.
/// </summary>
public class AgentChatMessageRepository : IAgentChatMessageRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AgentChatMessageRepository> _logger;

    private const string CollectionName = "agent_chat";
    private const string MessagesTable = "agent_chat_messages";

    public AgentChatMessageRepository(VibeDbContext context, ILogger<AgentChatMessageRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument?> GetMessageAsync(int clientId, int messageId)
    {
        var messages = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MessagesTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return messages.FirstOrDefault(d =>
        {
            var data = TryDeserialize<ChatMessageData>(d.Data);
            return data?.Id == messageId;
        });
    }

    public async Task<(List<VibeDocument> Messages, bool HasMore)> GetMessagesAsync(int clientId, int roomId, int? beforeId = null, int limit = 50)
    {
        var messages = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MessagesTable
                     && d.DeletedAt == null)
            .ToListAsync();

        var filtered = messages
            .Select(d => new { Document = d, Data = TryDeserialize<ChatMessageData>(d.Data) })
            .Where(x => x.Data?.RoomId == roomId)
            .Where(x => beforeId == null || x.Data!.Id < beforeId)
            .OrderByDescending(x => x.Data!.Id)
            .Take(limit + 1)
            .ToList();

        var hasMore = filtered.Count > limit;
        var result = filtered.Take(limit).Select(x => x.Document).ToList();

        return (result, hasMore);
    }

    public async Task<VibeDocument> CreateMessageAsync(int clientId, int roomId, int senderAgentId, int? senderUserId, string content, string contentType, string? metadataJson)
    {
        var now = DateTimeOffset.UtcNow;
        var nextId = await GetNextIdAsync(clientId);

        var messageData = new ChatMessageData
        {
            Id = nextId,
            RoomId = roomId,
            SenderAgentId = senderAgentId,
            SenderUserId = senderUserId,
            Content = content,
            ContentType = contentType,
            Metadata = string.IsNullOrEmpty(metadataJson) ? null : TryDeserialize<ChatMessageMetadata>(metadataJson),
            CreatedAt = now.ToString("o")
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = senderUserId ?? 0,
            Collection = CollectionName,
            TableName = MessagesTable,
            Data = JsonSerializer.Serialize(messageData),
            CreatedAt = now,
            CreatedBy = senderUserId ?? 0
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("AGENT_CHAT_MESSAGE_SENT: RoomId={RoomId}, MessageId={MessageId}, SenderId={SenderId}",
            roomId, nextId, senderAgentId);

        return document;
    }

    public async Task<bool> UpdateMessageAsync(int clientId, int messageId, string newContent)
    {
        var message = await GetMessageAsync(clientId, messageId);
        if (message == null) return false;

        var data = TryDeserialize<ChatMessageData>(message.Data);
        if (data == null) return false;

        data.Content = newContent;
        data.Metadata ??= new ChatMessageMetadata();
        data.Metadata.EditedAt = DateTimeOffset.UtcNow.ToString("o");

        message.Data = JsonSerializer.Serialize(data);
        message.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteMessageAsync(int clientId, int messageId)
    {
        var message = await GetMessageAsync(clientId, messageId);
        if (message == null) return false;

        var data = TryDeserialize<ChatMessageData>(message.Data);
        if (data == null) return false;

        data.Metadata ??= new ChatMessageMetadata();
        data.Metadata.DeletedAt = DateTimeOffset.UtcNow.ToString("o");
        data.Content = "[Message deleted]";

        message.Data = JsonSerializer.Serialize(data);
        message.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int?> GetLatestMessageIdAsync(int clientId, int roomId)
    {
        var messages = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MessagesTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return messages
            .Select(d => TryDeserialize<ChatMessageData>(d.Data))
            .Where(data => data?.RoomId == roomId)
            .OrderByDescending(data => data!.Id)
            .FirstOrDefault()?.Id;
    }

    public async Task<int> CountUnreadMessagesAsync(int clientId, int roomId, int? lastReadMessageId)
    {
        if (!lastReadMessageId.HasValue) return 0;

        var messages = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MessagesTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return messages
            .Select(d => TryDeserialize<ChatMessageData>(d.Data))
            .Count(data => data?.RoomId == roomId && data?.Id > lastReadMessageId);
    }

    private async Task<int> GetNextIdAsync(int clientId)
    {
        var seqName = $"vibe.seq_{clientId}_{CollectionName}_messages";

        try
        {
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            try
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = $"CREATE SEQUENCE IF NOT EXISTS {seqName} START WITH 1 INCREMENT BY 1";
                await createCmd.ExecuteNonQueryAsync();

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
            var allDocs = await _context.Documents
                .Where(d => d.ClientId == clientId
                         && d.Collection == CollectionName
                         && d.TableName == MessagesTable
                         && d.DeletedAt == null)
                .ToListAsync();

            var maxId = allDocs
                .Select(d => TryDeserialize<ChatMessageData>(d.Data)?.Id ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            return maxId + 1;
        }
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return null; }
    }
}

/// <summary>
/// Repository for agent_chat_members table operations.
/// </summary>
public class AgentChatMemberRepository : IAgentChatMemberRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AgentChatMemberRepository> _logger;

    private const string CollectionName = "agent_chat";
    private const string MembersTable = "agent_chat_members";

    public AgentChatMemberRepository(VibeDbContext context, ILogger<AgentChatMemberRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument?> GetMemberAsync(int clientId, int roomId, int agentId)
    {
        var members = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MembersTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return members.FirstOrDefault(d =>
        {
            var data = TryDeserialize<ChatMemberData>(d.Data);
            return data?.RoomId == roomId && data?.AgentId == agentId;
        });
    }

    public async Task<List<VibeDocument>> GetRoomMembersAsync(int clientId, int roomId)
    {
        var members = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MembersTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return members.Where(d =>
        {
            var data = TryDeserialize<ChatMemberData>(d.Data);
            return data?.RoomId == roomId;
        }).ToList();
    }

    public async Task<List<int>> GetRoomIdsForAgentAsync(int clientId, int agentId)
    {
        var members = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MembersTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return members
            .Select(d => TryDeserialize<ChatMemberData>(d.Data))
            .Where(data => data?.AgentId == agentId)
            .Select(data => data!.RoomId)
            .ToList();
    }

    public async Task<VibeDocument> AddMemberAsync(int clientId, int roomId, int agentId, string role)
    {
        var now = DateTimeOffset.UtcNow;
        var nextId = await GetNextIdAsync(clientId);

        var memberData = new ChatMemberData
        {
            Id = nextId,
            RoomId = roomId,
            AgentId = agentId,
            Role = role,
            JoinedAt = now.ToString("o"),
            Settings = new ChatMemberSettingsDto()
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = 0,
            Collection = CollectionName,
            TableName = MembersTable,
            Data = JsonSerializer.Serialize(memberData),
            CreatedAt = now,
            CreatedBy = 0
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("AGENT_CHAT_MEMBER_ADDED: RoomId={RoomId}, AgentId={AgentId}, Role={Role}",
            roomId, agentId, role);

        return document;
    }

    public async Task<bool> UpdateMemberRoleAsync(int clientId, int roomId, int agentId, string newRole)
    {
        var member = await GetMemberAsync(clientId, roomId, agentId);
        if (member == null) return false;

        var data = TryDeserialize<ChatMemberData>(member.Data);
        if (data == null) return false;

        data.Role = newRole;
        member.Data = JsonSerializer.Serialize(data);
        member.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateLastReadAsync(int clientId, int roomId, int agentId, int messageId)
    {
        var member = await GetMemberAsync(clientId, roomId, agentId);
        if (member == null) return false;

        var data = TryDeserialize<ChatMemberData>(member.Data);
        if (data == null) return false;

        data.LastReadMessageId = messageId;
        data.LastReadAt = DateTimeOffset.UtcNow.ToString("o");
        member.Data = JsonSerializer.Serialize(data);
        member.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveMemberAsync(int clientId, int roomId, int agentId)
    {
        var member = await GetMemberAsync(clientId, roomId, agentId);
        if (member == null) return false;

        member.DeletedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("AGENT_CHAT_MEMBER_REMOVED: RoomId={RoomId}, AgentId={AgentId}", roomId, agentId);
        return true;
    }

    public async Task<bool> IsMemberAsync(int clientId, int roomId, int agentId)
    {
        var member = await GetMemberAsync(clientId, roomId, agentId);
        return member != null;
    }

    public async Task<int> GetMemberCountAsync(int clientId, int roomId)
    {
        var members = await GetRoomMembersAsync(clientId, roomId);
        return members.Count;
    }

    private async Task<int> GetNextIdAsync(int clientId)
    {
        var seqName = $"vibe.seq_{clientId}_{CollectionName}_members";

        try
        {
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            try
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = $"CREATE SEQUENCE IF NOT EXISTS {seqName} START WITH 1 INCREMENT BY 1";
                await createCmd.ExecuteNonQueryAsync();

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
            var allDocs = await _context.Documents
                .Where(d => d.ClientId == clientId
                         && d.Collection == CollectionName
                         && d.TableName == MembersTable
                         && d.DeletedAt == null)
                .ToListAsync();

            var maxId = allDocs
                .Select(d => TryDeserialize<ChatMemberData>(d.Data)?.Id ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            return maxId + 1;
        }
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return null; }
    }
}

/// <summary>
/// Repository for agent_chat_typing table operations.
/// </summary>
public class AgentChatTypingRepository : IAgentChatTypingRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AgentChatTypingRepository> _logger;

    private const string CollectionName = "agent_chat";
    private const string TypingTable = "agent_chat_typing";

    public AgentChatTypingRepository(VibeDbContext context, ILogger<AgentChatTypingRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument> SetTypingAsync(int clientId, int roomId, int agentId, DateTimeOffset expiresAt)
    {
        var now = DateTimeOffset.UtcNow;

        // Check if existing typing record exists
        var existing = await GetTypingRecordAsync(clientId, roomId, agentId);
        if (existing != null)
        {
            var data = TryDeserialize<ChatTypingData>(existing.Data);
            if (data != null)
            {
                data.ExpiresAt = expiresAt.ToString("o");
                existing.Data = JsonSerializer.Serialize(data);
                existing.UpdatedAt = now;
                await _context.SaveChangesAsync();
                return existing;
            }
        }

        // Create new typing record
        var nextId = await GetNextIdAsync(clientId);
        var typingData = new ChatTypingData
        {
            Id = nextId,
            RoomId = roomId,
            AgentId = agentId,
            StartedAt = now.ToString("o"),
            ExpiresAt = expiresAt.ToString("o")
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = 0,
            Collection = CollectionName,
            TableName = TypingTable,
            Data = JsonSerializer.Serialize(typingData),
            CreatedAt = now,
            CreatedBy = 0
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        return document;
    }

    public async Task<bool> ClearTypingAsync(int clientId, int roomId, int agentId)
    {
        var existing = await GetTypingRecordAsync(clientId, roomId, agentId);
        if (existing == null) return false;

        existing.DeletedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<VibeDocument>> GetTypingAgentsAsync(int clientId, int roomId)
    {
        var now = DateTimeOffset.UtcNow;
        var typing = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == TypingTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return typing.Where(d =>
        {
            var data = TryDeserialize<ChatTypingData>(d.Data);
            if (data?.RoomId != roomId) return false;
            if (DateTimeOffset.TryParse(data?.ExpiresAt, out var expires) && expires < now) return false;
            return true;
        }).ToList();
    }

    public async Task<int> ClearExpiredTypingAsync(int clientId)
    {
        var now = DateTimeOffset.UtcNow;
        var typing = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == TypingTable
                     && d.DeletedAt == null)
            .ToListAsync();

        var expired = typing.Where(d =>
        {
            var data = TryDeserialize<ChatTypingData>(d.Data);
            return DateTimeOffset.TryParse(data?.ExpiresAt, out var expires) && expires < now;
        }).ToList();

        foreach (var doc in expired)
        {
            doc.DeletedAt = now;
        }

        await _context.SaveChangesAsync();
        return expired.Count;
    }

    private async Task<VibeDocument?> GetTypingRecordAsync(int clientId, int roomId, int agentId)
    {
        var typing = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == TypingTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return typing.FirstOrDefault(d =>
        {
            var data = TryDeserialize<ChatTypingData>(d.Data);
            return data?.RoomId == roomId && data?.AgentId == agentId;
        });
    }

    private async Task<int> GetNextIdAsync(int clientId)
    {
        var seqName = $"vibe.seq_{clientId}_{CollectionName}_typing";

        try
        {
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            try
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = $"CREATE SEQUENCE IF NOT EXISTS {seqName} START WITH 1 INCREMENT BY 1";
                await createCmd.ExecuteNonQueryAsync();

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
        catch
        {
            var allDocs = await _context.Documents
                .Where(d => d.ClientId == clientId
                         && d.Collection == CollectionName
                         && d.TableName == TypingTable
                         && d.DeletedAt == null)
                .ToListAsync();

            var maxId = allDocs
                .Select(d => TryDeserialize<ChatTypingData>(d.Data)?.Id ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            return maxId + 1;
        }
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return null; }
    }
}
