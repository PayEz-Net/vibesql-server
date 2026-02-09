using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for Vibe audit log operations.
/// </summary>
public class VibeAuditLogRepository : IVibeAuditLogRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<VibeAuditLogRepository> _logger;

    public VibeAuditLogRepository(VibeDbContext context, ILogger<VibeAuditLogRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(AuditLog auditLog)
    {
        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        _logger.LogDebug("AUDIT_LOG: {Category}/{Action} by user {UserId} on {TargetType}:{TargetId}",
            auditLog.Category, auditLog.Action, auditLog.AdminUserId, auditLog.TargetType, auditLog.TargetId);
    }

    public async Task<(List<AuditLog> Logs, int TotalCount)> QueryAsync(
        int clientId,
        int? adminUserId = null,
        string? category = null,
        string? action = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 50,
        string? targetId = null)
    {
        var query = _context.AuditLogs
            .Where(a => a.ClientId == clientId)
            .AsQueryable();

        if (adminUserId.HasValue)
            query = query.Where(a => a.AdminUserId == adminUserId.Value);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(a => a.Category == category);

        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);

        if (!string.IsNullOrEmpty(targetId))
            query = query.Where(a => a.TargetId == targetId);

        if (startDate.HasValue)
            query = query.Where(a => a.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.CreatedAt <= endDate.Value);

        var totalCount = await query.CountAsync();

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (logs, totalCount);
    }

    public async Task<List<AuditLog>> GetByUserAsync(int clientId, int userId, int limit = 100)
    {
        return await _context.AuditLogs
            .Where(a => a.ClientId == clientId && a.AdminUserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetFeatureUsageSummaryAsync(int clientId, DateTime? since = null)
    {
        var query = _context.AuditLogs
            .Where(a => a.ClientId == clientId);

        if (since.HasValue)
            query = query.Where(a => a.CreatedAt >= since.Value);

        var summary = await query
            .GroupBy(a => new { a.Category, a.Action })
            .Select(g => new { Key = $"{g.Key.Category}/{g.Key.Action}", Count = g.Count() })
            .ToListAsync();

        return summary.ToDictionary(x => x.Key, x => x.Count);
    }

    public async Task<int> DeleteOlderThanAsync(int clientId, DateTime beforeDate)
    {
        var logs = await _context.AuditLogs
            .Where(a => a.ClientId == clientId && a.CreatedAt < beforeDate)
            .ToListAsync();

        if (logs.Count == 0) return 0;

        _context.AuditLogs.RemoveRange(logs);
        await _context.SaveChangesAsync();

        _logger.LogInformation("AUDIT_LOG_CLEANUP: Deleted {Count} logs older than {Date} for client {ClientId}",
            logs.Count, beforeDate, clientId);

        return logs.Count;
    }

    public async Task<int> AnonymizeUserLogsAsync(int clientId, int userId)
    {
        var logs = await _context.AuditLogs
            .Where(a => a.ClientId == clientId && a.AdminUserId == userId)
            .ToListAsync();

        foreach (var log in logs)
        {
            log.AdminEmail = "[REDACTED]";
            log.IpAddress = "[REDACTED]";
            log.UserAgent = "[REDACTED]";
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("AUDIT_LOG_ANONYMIZED: Anonymized {Count} logs for user {UserId}, client {ClientId}",
            logs.Count, userId, clientId);

        return logs.Count;
    }

    public async Task<AuditSummaryStatsDto> GetSummaryStatsAsync(int clientId, DateTime? startDate, DateTime? endDate)
    {
        var query = _context.AuditLogs
            .Where(a => a.ClientId == clientId)
            .AsQueryable();

        if (startDate.HasValue)
            query = query.Where(a => a.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.CreatedAt <= endDate.Value);

        // Get total and success/failure counts in one query
        var totalCount = await query.CountAsync();
        var successCount = await query.CountAsync(a => a.IsSuccess);

        // Group by category (DB-level aggregation)
        var byCategory = await query
            .GroupBy(a => a.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync();

        // Group by category/action (DB-level aggregation)
        var byAction = await query
            .GroupBy(a => new { a.Category, a.Action })
            .Select(g => new { Key = $"{g.Key.Category}/{g.Key.Action}", Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        // Group by admin (DB-level aggregation)
        var byAdmin = await query
            .GroupBy(a => new { a.AdminUserId, a.AdminEmail })
            .Select(g => new { g.Key.AdminUserId, g.Key.AdminEmail, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        return new AuditSummaryStatsDto
        {
            TotalCount = totalCount,
            SuccessCount = successCount,
            FailureCount = totalCount - successCount,
            CountsByCategory = byCategory.ToDictionary(x => x.Category, x => x.Count),
            CountsByAction = byAction.ToDictionary(x => x.Key, x => x.Count),
            CountsByAdmin = byAdmin.Select(x => new AdminAuditCount
            {
                AdminUserId = x.AdminUserId,
                AdminEmail = x.AdminEmail,
                Count = x.Count
            }).ToList()
        };
    }
}
