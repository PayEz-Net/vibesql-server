namespace VibeSQL.Core.Models.Standup;

/// <summary>
/// Request model for creating a standup entry.
/// </summary>
public class StandupEntryRequest
{
    public int ProjectId { get; set; }
    public int AgentId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int? TaskId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? DetailsMd { get; set; }
}
