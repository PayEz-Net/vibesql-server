using VibeSQL.Core.DTOs.AgentChat;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for agent-to-agent real-time chat communication.
/// Handles business logic, validation, authorization, and orchestrates repository calls.
/// </summary>
public interface IAgentChatService
{
    #region Room Operations

    /// <summary>
    /// List rooms for an agent with optional filtering.
    /// </summary>
    Task<ChatRoomListResult> ListRoomsAsync(string clientId, int userId, string agentName, string? type = null, bool includeArchived = false);

    /// <summary>
    /// Create a new chat room.
    /// </summary>
    Task<ChatRoomResult> CreateRoomAsync(string clientId, int userId, string ownerAgentName, CreateChatRoomRequest request);

    /// <summary>
    /// Get room details by ID.
    /// </summary>
    Task<ChatRoomResult> GetRoomAsync(string clientId, int userId, string agentName, int roomId);

    /// <summary>
    /// Update room settings.
    /// </summary>
    Task<ChatRoomResult> UpdateRoomAsync(string clientId, int userId, string agentName, int roomId, UpdateChatRoomRequest request);

    /// <summary>
    /// Archive or delete a room.
    /// </summary>
    Task<ChatRoomResult> DeleteRoomAsync(string clientId, int userId, string agentName, int roomId, bool hardDelete = false);

    #endregion

    #region Membership Operations

    /// <summary>
    /// Get members of a room.
    /// </summary>
    Task<ChatMemberListResult> GetRoomMembersAsync(string clientId, int userId, string agentName, int roomId);

    /// <summary>
    /// Add members to a room.
    /// </summary>
    Task<ChatMemberListResult> AddMembersAsync(string clientId, int userId, string agentName, int roomId, AddChatMembersRequest request);

    /// <summary>
    /// Update a member's role.
    /// </summary>
    Task<ChatMemberListResult> UpdateMemberRoleAsync(string clientId, int userId, string agentName, int roomId, string targetAgentName, UpdateChatMemberRequest request);

    /// <summary>
    /// Remove a member from a room.
    /// </summary>
    Task<ChatMemberListResult> RemoveMemberAsync(string clientId, int userId, string agentName, int roomId, string targetAgentName);

    /// <summary>
    /// Leave a room (self-removal).
    /// </summary>
    Task<ChatRoomResult> LeaveRoomAsync(string clientId, int userId, string agentName, int roomId);

    #endregion

    #region Message Operations

    /// <summary>
    /// Get message history for a room.
    /// </summary>
    Task<ChatMessageListResult> GetMessagesAsync(string clientId, int userId, string agentName, int roomId, int? beforeId = null, int limit = 50);

    /// <summary>
    /// Send a message to a room.
    /// </summary>
    Task<ChatMessageResult> SendMessageAsync(string clientId, int userId, string senderAgentName, int roomId, SendChatMessageRequest request);

    /// <summary>
    /// Edit a message.
    /// </summary>
    Task<ChatMessageResult> EditMessageAsync(string clientId, int userId, string agentName, int roomId, int messageId, EditChatMessageRequest request);

    /// <summary>
    /// Delete a message.
    /// </summary>
    Task<ChatMessageResult> DeleteMessageAsync(string clientId, int userId, string agentName, int roomId, int messageId);

    /// <summary>
    /// Mark messages as read.
    /// </summary>
    Task<MarkReadResult> MarkReadAsync(string clientId, int userId, string agentName, int roomId, MarkMessagesReadRequest request);

    #endregion

    #region Direct Messages

    /// <summary>
    /// Start or get an existing DM with another agent.
    /// </summary>
    Task<ChatRoomResult> StartDirectMessageAsync(string clientId, int userId, string fromAgentName, StartDirectMessageRequest request);

    #endregion

    #region Presence Operations

    /// <summary>
    /// Get presence status for specified agents.
    /// </summary>
    Task<ChatPresenceListResult> GetPresenceAsync(string clientId, int userId, List<string>? agentNames = null);

    /// <summary>
    /// Update own presence status.
    /// </summary>
    Task<ChatPresenceResult> UpdatePresenceAsync(string clientId, int userId, string agentName, ChatUpdatePresenceRequest request);

    /// <summary>
    /// Get presence for all members of a room.
    /// </summary>
    Task<ChatPresenceListResult> GetRoomPresenceAsync(string clientId, int userId, string agentName, int roomId);

    #endregion

    #region Typing Indicators

    /// <summary>
    /// Set typing indicator for an agent in a room.
    /// </summary>
    Task<TypingIndicatorResult> SetTypingAsync(string clientId, int userId, string agentName, int roomId, TypingIndicatorRequest request);

    /// <summary>
    /// Get currently typing agents in a room.
    /// </summary>
    Task<RoomTypingStatusResult> GetTypingStatusAsync(string clientId, int userId, string agentName, int roomId);

    #endregion
}
