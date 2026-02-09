using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentChat;

#region Room Types and Roles

public static class ChatRoomType
{
    public const string DirectMessage = "dm";
    public const string Group = "group";
    public const string Team = "team";
}

public static class ChatMemberRole
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Member = "member";
}

public static class ChatPresenceStatus
{
    public const string Online = "online";
    public const string Away = "away";
    public const string Offline = "offline";
    public const string DoNotDisturb = "dnd";
}

public static class ChatContentType
{
    public const string Text = "text";
    public const string Markdown = "markdown";
    public const string Code = "code";
    public const string System = "system";
}

#endregion

#region Request DTOs

public class CreateChatRoomRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = ChatRoomType.Group;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("member_agents")]
    public List<string> MemberAgents { get; set; } = new();

    [JsonPropertyName("settings")]
    public ChatRoomSettingsDto? Settings { get; set; }
}

public class UpdateChatRoomRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("settings")]
    public ChatRoomSettingsDto? Settings { get; set; }
}

public class AddChatMembersRequest
{
    [JsonPropertyName("agent_names")]
    public List<string> AgentNames { get; set; } = new();

    [JsonPropertyName("role")]
    public string Role { get; set; } = ChatMemberRole.Member;
}

public class UpdateChatMemberRequest
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = ChatMemberRole.Member;
}

public class SendChatMessageRequest
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = ChatContentType.Text;

    [JsonPropertyName("reply_to_message_id")]
    public int? ReplyToMessageId { get; set; }
}

public class EditChatMessageRequest
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class MarkMessagesReadRequest
{
    [JsonPropertyName("up_to_message_id")]
    public int UpToMessageId { get; set; }
}

public class ChatUpdatePresenceRequest
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = ChatPresenceStatus.Online;

    [JsonPropertyName("status_text")]
    public string? StatusText { get; set; }
}

public class StartDirectMessageRequest
{
    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = "";

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class TypingIndicatorRequest
{
    [JsonPropertyName("is_typing")]
    public bool IsTyping { get; set; } = true;
}

#endregion

#region Result DTOs

public class ChatRoomListResult
{
    public bool Success { get; set; }
    public List<ChatRoomDto> Rooms { get; set; } = new();
    public int TotalCount { get; set; }
    public string? Error { get; set; }
}

public class ChatRoomResult
{
    public bool Success { get; set; }
    public ChatRoomDto? Room { get; set; }
    public string? Error { get; set; }
}

public class ChatMessageListResult
{
    public bool Success { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
    public bool HasMore { get; set; }
    public string? Error { get; set; }
}

public class ChatMessageResult
{
    public bool Success { get; set; }
    public ChatMessageDto? Message { get; set; }
    public string? Error { get; set; }
}

public class ChatMemberListResult
{
    public bool Success { get; set; }
    public List<ChatMemberDto> Members { get; set; } = new();
    public string? Error { get; set; }
}

public class ChatPresenceListResult
{
    public bool Success { get; set; }
    public Dictionary<string, ChatPresenceDto> Presence { get; set; } = new();
    public string? Error { get; set; }
}

public class ChatPresenceResult
{
    public bool Success { get; set; }
    public ChatPresenceDto? Presence { get; set; }
    public string? Error { get; set; }
}

public class MarkReadResult
{
    public bool Success { get; set; }
    public int RoomId { get; set; }
    public int UpToMessageId { get; set; }
    public DateTimeOffset ReadAt { get; set; }
    public string? Error { get; set; }
}

public class TypingIndicatorResult
{
    public bool Success { get; set; }
    public int RoomId { get; set; }
    public int AgentId { get; set; }
    public string AgentName { get; set; } = "";
    public bool IsTyping { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? Error { get; set; }
}

public class RoomTypingStatusResult
{
    public bool Success { get; set; }
    public int RoomId { get; set; }
    public List<TypingAgentDto> TypingAgents { get; set; } = new();
    public string? Error { get; set; }
}

#endregion

#region Data DTOs

public class ChatRoomDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = ChatRoomType.Group;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("owner_agent_id")]
    public int? OwnerAgentId { get; set; }

    [JsonPropertyName("owner_agent_name")]
    public string? OwnerAgentName { get; set; }

    [JsonPropertyName("team_id")]
    public int? TeamId { get; set; }

    [JsonPropertyName("member_count")]
    public int MemberCount { get; set; }

    [JsonPropertyName("unread_count")]
    public int UnreadCount { get; set; }

    [JsonPropertyName("last_message")]
    public ChatMessageDto? LastMessage { get; set; }

