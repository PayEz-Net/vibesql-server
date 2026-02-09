using System.Text.Json.Serialization;

namespace VibeSQL.Core.DTOs.AgentMail;

#region Request DTOs

/// <summary>
/// Query parameters for workload endpoint.
/// </summary>
public class AgentWorkloadQuery
{
    [JsonPropertyName("agent")]
    public string? Agent { get; set; }

    [JsonPropertyName("period")]
    public string Period { get; set; } = "7d";

    [JsonPropertyName("include_history")]
    public bool IncludeHistory { get; set; } = false;

    [JsonPropertyName("include_factors")]
    public bool IncludeFactors { get; set; } = true;
}

#endregion

#region Result DTOs

/// <summary>
/// Result from workload calculation for a single agent.
/// </summary>
public class AgentWorkloadResult
{
    public bool Success { get; set; }
    public AgentWorkloadData? Data { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Result from workload calculation for all agents (summary mode).
/// </summary>
public class AgentWorkloadSummaryResult
{
    public bool Success { get; set; }
    public AgentWorkloadSummaryData? Data { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
}

#endregion

#region Data DTOs

/// <summary>
/// Full workload data for a single agent.
/// </summary>
public class AgentWorkloadData
{
    [JsonPropertyName("agent_id")]
    public string AgentId { get; set; } = "";

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = "";

    [JsonPropertyName("generated_at")]
    public string GeneratedAt { get; set; } = "";

    [JsonPropertyName("period")]
    public string Period { get; set; } = "";

    [JsonPropertyName("workload")]
    public WorkloadScoreDto Workload { get; set; } = new();

    [JsonPropertyName("messages")]
    public WorkloadMessagesDto Messages { get; set; } = new();

    [JsonPropertyName("tasks")]
    public WorkloadTasksDto Tasks { get; set; } = new();

    [JsonPropertyName("response_time")]
    public WorkloadResponseTimeDto ResponseTime { get; set; } = new();

    [JsonPropertyName("history")]
    public List<WorkloadHistoryItemDto>? History { get; set; }
}

/// <summary>
/// Summary data for all agents.
/// </summary>
public class AgentWorkloadSummaryData
{
    [JsonPropertyName("summary")]
    public WorkloadTeamSummaryDto Summary { get; set; } = new();

    [JsonPropertyName("agents")]
    public List<AgentWorkloadBriefDto> Agents { get; set; } = new();
}

/// <summary>
/// Workload score with status and factor breakdown.
/// </summary>
public class WorkloadScoreDto
{
    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "available";

    [JsonPropertyName("status_emoji")]
    public string StatusEmoji { get; set; } = "🟢";

    [JsonPropertyName("factors")]
    public WorkloadFactorsDto? Factors { get; set; }
}

/// <summary>
/// Breakdown of factors contributing to workload score.
/// </summary>
public class WorkloadFactorsDto
{
    [JsonPropertyName("task_factor")]
    public double TaskFactor { get; set; }

    [JsonPropertyName("message_factor")]
    public double MessageFactor { get; set; }

    [JsonPropertyName("response_factor")]
    public double ResponseFactor { get; set; }

    [JsonPropertyName("overdue_factor")]
    public double OverdueFactor { get; set; }
}

/// <summary>
/// Message statistics for workload calculation.
/// </summary>
public class WorkloadMessagesDto
{
    [JsonPropertyName("period_total")]
    public MessageCountsDto PeriodTotal { get; set; } = new();

    [JsonPropertyName("daily_average")]
    public MessageCountsDto DailyAverage { get; set; } = new();

    [JsonPropertyName("today")]
    public MessageCountsDto Today { get; set; } = new();
}

/// <summary>
/// Message counts (sent/received/total).
/// </summary>
public class MessageCountsDto
{
    [JsonPropertyName("sent")]
    public double Sent { get; set; }

    [JsonPropertyName("received")]
    public double Received { get; set; }

    [JsonPropertyName("total")]
    public double Total { get; set; }
}

/// <summary>
/// Task statistics for workload calculation.
/// </summary>
public class WorkloadTasksDto
{
    [JsonPropertyName("open")]
    public OpenTasksDto Open { get; set; } = new();

    [JsonPropertyName("period")]
    public PeriodTasksDto Period { get; set; } = new();
}

/// <summary>
/// Current open task statistics.
/// </summary>
public class OpenTasksDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("high_priority")]
    public int HighPriority { get; set; }

    [JsonPropertyName("overdue")]
    public int Overdue { get; set; }

    [JsonPropertyName("oldest_hours")]
    public double OldestHours { get; set; }
}

/// <summary>
/// Task statistics for the query period.
/// </summary>
public class PeriodTasksDto
{
    [JsonPropertyName("assigned")]
    public int Assigned { get; set; }

    [JsonPropertyName("completed")]
    public int Completed { get; set; }

    [JsonPropertyName("completion_rate")]
    public double CompletionRate { get; set; }

