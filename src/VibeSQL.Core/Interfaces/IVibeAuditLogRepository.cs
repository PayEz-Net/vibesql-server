using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for Vibe audit log operations.
/// </summary>
public interface IVibeAuditLogRepository
{
    /// <summary>
    /// Log an audit event
    /// </summary>
    Task LogAsync(AuditLog auditLog);

    /// <summary>
    /// Query audit logs with filtering and pagination
    /// </summary>
    Task<(List<AuditLog> Logs, int TotalCount)> QueryAsync(
        int clientId,
        int? adminUserId = null,
        string? category = null,
        string? action = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 50,
        string? targetId = null);

    /// <summary>
    /// Get audit logs for a specific user
    /// </summary>
    Task<List<AuditLog>> GetByUserAsync(int clientId, int userId, int limit = 100);

    /// <summary>
    /// Get feature usage summary by category/action
    /// </summary>
    Task<Dictionary<string, int>> GetFeatureUsageSummaryAsync(int clientId, DateTime? since = null);

    /// <summary>
    /// Delete audit logs older than a date (for GDPR/retention)
    /// </summary>
    Task<int> DeleteOlderThanAsync(int clientId, DateTime beforeDate);

    /// <summary>
    /// Anonymize audit logs for a user (GDPR)
    /// </summary>
    Task<int> AnonymizeUserLogsAsync(int clientId, int userId);

    /// <summary>
    /// Get summary statistics using DB-level aggregation (efficient, no full data load).
    /// Returns counts by category, action, admin, and success/failure breakdown.
    /// </summary>
    Task<AuditSummaryStatsDto> GetSummaryStatsAsync(int clientId, DateTime? startDate, DateTime? endDate);
}

/// <summary>
/// Aggregated audit stats from DB (no entity loading)
/// </summary>
public class AuditSummaryStatsDto
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public Dictionary<string, int> CountsByCategory { get; set; } = new();
    public Dictionary<string, int> CountsByAction { get; set; } = new();
    public List<AdminAuditCount> CountsByAdmin { get; set; } = new();
}

/// <summary>
/// Admin audit count entry
/// </summary>
public class AdminAuditCount
{
    public int AdminUserId { get; set; }
    public string? AdminEmail { get; set; }
    public int Count { get; set; }
}
