namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for agent workload metrics and analytics.
/// Aggregates data from agent_mail_messages, agent_mail_inbox, and related tables.
/// </summary>
public interface IAgentWorkloadRepository
{
    /// <summary>
    /// Get workload metrics for a specific agent.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="agentId">Agent ID to get metrics for</param>
    /// <param name="periodDays">Number of days for period calculations</param>
    /// <param name="includeHistory">Whether to include daily breakdown</param>
    /// <returns>Aggregated workload metrics for the agent</returns>
    Task<AgentWorkloadMetricsData?> GetAgentMetricsAsync(
        int clientId,
        int agentId,
        int periodDays,
        bool includeHistory = false);

    /// <summary>
    /// Get workload metrics for all agents in a client.
    /// </summary>
    /// <param name="clientId">Client ID scope</param>
    /// <param name="periodDays">Number of days for period calculations</param>
    /// <returns>List of aggregated workload metrics for all agents</returns>
    Task<List<AgentWorkloadMetricsData>> GetAllAgentMetricsAsync(
        int clientId,
        int periodDays);

    /// <summary>
    /// Get message counts for an agent.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="agentId">Agent ID</param>
    /// <param name="since">Start date for counting</param>
    /// <param name="groupByDay">Whether to group by day</param>
    /// <returns>Message counts (sent/received)</returns>
    Task<AgentMessageCounts> GetMessageCountsAsync(
        int clientId,
        int agentId,
        DateTimeOffset since,
        bool groupByDay = false);

    /// <summary>
    /// Get task statistics for an agent.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="agentId">Agent ID</param>
    /// <param name="periodStart">Start of period for assigned/completed counts</param>
    /// <returns>Task statistics</returns>
    Task<AgentTaskStats> GetTaskStatsAsync(
        int clientId,
        int agentId,
        DateTimeOffset periodStart);

    /// <summary>
    /// Get response time metrics for an agent.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="agentId">Agent ID</param>
    /// <param name="since">Start date for calculating response times</param>
    /// <returns>Response time statistics (avg, median, p95)</returns>
    Task<AgentResponseTimeStats> GetResponseTimeStatsAsync(
        int clientId,
        int agentId,
        DateTimeOffset since);
}

/// <summary>
/// Complete workload metrics data from repository.
/// </summary>
public class AgentWorkloadMetricsData
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

    // Daily breakdown (optional)
    public List<DailyWorkloadMetrics>? DailyMetrics { get; set; }
}

/// <summary>
/// Daily metrics for historical tracking.
/// </summary>
public class DailyWorkloadMetrics
{
    public DateTime Date { get; set; }
    public int MessagesSent { get; set; }
    public int MessagesReceived { get; set; }
    public int TasksAssigned { get; set; }
    public int TasksCompleted { get; set; }
    public double AvgResponseMs { get; set; }
}

/// <summary>
/// Message counts for an agent.
/// </summary>
public class AgentMessageCounts
{
    public int Sent { get; set; }
    public int Received { get; set; }
    public int Total => Sent + Received;
    public List<DailyMessageCounts>? Daily { get; set; }
}

/// <summary>
/// Daily message counts breakdown.
/// </summary>
public class DailyMessageCounts
{
    public DateTime Date { get; set; }
    public int Sent { get; set; }
    public int Received { get; set; }
}

/// <summary>
/// Task statistics for an agent.
/// </summary>
public class AgentTaskStats
{
    public int OpenTasks { get; set; }
    public int HighPriorityOpen { get; set; }
    public int OverdueOpen { get; set; }
    public double? OldestOpenHours { get; set; }
    public int AssignedInPeriod { get; set; }
    public int CompletedInPeriod { get; set; }
    public int OverdueCompleted { get; set; }
}

/// <summary>
/// Response time statistics.
/// </summary>
public class AgentResponseTimeStats
{
    public double AvgMs { get; set; }
    public double MedianMs { get; set; }
    public double P95Ms { get; set; }
}
