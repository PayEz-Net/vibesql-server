using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for agent_chat_rooms table operations.
/// Handles CRUD operations for chat rooms.
/// </summary>
public interface IAgentChatRoomRepository
{
    /// <summary>
    /// Get a room by ID.
    /// </summary>
    Task<VibeDocument?> GetRoomAsync(int clientId, int roomId);

    /// <summary>
    /// Get rooms for an agent (rooms they are a member of).
    /// </summary>
    Task<List<VibeDocument>> GetRoomsForAgentAsync(int clientId, int agentId, string? type = null, bool includeArchived = false);

    /// <summary>
    /// Find existing DM room between two agents.
    /// </summary>
    Task<VibeDocument?> FindDmRoomAsync(int clientId, int agentId1, int agentId2);

    /// <summary>
    /// Create a new room.
    /// </summary>
    Task<VibeDocument> CreateRoomAsync(int clientId, int ownerUserId, int? ownerAgentId, string type, string? name, string? description, int? teamId);

    /// <summary>
    /// Update room details.
    /// </summary>
    Task<bool> UpdateRoomAsync(int clientId, int roomId, string? name, string? description, string? settingsJson);

    /// <summary>
    /// Update last message timestamp.
    /// </summary>
    Task<bool> UpdateLastMessageAtAsync(int clientId, int roomId);

    /// <summary>
    /// Archive/unarchive a room.
    /// </summary>
    Task<bool> SetArchiveStatusAsync(int clientId, int roomId, bool isArchived);

    /// <summary>
    /// Delete a room (soft delete).
    /// </summary>
    Task<bool> DeleteRoomAsync(int clientId, int roomId);

    /// <summary>
    /// Get room count for an agent.
    /// </summary>
    Task<int> GetRoomCountForAgentAsync(int clientId, int agentId);
}

/// <summary>
/// Repository for agent_chat_messages table operations.
/// Handles message CRUD and history retrieval.
/// </summary>
public interface IAgentChatMessageRepository
{
    /// <summary>
    /// Get a message by ID.
    /// </summary>
    Task<VibeDocument?> GetMessageAsync(int clientId, int messageId);

    /// <summary>
    /// Get messages for a room with pagination (cursor-based).
    /// </summary>
    Task<(List<VibeDocument> Messages, bool HasMore)> GetMessagesAsync(int clientId, int roomId, int? beforeId = null, int limit = 50);

    /// <summary>
    /// Create a new message.
    /// </summary>
    Task<VibeDocument> CreateMessageAsync(int clientId, int roomId, int senderAgentId, int? senderUserId, string content, string contentType, string? metadataJson);

    /// <summary>
    /// Update message content (edit).
    /// </summary>
    Task<bool> UpdateMessageAsync(int clientId, int messageId, string newContent);

    /// <summary>
    /// Soft delete a message.
    /// </summary>
    Task<bool> DeleteMessageAsync(int clientId, int messageId);

    /// <summary>
    /// Get the latest message ID in a room.
    /// </summary>
    Task<int?> GetLatestMessageIdAsync(int clientId, int roomId);

    /// <summary>
    /// Count unread messages for an agent in a room.
    /// </summary>
    Task<int> CountUnreadMessagesAsync(int clientId, int roomId, int? lastReadMessageId);
}

/// <summary>
/// Repository for agent_chat_members table operations.
/// Handles room membership management.
/// </summary>
public interface IAgentChatMemberRepository
{
    /// <summary>
    /// Get a membership record.
    /// </summary>
    Task<VibeDocument?> GetMemberAsync(int clientId, int roomId, int agentId);

    /// <summary>
    /// Get all members of a room.
    /// </summary>
    Task<List<VibeDocument>> GetRoomMembersAsync(int clientId, int roomId);

    /// <summary>
    /// Get all room IDs an agent is a member of.
    /// </summary>
    Task<List<int>> GetRoomIdsForAgentAsync(int clientId, int agentId);

    /// <summary>
    /// Add a member to a room.
    /// </summary>
    Task<VibeDocument> AddMemberAsync(int clientId, int roomId, int agentId, string role);

    /// <summary>
    /// Update member role.
    /// </summary>
    Task<bool> UpdateMemberRoleAsync(int clientId, int roomId, int agentId, string newRole);

    /// <summary>
    /// Update last read message.
    /// </summary>
    Task<bool> UpdateLastReadAsync(int clientId, int roomId, int agentId, int messageId);

    /// <summary>
    /// Remove a member from a room.
    /// </summary>
    Task<bool> RemoveMemberAsync(int clientId, int roomId, int agentId);

    /// <summary>
    /// Check if an agent is a member of a room.
    /// </summary>
    Task<bool> IsMemberAsync(int clientId, int roomId, int agentId);

    /// <summary>
    /// Get member count for a room.
    /// </summary>
    Task<int> GetMemberCountAsync(int clientId, int roomId);
}

/// <summary>
/// Repository for agent_chat_typing table operations.
/// Handles typing indicator state.
/// </summary>
public interface IAgentChatTypingRepository
{
    /// <summary>
    /// Set typing indicator for an agent in a room.
    /// </summary>
    Task<VibeDocument> SetTypingAsync(int clientId, int roomId, int agentId, DateTimeOffset expiresAt);

    /// <summary>
    /// Clear typing indicator for an agent in a room.
    /// </summary>
    Task<bool> ClearTypingAsync(int clientId, int roomId, int agentId);

    /// <summary>
    /// Get all currently typing agents in a room.
    /// </summary>
    Task<List<VibeDocument>> GetTypingAgentsAsync(int clientId, int roomId);

    /// <summary>
    /// Clear expired typing indicators.
    /// </summary>
    Task<int> ClearExpiredTypingAsync(int clientId);
}
