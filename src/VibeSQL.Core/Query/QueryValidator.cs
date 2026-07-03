namespace VibeSQL.Core.Query;

/// <summary>
/// Validates SQL queries for basic requirements.
/// </summary>
public class QueryValidator : IQueryValidator
{
    /// <summary>
    /// Maximum allowed SQL query length for data DML (256KB).
    /// Document INSERTs carry user content that can be 50-100KB+.
    /// Schema DDL (INSERT/UPDATE on collection_schemas) uses <see cref="SchemaMaxQuerySize"/>.
    /// </summary>
    public const int MaxQuerySize = 262144;

    /// <summary>
    /// Maximum allowed SQL query length for schema operations (512KB).
    /// Schema definitions contain full JSON with table/column definitions.
    /// </summary>
    public const int SchemaMaxQuerySize = 524288;

    /// <summary>
    /// Valid SQL keywords that queries must start with.
    /// </summary>
    private static readonly string[] ValidKeywords =
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "CREATE", "DROP", "ALTER", "TRUNCATE"
    };

    public void Validate(string? sql)
    {
        var trimmed = sql?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new VibeQueryError(
                VibeErrorCodes.MissingRequiredField,
                "Missing required field",
                "The 'sql' field is required and cannot be empty");
        }

        var upperTrimmed = trimmed.ToUpperInvariant();
        var limit = upperTrimmed.StartsWith("INSERT INTO COLLECTION_SCHEMAS") ||
                    upperTrimmed.StartsWith("UPDATE COLLECTION_SCHEMAS")
            ? SchemaMaxQuerySize
            : MaxQuerySize;

        if (trimmed.Length > limit)
        {
            var limitKb = limit / 1024;
            var preview = trimmed.Length > 80 ? trimmed[..80] + "..." : trimmed;
            throw new VibeQueryError(
                VibeErrorCodes.QueryTooLarge,
                "Query too large",
                $"SQL query exceeds the maximum allowed size of {limitKb}KB ({trimmed.Length} chars). Starts with: {preview}");
        }

        if (!ValidKeywords.Any(keyword => upperTrimmed.StartsWith(keyword)))
        {
            var preview = trimmed.Length > 80 ? trimmed[..80] + "..." : trimmed;
            throw new VibeQueryError(
                VibeErrorCodes.InvalidSQL,
                "Invalid SQL syntax",
                $"Query must start with a valid SQL keyword (SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, ALTER, TRUNCATE). Received: {preview}");
        }
    }
}
