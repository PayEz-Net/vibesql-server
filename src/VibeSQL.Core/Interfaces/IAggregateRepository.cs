namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for aggregate operations on Vibe SQL documents.
/// </summary>
public interface IAggregateRepository
{
    /// <summary>
    /// Calculates the sum of a numeric field across documents.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="collection">Collection name</param>
    /// <param name="tableName">Table name</param>
    /// <param name="field">Field to sum</param>
    /// <param name="filters">Optional filters (field:value pairs)</param>
    /// <returns>Sum result and count of documents</returns>
    Task<(decimal sum, int count)> SumAsync(
        int clientId,
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null);

    /// <summary>
    /// Counts documents matching the filters.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="collection">Collection name</param>
    /// <param name="tableName">Table name</param>
    /// <param name="filters">Optional filters (field:value pairs)</param>
    /// <returns>Count of matching documents</returns>
    Task<int> CountAsync(
        int clientId,
        string collection,
        string tableName,
        Dictionary<string, string>? filters = null);

    /// <summary>
    /// Calculates the average of a numeric field across documents.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="collection">Collection name</param>
    /// <param name="tableName">Table name</param>
    /// <param name="field">Field to average</param>
    /// <param name="filters">Optional filters (field:value pairs)</param>
    /// <returns>Average result and count of documents</returns>
    Task<(decimal average, int count)> AverageAsync(
        int clientId,
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null);

    /// <summary>
    /// Finds the minimum value of a numeric field across documents.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="collection">Collection name</param>
    /// <param name="tableName">Table name</param>
    /// <param name="field">Field to find minimum</param>
    /// <param name="filters">Optional filters (field:value pairs)</param>
    /// <returns>Minimum value and count of documents</returns>
    Task<(decimal min, int count)> MinAsync(
        int clientId,
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null);

    /// <summary>
    /// Finds the maximum value of a numeric field across documents.
    /// </summary>
    /// <param name="clientId">Client ID</param>
    /// <param name="collection">Collection name</param>
    /// <param name="tableName">Table name</param>
    /// <param name="field">Field to find maximum</param>
    /// <param name="filters">Optional filters (field:value pairs)</param>
    /// <returns>Maximum value and count of documents</returns>
    Task<(decimal max, int count)> MaxAsync(
        int clientId,
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null);
}
