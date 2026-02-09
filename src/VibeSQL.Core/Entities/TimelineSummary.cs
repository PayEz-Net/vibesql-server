namespace VibeSQL.Core.Entities;

/// <summary>
/// Summary of timeline activity.
/// </summary>
public class TimelineSummary
{
    public DateTime? FirstEvent { get; set; }
    public DateTime? LastEvent { get; set; }
    public int EventCount { get; set; }
    public double? DurationDays { get; set; }
    public List<TimelineEvent> RecentEvents { get; set; } = new();
}