    [JsonPropertyName("settings")]
    public ChatRoomSettingsDto? Settings { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("last_message_at")]
    public string? LastMessageAt { get; set; }
}

public class ChatRoomSettingsDto
{
    [JsonPropertyName("message_retention_days")]
    public int MessageRetentionDays { get; set; } = 90;

    [JsonPropertyName("allow_guests")]
    public bool AllowGuests { get; set; } = false;

    [JsonPropertyName("is_archived")]
    public bool IsArchived { get; set; } = false;
}

public class ChatMessageDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("room_id")]
    public int RoomId { get; set; }

    [JsonPropertyName("sender_agent_id")]
    public int SenderAgentId { get; set; }

    [JsonPropertyName("sender_agent_name")]
    public string SenderAgentName { get; set; } = "";

    [JsonPropertyName("sender_agent_display")]
    public string SenderAgentDisplay { get; set; } = "";

    [JsonPropertyName("sender_user_id")]
    public int? SenderUserId { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = ChatContentType.Text;

    [JsonPropertyName("reply_to_message_id")]
    public int? ReplyToMessageId { get; set; }

    [JsonPropertyName("mentions")]
    public List<int>? Mentions { get; set; }

    [JsonPropertyName("edited_at")]
    public string? EditedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public string? DeletedAt { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
}

public class ChatMemberDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("room_id")]
    public int RoomId { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = "";

    [JsonPropertyName("agent_display_name")]
    public string AgentDisplayName { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = ChatMemberRole.Member;

    [JsonPropertyName("joined_at")]
    public string? JoinedAt { get; set; }

    [JsonPropertyName("last_read_message_id")]
    public int? LastReadMessageId { get; set; }

    [JsonPropertyName("last_read_at")]
    public string? LastReadAt { get; set; }

    [JsonPropertyName("settings")]
    public ChatMemberSettingsDto? Settings { get; set; }

    [JsonPropertyName("presence")]
    public ChatPresenceDto? Presence { get; set; }
}

public class ChatMemberSettingsDto
{
    [JsonPropertyName("notifications_enabled")]
    public bool NotificationsEnabled { get; set; } = true;

    [JsonPropertyName("pinned")]
    public bool Pinned { get; set; } = false;

    [JsonPropertyName("muted_until")]
    public string? MutedUntil { get; set; }
}

public class ChatPresenceDto
{
    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = ChatPresenceStatus.Offline;

    [JsonPropertyName("status_text")]
    public string? StatusText { get; set; }

    [JsonPropertyName("last_active_at")]
    public string? LastActiveAt { get; set; }

    [JsonPropertyName("last_heartbeat_at")]
    public string? LastHeartbeatAt { get; set; }
}

public class TypingAgentDto
{
    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = "";

    [JsonPropertyName("started_at")]
    public string? StartedAt { get; set; }

    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; set; }
}

#endregion

#region Internal Data Models (for JSON serialization in VibeDocument)

public class ChatRoomData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = ChatRoomType.Group;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("owner_agent_id")]
    public int? OwnerAgentId { get; set; }

    [JsonPropertyName("team_id")]
    public int? TeamId { get; set; }

    [JsonPropertyName("settings")]
    public ChatRoomSettingsDto? Settings { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("last_message_at")]
    public string? LastMessageAt { get; set; }
}

public class ChatMessageData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("room_id")]
    public int RoomId { get; set; }

    [JsonPropertyName("sender_agent_id")]
    public int SenderAgentId { get; set; }

    [JsonPropertyName("sender_user_id")]
    public int? SenderUserId { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = ChatContentType.Text;

    [JsonPropertyName("metadata")]
    public ChatMessageMetadata? Metadata { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
}

public class ChatMessageMetadata
{
    [JsonPropertyName("mentions")]
    public List<int>? Mentions { get; set; }

    [JsonPropertyName("reply_to_message_id")]
    public int? ReplyToMessageId { get; set; }

    [JsonPropertyName("edited_at")]
    public string? EditedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public string? DeletedAt { get; set; }
}

public class ChatMemberData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("room_id")]
    public int RoomId { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = ChatMemberRole.Member;

    [JsonPropertyName("joined_at")]
    public string? JoinedAt { get; set; }

    [JsonPropertyName("last_read_message_id")]
    public int? LastReadMessageId { get; set; }

    [JsonPropertyName("last_read_at")]
    public string? LastReadAt { get; set; }

    [JsonPropertyName("settings")]
    public ChatMemberSettingsDto? Settings { get; set; }
}

public class ChatTypingData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("room_id")]
    public int RoomId { get; set; }

    [JsonPropertyName("agent_id")]
    public int AgentId { get; set; }

    [JsonPropertyName("started_at")]
    public string? StartedAt { get; set; }

    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; set; }
}

#endregion
