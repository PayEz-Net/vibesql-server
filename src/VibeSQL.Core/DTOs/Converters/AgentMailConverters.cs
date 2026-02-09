using VibeSQL.Core.DTOs.AgentMail;
using System.Text.Json;

namespace VibeSQL.Core.DTOs.Converters;

/// <summary>
/// Converters for Agent Mail data models to DTOs.
/// Handles Entity/JSON → DTO transformations for the service layer.
/// </summary>
public static class AgentMailConverters
{
    #region Agent Conversions

    public static AgentMailAgentDto ToDto(this AgentDataModel model)
    {
        if (model == null) return null!;
        return new AgentMailAgentDto
        {
            Id = model.Id,
            Name = model.Name ?? "",
            DisplayName = model.DisplayName ?? "",
            Role = model.Role ?? "",
            Program = model.Program ?? "",
            Model = model.Model ?? "",
            IsShared = model.IsShared ?? false,
            IsActive = model.IsActive ?? true,
            LastActiveAt = model.LastActiveAt
        };
    }

    public static List<AgentMailAgentDto> ToDtos(this IEnumerable<AgentDataModel> models)
    {
        if (models == null) return new List<AgentMailAgentDto>();
        return models.Select(m => m.ToDto()).ToList();
    }

    #endregion

    #region Message Conversions

    public static AgentMailMessageDto ToDto(this MessageDataModel model)
    {
        if (model == null) return null!;
        return new AgentMailMessageDto
        {
            Id = model.Id,
            FromAgentId = model.FromAgentId,
            FromUserId = model.FromUserId,
            ThreadId = model.ThreadId,
            Subject = model.Subject,
            Body = model.Body,
            BodyFormat = model.BodyFormat,
            Importance = model.Importance,
            CreatedAt = model.CreatedAt
        };
    }

    public static List<AgentMailMessageDto> ToDtos(this IEnumerable<MessageDataModel> models)
    {
        if (models == null) return new List<AgentMailMessageDto>();
        return models.Select(m => m.ToDto()).ToList();
    }

    #endregion

    #region JSON Parsing Helpers

    /// <summary>
    /// Safely deserializes JSON to a model, returning null on failure.
    /// </summary>
    public static T? TryDeserialize<T>(string? json) where T : class
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
    /// Parses agent data from VibeDocument.Data JSON and converts to DTO.
    /// </summary>
    public static AgentMailAgentDto? ParseAgentToDto(string? json)
    {
        var model = TryDeserialize<AgentDataModel>(json);
        return model?.ToDto();
    }

    /// <summary>
    /// Parses message data from VibeDocument.Data JSON and converts to DTO.
    /// </summary>
    public static AgentMailMessageDto? ParseMessageToDto(string? json)
    {
        var model = TryDeserialize<MessageDataModel>(json);
        return model?.ToDto();
    }

    /// <summary>
    /// Parses agent data from VibeDocument.Data JSON.
    /// </summary>
    public static AgentDataModel? ParseAgentData(string? json)
    {
        return TryDeserialize<AgentDataModel>(json);
    }

    /// <summary>
    /// Parses message data from VibeDocument.Data JSON.
    /// </summary>
    public static MessageDataModel? ParseMessageData(string? json)
    {
        return TryDeserialize<MessageDataModel>(json);
    }

    /// <summary>
    /// Parses inbox data from VibeDocument.Data JSON.
    /// </summary>
    public static InboxDataModel? ParseInboxData(string? json)
    {
        return TryDeserialize<InboxDataModel>(json);
    }

    /// <summary>
    /// Parses reaction data from VibeDocument.Data JSON.
    /// </summary>
    public static MessageReactionDataModel? ParseReactionData(string? json)
    {
        return TryDeserialize<MessageReactionDataModel>(json);
    }

    /// <summary>
    /// Parses pin data from VibeDocument.Data JSON.
    /// </summary>
    public static MessagePinDataModel? ParsePinData(string? json)
    {
        return TryDeserialize<MessagePinDataModel>(json);
    }

    #endregion

    #region Reaction Conversions

    public static ReactionDto ToDto(this MessageReactionDataModel model)
    {
        if (model == null) return null!;
        return new ReactionDto
        {
            Id = model.Id,
            MessageId = model.MessageId,
            AgentId = model.AgentId,
            ReactionType = model.ReactionType,
            CreatedAt = model.CreatedAt
        };
    }

    #endregion

    #region Pin Conversions

    public static PinDto ToDto(this MessagePinDataModel model)
    {
        if (model == null) return null!;
        return new PinDto
        {
            Id = model.Id,
            MessageId = model.MessageId,
            PinnedBy = new PinnerDto
            {
                Id = model.PinnedByAgentId.ToString(),
                Name = "" // Name is set by the service layer
            },
            PinType = model.PinType,
            ChannelId = model.ChannelId,
            Note = model.Note,
            Position = model.Position,
            CreatedAt = model.CreatedAt
        };
    }

    public static List<PinDto> ToDtos(this IEnumerable<MessagePinDataModel> models)
    {
        if (models == null) return new List<PinDto>();
        return models.Select(m => m.ToDto()).ToList();
    }

    #endregion
}
