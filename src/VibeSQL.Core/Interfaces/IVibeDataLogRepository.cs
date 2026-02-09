namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for Vibe data log operations.
/// Uses the Vibe document system (vibe.documents with collection='vibe_app', table='data_logs').
/// </summary>
public interface IVibeDataLogRepository
{
    /// <summary>
    /// Creates a new log document in vibe_app.data_logs.
    /// </summary>
    Task<int> CreateLogDocumentAsync(int clientId, Dictionary<string, object?> logData);

    /// <summary>
    /// Queries log documents with filtering and pagination.
    /// Returns document data as dictionaries.
    /// </summary>
    /// <param name="clientId">IDP client ID to filter by document ownership</param>
    /// <param name="vibeClientId">Optional vibe_client_id to also match in JSON data (for logs from drain)</param>
    Task<(List<Dictionary<string, object?>> Items, int Total)> QueryAsync(
        int clientId,
        string? vibeClientId = null,
        string? level = null,
        string? category = null,
        string? collection = null,
        string? errorCode = null,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        int page = 1,
        int limit = 100);

    /// <summary>
    /// Gets log statistics for a time period.
    /// </summary>
    /// <param name="clientId">IDP client ID to filter by document ownership</param>
    /// <param name="vibeClientId">Optional vibe_client_id to also match in JSON data</param>
    Task<LogStats> GetStatsAsync(int clientId, TimeSpan period, string? vibeClientId = null);

    /// <summary>
    /// Gets the log level setting for a client from vibe_app.client_log_settings.
    /// </summary>
    Task<string> GetLogLevelAsync(int clientId);

    /// <summary>
    /// Sets the log level for a client in vibe_app.client_log_settings.
    /// </summary>
    Task SetLogLevelAsync(int clientId, string level);

    /// <summary>
    /// Purges log documents older than the specified date for a given level.
    /// </summary>
    Task<int> PurgeLogsAsync(int clientId, DateTimeOffset olderThan, string? level = null);
}

/// <summary>
/// Log statistics for a time period.
/// </summary>
public class LogStats
{
    public TimeSpan Period { get; set; }
    public Dictionary<string, int> CountsByLevel { get; set; } = new();
    public Dictionary<string, int> CountsByCategory { get; set; } = new();
    public List<ErrorCodeCount> TopErrors { get; set; } = new();
}

/// <summary>
/// Error code occurrence count.
/// </summary>
public class ErrorCodeCount
{
    public string ErrorCode { get; set; } = string.Empty;
    public int Count { get; set; }
}
