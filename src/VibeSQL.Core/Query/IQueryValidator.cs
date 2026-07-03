namespace VibeSQL.Core.Query;

/// <summary>
/// Validates SQL queries for basic requirements.
/// </summary>
public interface IQueryValidator
{
    /// <summary>
    /// Validates a SQL query for basic requirements.
    /// </summary>
    /// <exception cref="VibeQueryError">Thrown if validation fails.</exception>
    void Validate(string? sql);
}
