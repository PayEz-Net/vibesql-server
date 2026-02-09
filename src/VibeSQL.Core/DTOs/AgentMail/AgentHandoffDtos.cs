using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

#region Enums

/// <summary>
/// Handoff status values following the state machine.
/// </summary>
public static class HandoffStatus
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Declined = "declined";
    public const string Expired = "expired";
    public const string Superseded = "superseded";
}

/// <summary>
/// Handoff urgency levels.
/// </summary>
public static class HandoffUrgency
{
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";
    public const string Critical = "critical";
}

/// <summary>
/// Blocker types.
/// </summary>
public static class BlockerType
{
    public const string Technical = "technical";
    public const string Dependency = "dependency";
    public const string Permission = "permission";
    public const string Information = "information";
    public const string External = "external";
}

/// <summary>
/// Blocker severity.
/// </summary>
public static class BlockerSeverity
{
    public const string Soft = "soft";
    public const string Hard = "hard";
}

/// <summary>
/// Next step types.
/// </summary>
public static class NextStepType
{
    public const string Action = "action";
    public const string Decision = "decision";
    public const string Verification = "verification";
    public const string Delivery = "delivery";
}

/// <summary>
/// Key fact importance levels.
/// </summary>
public static class KeyFactImportance
{
    public const string Critical = "critical";
    public const string High = "high";
    public const string Medium = "medium";
}

/// <summary>
/// Artifact types.
/// </summary>
public static class ArtifactType
{
    public const string File = "file";
    public const string Url = "url";
    public const string Snippet = "snippet";
    public const string Reference = "reference";
}

/// <summary>
/// Decline reasons.
/// </summary>
public static class DeclineReason
{
    public const string Capacity = "capacity";
    public const string Capability = "capability";
    public const string Policy = "policy";
    public const string Other = "other";
}

#endregion

#region Sub-DTOs

/// <summary>
/// Agent identity for handoff participants.
/// </summary>
public class AgentIdentityDto
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    [JsonPropertyName("capabilities")]
    public List<string>? Capabilities { get; set; }
}

/// <summary>
/// Task context describing the work being handed off.
/// </summary>
public class TaskContextDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("origin")]
    public TaskOriginDto? Origin { get; set; }

    [JsonPropertyName("constraints")]
    public List<string>? Constraints { get; set; }

    [JsonPropertyName("deadline")]
    public string? Deadline { get; set; }
}

/// <summary>
/// Origin information for the task.
/// </summary>
public class TaskOriginDto
{
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "";

    [JsonPropertyName("message_id")]
    public string? MessageId { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

/// <summary>
/// Progress state of the task.
/// </summary>
public class ProgressStateDto
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "not_started";

    [JsonPropertyName("percent_complete")]
    public int? PercentComplete { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("completed_steps")]
    public List<CompletedStepDto>? CompletedSteps { get; set; }

    [JsonPropertyName("decisions")]
    public List<DecisionDto>? Decisions { get; set; }
}

/// <summary>
/// A completed step in the task.
/// </summary>
public class CompletedStepDto
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = "";

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// A decision made during the task.
/// </summary>
public class DecisionDto
{
    [JsonPropertyName("what")]
    public string What { get; set; } = "";

    [JsonPropertyName("why")]
    public string Why { get; set; } = "";

    [JsonPropertyName("alternatives")]
    public List<string>? Alternatives { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

/// <summary>
/// A blocker preventing progress.
/// </summary>
public class BlockerDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = BlockerType.Technical;

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = BlockerSeverity.Soft;

    [JsonPropertyName("suggested_resolution")]
    public string? SuggestedResolution { get; set; }

    [JsonPropertyName("waiting_on")]
    public string? WaitingOn { get; set; }
}

/// <summary>
/// A next step to be taken.
/// </summary>
public class NextStepDto
{
    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = NextStepType.Action;

    [JsonPropertyName("prerequisites")]
    public List<string>? Prerequisites { get; set; }

