namespace VibeSQL.Core.Entities;

/// <summary>
/// Represents a standup entry from the vibe_agents.standup_entries table.
/// </summary>
public class StandupEntry
{
    public int EntryId { get; set; }
    public int ProjectId { get; set; }
    public int AgentId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int? TaskId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? DetailsMd { get; set; }
    public DateTime CreatedAt { get; set; }
}
