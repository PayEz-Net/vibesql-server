using VibeSQL.Core.Models.Aggregate;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for aggregate operations on Vibe SQL documents.
/// </summary>
public interface IAggregateService
{
    /// <summary>
    /// Calculates the sum of a numeric field across documents.
    /// </summary>
    /// <param name="collection">Collection name</param>
    /// <param name="tableName">Table name</param>
    /// <param name="field">Field to sum</param>
    /// <param name="filters">Optional filters (field:value pairs)</param>
    /// <returns>Aggregate result with sum and count</returns>
    Task<AggregateResult> SumAsync(
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null);

    /// <summary>
    /// Counts documents matching the filters.
    /// </summary>
    /// <param name="collection">Collection name</param>
    /// <param name="tableName">Table name</param>
    /// <param name="filters">Optional filters (field:value pairs)</param>
    /// <returns>Aggregate result with count</returns>
    Task<AggregateResult> CountAsync(
        string collection,
        string tableName,
        Dictionary<string, string>? filters = null);

    /// <summary>
    /// Calculates the average of a numeric field across documents.
    /// </summary>
    /// <param name="collection">Collection name</param>
    /// <param name="tableName">Table name</param>
    /// <param name="field">Field to average</param>
    /// <param name="filters">Optional filters (field:value pairs)</param>
    /// <returns>Aggregate result with average and count</returns>
    Task<AggregateResult> AverageAsync(
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null);

    /// <summary>
    /// Finds the minimum value of a numeric field across documents.
    /// </summary>
    /// <param name="collection">Collection name</param>
    /// <param name="tableName">Table name</param>
    /// <param name="field">Field to find minimum</param>
    /// <param name="filters">Optional filters (field:value pairs)</param>
    /// <returns>Aggregate result with minimum value and count</returns>
    Task<AggregateResult> MinAsync(
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null);

    /// <summary>
    /// Finds the maximum value of a numeric field across documents.
    /// </summary>
    /// <param name="collection">Collection name</param>
    /// <param name="tableName">Table name</param>
    /// <param name="field">Field to find maximum</param>
    /// <param name="filters">Optional filters (field:value pairs)</param>
    /// <returns>Aggregate result with maximum value and count</returns>
    Task<AggregateResult> MaxAsync(
        string collection,
        string tableName,
        string field,
        Dictionary<string, string>? filters = null);
}