    [JsonPropertyName("estimated_effort")]
    public string? EstimatedEffort { get; set; }

    [JsonPropertyName("hints")]
    public List<string>? Hints { get; set; }
}

/// <summary>
/// Memory snapshot for context continuity.
/// </summary>
public class MemorySnapshotDto
{
    [JsonPropertyName("key_facts")]
    public List<KeyFactDto>? KeyFacts { get; set; }

    [JsonPropertyName("user_preferences")]
    public JsonElement? UserPreferences { get; set; }

    [JsonPropertyName("conversation_highlights")]
    public List<string>? ConversationHighlights { get; set; }

    [JsonPropertyName("warnings")]
    public List<string>? Warnings { get; set; }
}

/// <summary>
/// A key fact to remember.
/// </summary>
public class KeyFactDto
{
    [JsonPropertyName("fact")]
    public string Fact { get; set; } = "";

    [JsonPropertyName("importance")]
    public string Importance { get; set; } = KeyFactImportance.Medium;

    [JsonPropertyName("source")]
    public string? Source { get; set; }
}

/// <summary>
/// An artifact related to the task.
/// </summary>
public class ArtifactDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = ArtifactType.File;

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("location")]
    public string Location { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("relevance")]
    public string Relevance { get; set; } = "";
}

#endregion

#region Request DTOs

/// <summary>
/// Request to create a new handoff.
/// </summary>
public class CreateHandoffRequest
{
    [JsonPropertyName("to")]
    public AgentIdentityDto? To { get; set; }

    [JsonPropertyName("task")]
    public TaskContextDto Task { get; set; } = new();

    [JsonPropertyName("progress")]
    public ProgressStateDto Progress { get; set; } = new();

    [JsonPropertyName("blockers")]
    public List<BlockerDto>? Blockers { get; set; }

    [JsonPropertyName("next_steps")]
    public List<NextStepDto>? NextSteps { get; set; }

    [JsonPropertyName("memory")]
    public MemorySnapshotDto? Memory { get; set; }

    [JsonPropertyName("artifacts")]
    public List<ArtifactDto>? Artifacts { get; set; }

    [JsonPropertyName("urgency")]
    public string Urgency { get; set; } = HandoffUrgency.Normal;

    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; set; }

    [JsonPropertyName("auto_accept")]
    public bool AutoAccept { get; set; }
}

/// <summary>
/// Request to accept a handoff.
/// </summary>
public class AcceptHandoffRequest
{
    [JsonPropertyName("acknowledged_blockers")]
    public List<string>? AcknowledgedBlockers { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// Request to decline a handoff.
/// </summary>
public class DeclineHandoffRequest
{
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = DeclineReason.Other;

    [JsonPropertyName("explanation")]
    public string? Explanation { get; set; }

    [JsonPropertyName("suggest_alternative")]
    public string? SuggestAlternative { get; set; }
}

/// <summary>
/// Query parameters for listing handoffs.
/// </summary>
public class ListHandoffsQuery
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("to")]
    public string? To { get; set; }

    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 20;

    [JsonPropertyName("offset")]
    public int Offset { get; set; } = 0;
}

#endregion

#region Result DTOs

