namespace VibeSQL.Core.Entities;

/// <summary>
/// Represents a single event in a timeline.
/// </summary>
public class TimelineEvent
{
    public DateTime Timestamp { get; set; }
    public string Event { get; set; } = string.Empty;
    public object? Data { get; set; }
}
