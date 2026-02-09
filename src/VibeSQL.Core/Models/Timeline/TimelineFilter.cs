namespace VibeSQL.Core.Models.Timeline;

/// <summary>
/// Filter criteria for querying timeline events.
/// </summary>
public class TimelineFilter
{
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
    public string? EventFilter { get; set; }
    public int? Limit { get; set; }
}