/// <summary>
/// Result from creating a handoff.
/// </summary>
public class CreateHandoffResult
{
    public bool Success { get; set; }
    public HandoffDto? Handoff { get; set; }
    public string Error { get; set; } = "";
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Result from getting a handoff.
/// </summary>
public class GetHandoffResult
{
    public bool Success { get; set; }
    public HandoffDto? Handoff { get; set; }
    public string Error { get; set; } = "";
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Result from listing handoffs.
/// </summary>
public class ListHandoffsResult
{
    public bool Success { get; set; }
    public List<HandoffSummaryDto> Handoffs { get; set; } = new();
    public int Total { get; set; }
    public bool HasMore { get; set; }
    public string Error { get; set; } = "";
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Result from accepting a handoff.
/// </summary>
public class AcceptHandoffResult
{
    public bool Success { get; set; }
    public HandoffDto? Handoff { get; set; }
    public string Error { get; set; } = "";
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Result from declining a handoff.
/// </summary>
public class DeclineHandoffResult
{
    public bool Success { get; set; }
    public HandoffDto? Handoff { get; set; }
    public string Error { get; set; } = "";
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Result from getting handoff history.
/// </summary>
public class GetHandoffHistoryResult
{
    public bool Success { get; set; }
    public string TaskId { get; set; } = "";
    public List<HandoffChainItemDto> Chain { get; set; } = new();
    public AgentIdentityDto? CurrentOwner { get; set; }
    public int TotalHandoffs { get; set; }
    public string Error { get; set; } = "";
    public string? ErrorCode { get; set; }
}

#endregion

#region Response DTOs

/// <summary>
/// Full handoff DTO for API responses.
/// </summary>
public class HandoffDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("status")]
    public string Status { get; set; } = HandoffStatus.Pending;

    [JsonPropertyName("from")]
    public AgentIdentityDto From { get; set; } = new();

    [JsonPropertyName("to")]
    public AgentIdentityDto? To { get; set; }

    [JsonPropertyName("task")]
    public TaskContextDto Task { get; set; } = new();

    [JsonPropertyName("progress")]
    public ProgressStateDto Progress { get; set; } = new();

    [JsonPropertyName("blockers")]
    public List<BlockerDto>? Blockers { get; set; }

    [JsonPropertyName("next_steps")]
    public List<NextStepDto>? NextSteps { get; set; }

    [JsonPropertyName("memory")]
    public MemorySnapshotDto? Memory { get; set; }

    [JsonPropertyName("artifacts")]
    public List<ArtifactDto>? Artifacts { get; set; }

    [JsonPropertyName("urgency")]
    public string Urgency { get; set; } = HandoffUrgency.Normal;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; set; }

    [JsonPropertyName("auto_accept")]
    public bool AutoAccept { get; set; }

    [JsonPropertyName("accepted_at")]
    public string? AcceptedAt { get; set; }

    [JsonPropertyName("accepted_by")]
    public AgentIdentityDto? AcceptedBy { get; set; }

    [JsonPropertyName("accepted_notes")]
    public string? AcceptedNotes { get; set; }

    [JsonPropertyName("declined_at")]
    public string? DeclinedAt { get; set; }

    [JsonPropertyName("decline_reason")]
    public string? DeclineReason { get; set; }

    [JsonPropertyName("decline_explanation")]
    public string? DeclineExplanation { get; set; }

    [JsonPropertyName("suggested_alternative")]
    public string? SuggestedAlternative { get; set; }
}

/// <summary>
/// Summary DTO for listing handoffs.
/// </summary>
public class HandoffSummaryDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("from")]
    public AgentIdentityDto From { get; set; } = new();

    [JsonPropertyName("to")]
    public AgentIdentityDto? To { get; set; }

    [JsonPropertyName("task")]
    public TaskSummaryDto Task { get; set; } = new();

    [JsonPropertyName("urgency")]
    public string Urgency { get; set; } = HandoffUrgency.Normal;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; set; }
}

/// <summary>
/// Task summary for list responses.
/// </summary>
public class TaskSummaryDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
}

/// <summary>
/// Chain item for handoff history.
/// </summary>
public class HandoffChainItemDto
{
    [JsonPropertyName("handoff_id")]
    public string HandoffId { get; set; } = "";

    [JsonPropertyName("from")]
    public AgentIdentityDto From { get; set; } = new();

    [JsonPropertyName("to")]
    public AgentIdentityDto? To { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

#endregion

#region Data Models (for Vibe document storage)

/// <summary>
/// Handoff data model stored in Vibe documents.
/// </summary>
public class HandoffDataModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("status")]
    public string Status { get; set; } = HandoffStatus.Pending;

    [JsonPropertyName("from")]
    public JsonElement? From { get; set; }

