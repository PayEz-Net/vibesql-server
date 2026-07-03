namespace VibeSQL.Core.Query;

/// <summary>
/// Executes SQL queries with validation, safety checks, and limits.
/// </summary>
public interface IQueryExecutor
{
    /// <summary>
    /// Executes a SQL query with the full validation pipeline.
    /// When <paramref name="clientId"/> is provided, the query runs on the RLS-enforced
    /// connection (VibeDbRls / vibe_rls_user) inside a transaction with
    /// <c>SET LOCAL app.client_id = {clientId}</c> so Postgres row-level security scopes
    /// tenant tables to that client.
    /// </summary>
    Task<QueryExecutionResult> ExecuteAsync(
        string sql,
        string? tier = null,
        int? clientId = null,
        CancellationToken cancellationToken = default);
}
