namespace VibeSQL.Core.Models.Standup;

/// <summary>
/// Filter criteria for querying standup entries.
/// </summary>
public class StandupFilter
{
    public int? AgentId { get; set; }
    public string? EventType { get; set; }
    public int? TaskId { get; set; }
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
    public int? Limit { get; set; }
}