    [JsonPropertyName("to")]
    public JsonElement? To { get; set; }

    [JsonPropertyName("task")]
    public JsonElement? Task { get; set; }

    [JsonPropertyName("progress")]
    public JsonElement? Progress { get; set; }

    [JsonPropertyName("blockers")]
    public JsonElement? Blockers { get; set; }

    [JsonPropertyName("next_steps")]
    public JsonElement? NextSteps { get; set; }

    [JsonPropertyName("memory")]
    public JsonElement? Memory { get; set; }

    [JsonPropertyName("artifacts")]
    public JsonElement? Artifacts { get; set; }

    [JsonPropertyName("urgency")]
    public string Urgency { get; set; } = HandoffUrgency.Normal;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; set; }

    [JsonPropertyName("auto_accept")]
    public bool AutoAccept { get; set; }

    [JsonPropertyName("accepted_at")]
    public string? AcceptedAt { get; set; }

    [JsonPropertyName("accepted_by")]
    public JsonElement? AcceptedBy { get; set; }

    [JsonPropertyName("accepted_notes")]
    public string? AcceptedNotes { get; set; }

    [JsonPropertyName("declined_at")]
    public string? DeclinedAt { get; set; }

    [JsonPropertyName("decline_reason")]
    public string? DeclineReason { get; set; }

    [JsonPropertyName("decline_explanation")]
    public string? DeclineExplanation { get; set; }

    [JsonPropertyName("suggested_alternative")]
    public string? SuggestedAlternative { get; set; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    [JsonPropertyName("previous_handoff_id")]
    public string? PreviousHandoffId { get; set; }

    /// <summary>
    /// Convert to full HandoffDto.
    /// </summary>
    public HandoffDto ToDto()
    {
        return new HandoffDto
        {
            Id = Id,
            Version = Version,
            Status = Status,
            From = DeserializeOrDefault<AgentIdentityDto>(From) ?? new AgentIdentityDto(),
            To = DeserializeOrDefault<AgentIdentityDto>(To),
            Task = DeserializeOrDefault<TaskContextDto>(Task) ?? new TaskContextDto(),
            Progress = DeserializeOrDefault<ProgressStateDto>(Progress) ?? new ProgressStateDto(),
            Blockers = DeserializeOrDefault<List<BlockerDto>>(Blockers),
            NextSteps = DeserializeOrDefault<List<NextStepDto>>(NextSteps),
            Memory = DeserializeOrDefault<MemorySnapshotDto>(Memory),
            Artifacts = DeserializeOrDefault<List<ArtifactDto>>(Artifacts),
            Urgency = Urgency,
            CreatedAt = CreatedAt,
            ExpiresAt = ExpiresAt,
            AutoAccept = AutoAccept,
            AcceptedAt = AcceptedAt,
            AcceptedBy = DeserializeOrDefault<AgentIdentityDto>(AcceptedBy),
            AcceptedNotes = AcceptedNotes,
            DeclinedAt = DeclinedAt,
            DeclineReason = DeclineReason,
            DeclineExplanation = DeclineExplanation,
            SuggestedAlternative = SuggestedAlternative
        };
    }

    /// <summary>
    /// Convert to summary DTO.
    /// </summary>
    public HandoffSummaryDto ToSummaryDto()
    {
        var taskData = DeserializeOrDefault<TaskContextDto>(Task);
        return new HandoffSummaryDto
        {
            Id = Id,
            Status = Status,
            From = DeserializeOrDefault<AgentIdentityDto>(From) ?? new AgentIdentityDto(),
            To = DeserializeOrDefault<AgentIdentityDto>(To),
            Task = new TaskSummaryDto
            {
                Id = taskData?.Id ?? "",
                Title = taskData?.Title ?? ""
            },
            Urgency = Urgency,
            CreatedAt = CreatedAt,
            ExpiresAt = ExpiresAt
        };
    }

    private static T? DeserializeOrDefault<T>(JsonElement? element) where T : class
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

#endregion
