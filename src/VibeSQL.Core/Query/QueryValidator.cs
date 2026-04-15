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
    /// Maximum allowed SQL query length for data DML (256KB).
    /// Document INSERTs carry user content (resumes, profiles) that can be 50-100KB+.
    /// Previous 10KB limit blocked real payloads.
    /// </summary>
    public const int MaxQuerySize = 256 * 1024;

    /// <summary>
    /// Maximum allowed SQL query length for schema operations (512KB).
    /// Schema definitions contain full JSON with table/column definitions.
    /// </summary>
    public const int SchemaMaxQuerySize = 512 * 1024;

    /// <summary>
    /// Maximum characters to include in error previews when validation fails.
    /// </summary>
    private const int ErrorPreviewMaxChars = 80;

    private static readonly string[] ValidKeywords =
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "CREATE", "DROP", "ALTER", "TRUNCATE"
    };

    /// <summary>
    /// Validate a SQL query for basic requirements: non-empty, within the
    /// size limit, and starting with a recognized keyword.
    /// </summary>
    /// <remarks>
    /// Schema-write detection is intentionally narrow: only <c>INSERT INTO</c>
    /// and <c>UPDATE</c> targeting <c>collection_schemas</c> lift the limit to
    /// <see cref="SchemaMaxQuerySize"/>. <c>SELECT</c>, <c>DELETE</c>, and DDL
    /// statements targeting the schemas table fall through to the default
    /// <see cref="MaxQuerySize"/> because they do not match the
    /// <c>INSERT INTO</c> / <c>UPDATE</c> prefixes — DELETE is not a schema
    /// write and DDL payloads are small enough for the default budget.
    /// </remarks>
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

        var upperTrimmed = trimmed.ToUpperInvariant();

        // Schema WRITES get the 512KB allowance because schema rows contain
        // full JSON definitions. SELECT, DELETE, and DDL on collection_schemas
        // stay on the default 256KB limit.
        var isSchemaWrite =
            upperTrimmed.StartsWith("INSERT INTO COLLECTION_SCHEMAS", StringComparison.Ordinal) ||
            upperTrimmed.StartsWith("UPDATE COLLECTION_SCHEMAS", StringComparison.Ordinal);
        var limit = isSchemaWrite ? SchemaMaxQuerySize : MaxQuerySize;

        if (sql!.Length > limit)
        {
            var limitKb = limit / 1024;
            var preview = BuildPreview(sql);
            throw new VibeQueryError(
                VibeErrorCodes.QueryTooLarge,
                "Query too large",
                $"SQL query exceeds the maximum allowed size of {limitKb}KB. Starts with: {preview}");
        }

        var hasValidKeyword = ValidKeywords.Any(keyword => upperTrimmed.StartsWith(keyword));

        if (!hasValidKeyword)
        {
            var preview = BuildPreview(sql);
            throw new VibeQueryError(
                VibeErrorCodes.InvalidSQL,
                "Invalid SQL syntax",
                $"Query must start with a valid SQL keyword (SELECT, INSERT, UPDATE, DELETE, CREATE, DROP). Received: {preview}");
        }
    }

    // Pragmatic code-unit slicing with a high-surrogate guard: if the last
    // code unit of the slice is a high surrogate, shorten by one so the pair
    // is not split. See QueryValidatorTests.cs header for the Unicode
    // trade-off rationale (Option 2 vs full rune enumeration).
    private static string BuildPreview(string sql)
    {
        if (sql.Length <= ErrorPreviewMaxChars)
        {
            return sql;
        }

        var sliceLength = ErrorPreviewMaxChars;
        if (char.IsHighSurrogate(sql[sliceLength - 1]))
        {
            sliceLength -= 1;
        }
        return sql[..sliceLength] + "...";
    }
}
