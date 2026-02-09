namespace VibeSQL.Core.Models.Timeline;

/// <summary>
/// Request model for appending an event to a timeline.
/// </summary>
public class TimelineEventRequest
{
    public DateTime Timestamp { get; set; }
    public string Event { get; set; } = string.Empty;
    public object? Data { get; set; }
}
