using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for agent workload metrics and analytics.
/// Aggregates data from agent_mail_messages, agent_mail_inbox, and related tables.
/// </summary>
public class AgentWorkloadRepository : IAgentWorkloadRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AgentWorkloadRepository> _logger;

    private const string CollectionName = "agent_mail";
    private const string MessagesTable = "agent_mail_messages";
    private const string InboxTable = "agent_mail_inbox";
    private const string AgentsTable = "agent_mail_agents";
    private const string TasksTable = "agent_mail_tasks";

    public AgentWorkloadRepository(VibeDbContext context, ILogger<AgentWorkloadRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AgentWorkloadMetricsData?> GetAgentMetricsAsync(
        int clientId,
        int agentId,
        int periodDays,
        bool includeHistory = false)
    {
        var now = DateTimeOffset.UtcNow;
        var todayStart = now.Date;
        var periodStart = now.AddDays(-periodDays);

        // Get agent info
        var agentDoc = await GetAgentDocumentAsync(clientId, agentId);
        if (agentDoc == null)
        {
            _logger.LogWarning("WORKLOAD_AGENT_NOT_FOUND: AgentId={AgentId}, ClientId={ClientId}", agentId, clientId);
            return null;
        }

        var agentData = TryDeserialize<AgentData>(agentDoc.Data);
        if (agentData == null) return null;

        // Get message metrics
        var messageCounts = await GetMessageCountsAsync(clientId, agentId, periodStart, includeHistory);

        // Get task metrics
        var taskStats = await GetTaskStatsAsync(clientId, agentId, periodStart);

        // Get response time metrics
        var responseStats = await GetResponseTimeStatsAsync(clientId, agentId, periodStart);

        var metrics = new AgentWorkloadMetricsData
        {
            AgentId = agentId,
            AgentName = agentData.Name ?? "",
            AgentDisplayName = agentData.DisplayName ?? agentData.Name ?? "",
            MaxConcurrentTasks = agentData.MaxConcurrentTasks ?? 10,

            // Message metrics
            MessagesSentToday = messageCounts.Daily?.FirstOrDefault(d => d.Date.Date == todayStart)?.Sent ?? 0,
            MessagesReceivedToday = messageCounts.Daily?.FirstOrDefault(d => d.Date.Date == todayStart)?.Received ?? 0,
            MessagesSentPeriod = messageCounts.Sent,
            MessagesReceivedPeriod = messageCounts.Received,

            // Task metrics
            OpenTasks = taskStats.OpenTasks,
            HighPriorityOpenTasks = taskStats.HighPriorityOpen,
            OverdueOpenTasks = taskStats.OverdueOpen,
            OldestOpenTaskHours = taskStats.OldestOpenHours,
            TasksAssignedPeriod = taskStats.AssignedInPeriod,
            TasksCompletedPeriod = taskStats.CompletedInPeriod,
            OverdueCompletedPeriod = taskStats.OverdueCompleted,

            // Response time metrics
            AvgResponseTimeMs = responseStats.AvgMs,
            MedianResponseTimeMs = responseStats.MedianMs,
            P95ResponseTimeMs = responseStats.P95Ms
        };

        // Include daily history if requested
        if (includeHistory && messageCounts.Daily != null)
        {
            metrics.DailyMetrics = messageCounts.Daily.Select(d => new DailyWorkloadMetrics
            {
                Date = d.Date,
                MessagesSent = d.Sent,
                MessagesReceived = d.Received,
                TasksAssigned = 0, // TODO: Aggregate from task history
                TasksCompleted = 0,
                AvgResponseMs = 0 // TODO: Calculate per-day response times
            }).ToList();
        }

        return metrics;
    }

    public async Task<List<AgentWorkloadMetricsData>> GetAllAgentMetricsAsync(int clientId, int periodDays)
    {
        var agents = await GetAllAgentsAsync(clientId);
        var metrics = new List<AgentWorkloadMetricsData>();

        foreach (var agentDoc in agents)
        {
            var agentData = TryDeserialize<AgentData>(agentDoc.Data);
            if (agentData == null || agentData.Id == 0) continue;

            var agentMetrics = await GetAgentMetricsAsync(clientId, agentData.Id, periodDays, false);
            if (agentMetrics != null)
            {
                metrics.Add(agentMetrics);
            }
        }

        return metrics;
    }

    public async Task<AgentMessageCounts> GetMessageCountsAsync(
        int clientId,
        int agentId,
        DateTimeOffset since,
        bool groupByDay = false)
    {
        // Get all messages for the period
        var messages = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MessagesTable
                     && d.DeletedAt == null
                     && d.CreatedAt >= since)
            .ToListAsync();

        // Get inbox entries to count received messages
        var inboxEntries = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == InboxTable
                     && d.DeletedAt == null
                     && d.CreatedAt >= since)
            .ToListAsync();

        // Count sent messages (where from_agent_id matches)
        var sentMessages = messages.Where(d =>
        {
            var data = TryDeserialize<MessageData>(d.Data);
            return data?.FromAgentId == agentId;
        }).ToList();

        // Count received messages (inbox entries for this agent)
        var receivedEntries = inboxEntries.Where(d =>
        {
            var data = TryDeserialize<InboxData>(d.Data);
            return data?.AgentId == agentId;
        }).ToList();

        var result = new AgentMessageCounts
        {
            Sent = sentMessages.Count,
            Received = receivedEntries.Count
        };

        if (groupByDay)
        {
            var dailyCounts = new Dictionary<DateTime, DailyMessageCounts>();

            foreach (var msg in sentMessages)
            {
                var date = msg.CreatedAt.Date;
                if (!dailyCounts.ContainsKey(date))
                    dailyCounts[date] = new DailyMessageCounts { Date = date };
                dailyCounts[date].Sent++;
            }

            foreach (var entry in receivedEntries)
            {
                var date = entry.CreatedAt.Date;
                if (!dailyCounts.ContainsKey(date))
                    dailyCounts[date] = new DailyMessageCounts { Date = date };
                dailyCounts[date].Received++;
            }

            result.Daily = dailyCounts.Values.OrderBy(d => d.Date).ToList();
        }

        return result;
    }

    public async Task<AgentTaskStats> GetTaskStatsAsync(
        int clientId,
        int agentId,
        DateTimeOffset periodStart)
    {
        var now = DateTimeOffset.UtcNow;

        // Get all tasks for this agent
        var taskDocs = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == TasksTable
                     && d.DeletedAt == null)
            .ToListAsync();

        var tasks = taskDocs
            .Select(d => TryDeserialize<TaskData>(d.Data))
            .Where(t => t != null && t.AgentId == agentId)
            .ToList();

        // Calculate open task metrics
        var openTasks = tasks.Where(t =>
            t!.Status == "assigned" || t.Status == "in_progress").ToList();

        var openTasksCount = openTasks.Count;
        var highPriorityCount = openTasks.Count(t =>
            t!.Priority == "high" || t!.Priority == "critical");

        var overdueOpenCount = openTasks.Count(t =>
        {
            if (string.IsNullOrEmpty(t!.DueAt)) return false;
            return DateTimeOffset.TryParse(t.DueAt, out var due) && due < now;
        });

        // Calculate oldest open task age
        double? oldestHours = null;
        var oldestTask = openTasks
            .Where(t => !string.IsNullOrEmpty(t!.AssignedAt))
            .Select(t => DateTimeOffset.TryParse(t!.AssignedAt, out var assigned) ? assigned : (DateTimeOffset?)null)
            .Where(d => d.HasValue)
            .Min();

        if (oldestTask.HasValue)
        {
            oldestHours = (now - oldestTask.Value).TotalHours;
        }

        // Calculate period metrics
        var assignedInPeriod = tasks.Count(t =>
        {
            if (string.IsNullOrEmpty(t!.AssignedAt)) return false;
            return DateTimeOffset.TryParse(t.AssignedAt, out var assigned) && assigned >= periodStart;
        });

        var completedInPeriod = tasks.Count(t =>
        {
            if (t!.Status != "completed" || string.IsNullOrEmpty(t.CompletedAt)) return false;
            return DateTimeOffset.TryParse(t.CompletedAt, out var completed) && completed >= periodStart;
        });

        var overdueCompleted = tasks.Count(t =>
        {
            if (t!.Status != "completed") return false;
            if (string.IsNullOrEmpty(t.CompletedAt) || string.IsNullOrEmpty(t.DueAt)) return false;
            if (!DateTimeOffset.TryParse(t.CompletedAt, out var completed)) return false;
            if (!DateTimeOffset.TryParse(t.DueAt, out var due)) return false;
            return completed > due && completed >= periodStart;
        });

        return new AgentTaskStats
        {
            OpenTasks = openTasksCount,
            HighPriorityOpen = highPriorityCount,
            OverdueOpen = overdueOpenCount,
            OldestOpenHours = oldestHours,
            AssignedInPeriod = assignedInPeriod,
            CompletedInPeriod = completedInPeriod,
            OverdueCompleted = overdueCompleted
        };
    }

    public async Task<AgentResponseTimeStats> GetResponseTimeStatsAsync(
        int clientId,
        int agentId,
        DateTimeOffset since)
    {
        // Get inbox entries (received messages) for this agent
        var inboxDocs = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == InboxTable
                     && d.DeletedAt == null
                     && d.CreatedAt >= since)
            .ToListAsync();

        // Get all messages for this agent (sent replies)
        var messageDocs = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MessagesTable
                     && d.DeletedAt == null
                     && d.CreatedAt >= since)
            .ToListAsync();

        // Parse inbox entries to get received message timestamps grouped by thread
        var receivedByThread = new Dictionary<string, List<DateTimeOffset>>();
        foreach (var doc in inboxDocs)
        {
            var data = TryDeserialize<InboxData>(doc.Data);
            if (data?.AgentId != agentId) continue;

            // Get the associated message to find thread_id
            var msgDoc = messageDocs.FirstOrDefault(m =>
            {
                var msgData = TryDeserialize<MessageData>(m.Data);
                return msgData?.Id == data.MessageId;
            });

            if (msgDoc != null)
            {
                var msgData = TryDeserialize<MessageData>(msgDoc.Data);
                if (msgData?.ThreadId != null)
                {
                    if (!receivedByThread.ContainsKey(msgData.ThreadId))
                        receivedByThread[msgData.ThreadId] = new List<DateTimeOffset>();
                    receivedByThread[msgData.ThreadId].Add(doc.CreatedAt);
                }
            }
        }

        // Parse sent messages to get response timestamps by thread
        var sentByThread = new Dictionary<string, List<DateTimeOffset>>();
        foreach (var doc in messageDocs)
        {
            var data = TryDeserialize<MessageData>(doc.Data);
            if (data?.FromAgentId != agentId) continue;
            if (string.IsNullOrEmpty(data.ThreadId)) continue;

            if (!sentByThread.ContainsKey(data.ThreadId))
                sentByThread[data.ThreadId] = new List<DateTimeOffset>();
            sentByThread[data.ThreadId].Add(doc.CreatedAt);
        }

        // Calculate response times
        var responseTimes = new List<double>();
        foreach (var threadId in receivedByThread.Keys)
        {
            if (!sentByThread.ContainsKey(threadId)) continue;

            var received = receivedByThread[threadId].OrderBy(t => t).ToList();
            var sent = sentByThread[threadId].OrderBy(t => t).ToList();

            foreach (var recvTime in received)
            {
                // Find first response after this received message
                var response = sent.FirstOrDefault(s => s > recvTime);
                if (response != default)
                {
                    var responseMs = (response - recvTime).TotalMilliseconds;
                    if (responseMs > 0 && responseMs < 86400000) // Cap at 24 hours
                    {
                        responseTimes.Add(responseMs);
                    }
                }
            }
        }

        if (responseTimes.Count == 0)
        {
            return new AgentResponseTimeStats
            {
                AvgMs = 0,
                MedianMs = 0,
                P95Ms = 0
            };
        }

        responseTimes.Sort();
        var avg = responseTimes.Average();
        var median = responseTimes[responseTimes.Count / 2];
        var p95Index = (int)Math.Ceiling(responseTimes.Count * 0.95) - 1;
        var p95 = responseTimes[Math.Max(0, Math.Min(p95Index, responseTimes.Count - 1))];

        return new AgentResponseTimeStats
        {
            AvgMs = avg,
            MedianMs = median,
            P95Ms = p95
        };
    }

    #region Helper Methods

    private async Task<VibeSQL.Core.Entities.VibeDocument?> GetAgentDocumentAsync(int clientId, int agentId)
    {
        var agents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == AgentsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return agents.FirstOrDefault(d =>
        {
            var data = TryDeserialize<AgentData>(d.Data);
            return data?.Id == agentId;
        });
    }

    private async Task<List<VibeSQL.Core.Entities.VibeDocument>> GetAllAgentsAsync(int clientId)
    {
        return await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == AgentsTable
                     && d.DeletedAt == null)
            .ToListAsync();
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    #endregion

    #region Internal Data Models

    private class AgentData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("max_concurrent_tasks")]
        public int? MaxConcurrentTasks { get; set; }
    }

    private class MessageData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("thread_id")]
        public string? ThreadId { get; set; }

        [JsonPropertyName("from_agent_id")]
        public int FromAgentId { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }
    }

    private class InboxData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("message_id")]
        public int MessageId { get; set; }

        [JsonPropertyName("agent_id")]
        public int AgentId { get; set; }

        [JsonPropertyName("read_at")]
        public string? ReadAt { get; set; }
    }

    private class TaskData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("agent_id")]
        public int AgentId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("priority")]
        public string? Priority { get; set; }

        [JsonPropertyName("assigned_at")]
        public string? AssignedAt { get; set; }

        [JsonPropertyName("due_at")]
        public string? DueAt { get; set; }

        [JsonPropertyName("completed_at")]
        public string? CompletedAt { get; set; }
    }

    #endregion
}
