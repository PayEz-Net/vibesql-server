namespace VibeSQL.Core.Query;

/// <summary>
/// Validates SQL queries for basic requirements
/// </summary>
public interface IQueryValidator
{
    void Validate(string sql);
}

public class QueryValidator : IQueryValidator
{
    /// <summary>
    /// Maximum allowed SQL query length for data DML (10KB).
    /// Schema DDL (INSERT/UPDATE on collection_schemas) uses SchemaMaxQuerySize.
    /// </summary>
    public const int MaxQuerySize = 10 * 1024;

    /// <summary>
    /// Maximum allowed SQL query length for schema operations (512KB).
    /// Schema definitions contain full JSON with table/column definitions.
    /// </summary>
    public const int SchemaMaxQuerySize = 512 * 1024;

    private static readonly string[] ValidKeywords =
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "CREATE", "DROP", "ALTER", "TRUNCATE"
    };

    public void Validate(string sql)
    {
        var trimmed = sql?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new VibeQueryError(
                VibeErrorCodes.MissingRequiredField,
                "Missing required field",
                "The 'sql' field is required and cannot be empty");
        }

        // Schema operations get a higher size limit
        var isSchemaOperation = sql!.Contains("collection_schemas", StringComparison.OrdinalIgnoreCase);
        var limit = isSchemaOperation ? SchemaMaxQuerySize : MaxQuerySize;

        if (sql.Length > limit)
        {
            var limitKb = limit / 1024;
            throw new VibeQueryError(
                VibeErrorCodes.QueryTooLarge,
                "Query too large",
                $"SQL query exceeds the maximum allowed size of {limitKb}KB");
        }

        var upperSql = trimmed.ToUpperInvariant();
        var hasValidKeyword = ValidKeywords.Any(keyword => upperSql.StartsWith(keyword));

        if (!hasValidKeyword)
        {
            throw new VibeQueryError(
                VibeErrorCodes.InvalidSQL,
                "Invalid SQL syntax",
                "Query must start with a valid SQL keyword (SELECT, INSERT, UPDATE, DELETE, CREATE, DROP)");
        }
    }
}
