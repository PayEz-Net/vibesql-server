namespace VibeSQL.Core.Query;

/// <summary>
/// Enforces query result limits and timeouts.
/// </summary>
public interface IQueryLimiter
{
    /// <summary>
    /// Maximum allowed result rows.
    /// </summary>
    int MaxResultRows { get; }

    /// <summary>
    /// Default query timeout in seconds.
    /// </summary>
    int DefaultTimeoutSeconds { get; }

    /// <summary>
    /// Checks if the row count exceeds the limit.
    /// </summary>
    /// <exception cref="VibeQueryError">Thrown if the limit is exceeded.</exception>
    void CheckRowLimit(int currentRowCount);

    /// <summary>
    /// Gets the query timeout based on tier.
    /// </summary>
    TimeSpan GetTimeout(string? tier = null);
}
