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
    public const int MaxQuerySize = 10 * 1024;

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

        if (sql!.Length > MaxQuerySize)
        {
            throw new VibeQueryError(
                VibeErrorCodes.QueryTooLarge,
                "Query too large",
                "SQL query exceeds the maximum allowed size of 10KB");
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