    [JsonPropertyName("overdue_completed")]
    public int OverdueCompleted { get; set; }
}

/// <summary>
/// Response time statistics.
/// </summary>
public class WorkloadResponseTimeDto
{
    [JsonPropertyName("avg_ms")]
    public double AvgMs { get; set; }

    [JsonPropertyName("avg_formatted")]
    public string AvgFormatted { get; set; } = "";

    [JsonPropertyName("median_ms")]
    public double MedianMs { get; set; }

    [JsonPropertyName("median_formatted")]
    public string MedianFormatted { get; set; } = "";

    [JsonPropertyName("p95_ms")]
    public double P95Ms { get; set; }

    [JsonPropertyName("p95_formatted")]
    public string P95Formatted { get; set; } = "";
}

/// <summary>
/// Historical workload data point.
/// </summary>
public class WorkloadHistoryItemDto
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("messages_sent")]
    public int MessagesSent { get; set; }

    [JsonPropertyName("messages_received")]
    public int MessagesReceived { get; set; }

    [JsonPropertyName("tasks_assigned")]
    public int TasksAssigned { get; set; }

    [JsonPropertyName("tasks_completed")]
    public int TasksCompleted { get; set; }

    [JsonPropertyName("avg_response_ms")]
    public double AvgResponseMs { get; set; }

    [JsonPropertyName("workload_score")]
    public int WorkloadScore { get; set; }
}

/// <summary>
/// Team-level workload summary.
/// </summary>
public class WorkloadTeamSummaryDto
{
    [JsonPropertyName("total_agents")]
    public int TotalAgents { get; set; }

    [JsonPropertyName("by_status")]
    public WorkloadStatusCountsDto ByStatus { get; set; } = new();

    [JsonPropertyName("avg_workload_score")]
    public int AvgWorkloadScore { get; set; }
}

/// <summary>
/// Count of agents by workload status.
/// </summary>
public class WorkloadStatusCountsDto
{
    [JsonPropertyName("available")]
    public int Available { get; set; }

    [JsonPropertyName("busy")]
    public int Busy { get; set; }

    [JsonPropertyName("overloaded")]
    public int Overloaded { get; set; }
}

/// <summary>
/// Brief workload info for agent list.
/// </summary>
public class AgentWorkloadBriefDto
{
    [JsonPropertyName("agent_id")]
    public string AgentId { get; set; } = "";

    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = "";

    [JsonPropertyName("workload_score")]
    public int WorkloadScore { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "available";

    [JsonPropertyName("status_emoji")]
    public string StatusEmoji { get; set; } = "🟢";

    [JsonPropertyName("open_tasks")]
    public int OpenTasks { get; set; }

    [JsonPropertyName("messages_today")]
    public int MessagesToday { get; set; }
}

#endregion

#region Internal Data Models

/// <summary>
/// Raw metrics from repository for workload calculation.
/// </summary>
public class AgentWorkloadMetrics
{
    public int AgentId { get; set; }
    public string AgentName { get; set; } = "";
    public string AgentDisplayName { get; set; } = "";
    public int MaxConcurrentTasks { get; set; } = 10;

    // Message metrics
    public int MessagesSentToday { get; set; }
    public int MessagesReceivedToday { get; set; }
    public int MessagesSentPeriod { get; set; }
    public int MessagesReceivedPeriod { get; set; }

    // Task metrics
    public int OpenTasks { get; set; }
    public int HighPriorityOpenTasks { get; set; }
    public int OverdueOpenTasks { get; set; }
    public double? OldestOpenTaskHours { get; set; }
    public int TasksAssignedPeriod { get; set; }
    public int TasksCompletedPeriod { get; set; }
    public int OverdueCompletedPeriod { get; set; }

    // Response time metrics (in milliseconds)
    public double AvgResponseTimeMs { get; set; }
    public double MedianResponseTimeMs { get; set; }
    public double P95ResponseTimeMs { get; set; }

    // Daily breakdown (for history)
    public List<DailyMetrics>? DailyMetrics { get; set; }
}

/// <summary>
/// Daily metrics for historical tracking.
/// </summary>
public class DailyMetrics
{
    public DateTime Date { get; set; }
    public int MessagesSent { get; set; }
    public int MessagesReceived { get; set; }
    public int TasksAssigned { get; set; }
    public int TasksCompleted { get; set; }
    public double AvgResponseMs { get; set; }
}

/// <summary>
/// Configuration for workload scoring algorithm.
/// </summary>
public class WorkloadScoringConfig
{
    public int TaskCapacity { get; set; } = 10;
    public int BaselineMessagesPerDay { get; set; } = 50;
    public int TargetResponseMs { get; set; } = 60000; // 1 minute
    public double WeightTasks { get; set; } = 0.40;
    public double WeightMessages { get; set; } = 0.25;
    public double WeightResponse { get; set; } = 0.20;
    public double WeightOverdue { get; set; } = 0.15;
}

#endregion
