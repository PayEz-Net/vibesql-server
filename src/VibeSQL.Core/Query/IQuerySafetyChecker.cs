namespace VibeSQL.Core.Query;

/// <summary>
/// Enforces safety rules on SQL queries.
/// </summary>
public interface IQuerySafetyChecker
{
    /// <summary>
    /// Checks if a SQL query is safe to execute.
    /// </summary>
    /// <exception cref="VibeQueryError">Thrown if the query is unsafe.</exception>
    void CheckSafety(string? sql);
}
