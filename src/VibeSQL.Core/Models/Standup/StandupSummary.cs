using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Models.Standup;

/// <summary>
/// Summary of standup activity for a project over a time period.
/// </summary>
public class StandupSummary
{
    public int ProjectId { get; set; }
    public DateTime Since { get; set; }
    public DateTime Until { get; set; }
    public int TotalEntries { get; set; }
    public int TasksStarted { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksBlocked { get; set; }
    public int ReviewsRequested { get; set; }
    public int ReviewsPassed { get; set; }
    public int ReviewsFailed { get; set; }
    public int MilestonesDone { get; set; }
    public List<StandupEntry> RecentEntries { get; set; } = new();
}
