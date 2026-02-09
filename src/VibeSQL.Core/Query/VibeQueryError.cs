namespace VibeSQL.Core.Query;

/// <summary>
/// VibeSQL error codes
/// </summary>
public static class VibeErrorCodes
{
    public const string InvalidSQL = "INVALID_SQL";
    public const string MissingRequiredField = "MISSING_REQUIRED_FIELD";
    public const string UnsafeQuery = "UNSAFE_QUERY";
    public const string QueryTimeout = "QUERY_TIMEOUT";
    public const string QueryTooLarge = "QUERY_TOO_LARGE";
    public const string ResultTooLarge = "RESULT_TOO_LARGE";
    public const string DocumentTooLarge = "DOCUMENT_TOO_LARGE";
    public const string InternalError = "INTERNAL_ERROR";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
    public const string DatabaseUnavailable = "DATABASE_UNAVAILABLE";
    public const string Unauthorized = "UNAUTHORIZED";
}

/// <summary>
/// VibeSQL error with code, message, and detail
/// </summary>
public class VibeQueryError : Exception
{
    public string Code { get; }
    public string Detail { get; }
    public int HttpStatusCode { get; }

    public VibeQueryError(string code, string message, string detail = "")
        : base(message)
    {
        Code = code;
        Detail = detail;
        HttpStatusCode = GetHttpStatusCode(code);
    }

    private static int GetHttpStatusCode(string errorCode) => errorCode switch
    {
        VibeErrorCodes.InvalidSQL => 400,
        VibeErrorCodes.MissingRequiredField => 400,
        VibeErrorCodes.UnsafeQuery => 400,
        VibeErrorCodes.QueryTimeout => 408,
        VibeErrorCodes.QueryTooLarge => 413,
        VibeErrorCodes.ResultTooLarge => 413,
        VibeErrorCodes.DocumentTooLarge => 413,
        VibeErrorCodes.Unauthorized => 401,
        VibeErrorCodes.InternalError => 500,
        VibeErrorCodes.ServiceUnavailable => 503,
        VibeErrorCodes.DatabaseUnavailable => 503,
        _ => 500
    };

    public object ToResponse() => new
    {
        success = false,
        error = new
        {
            code = Code,
            message = Message,
            detail = string.IsNullOrEmpty(Detail) ? null : Detail
        }
    };
}

/// <summary>
/// PostgreSQL SQLSTATE to VibeSQL error code mapping
/// </summary>
public static class SqlStateMapper
{
    private static readonly Dictionary<string, string> SqlStateToVibeCode = new()
    {
        // Syntax errors
        ["42601"] = VibeErrorCodes.InvalidSQL,
        ["42703"] = VibeErrorCodes.InvalidSQL,
        ["42P01"] = VibeErrorCodes.InvalidSQL,
        ["42P02"] = VibeErrorCodes.InvalidSQL,
        ["42883"] = VibeErrorCodes.InvalidSQL,
        ["42804"] = VibeErrorCodes.InvalidSQL,

        // Query cancellation
        ["57014"] = VibeErrorCodes.QueryTimeout,

        // Resource limits
        ["53000"] = VibeErrorCodes.DatabaseUnavailable,
        ["53100"] = VibeErrorCodes.DatabaseUnavailable,
        ["53200"] = VibeErrorCodes.DatabaseUnavailable,
        ["53300"] = VibeErrorCodes.DatabaseUnavailable,
        ["53400"] = VibeErrorCodes.DatabaseUnavailable,

        // Connection errors
        ["08000"] = VibeErrorCodes.DatabaseUnavailable,
        ["08003"] = VibeErrorCodes.DatabaseUnavailable,
        ["08006"] = VibeErrorCodes.DatabaseUnavailable,
        ["08001"] = VibeErrorCodes.DatabaseUnavailable,
        ["08004"] = VibeErrorCodes.DatabaseUnavailable,

        // Document size errors
        ["54000"] = VibeErrorCodes.DocumentTooLarge,
        ["54001"] = VibeErrorCodes.DocumentTooLarge,
    };

    /// <summary>
    /// Translate Devart PgSqlException to VibeQueryError
    /// </summary>
    public static VibeQueryError TranslateDevartError(Devart.Data.PostgreSql.PgSqlException pgEx)
    {
        var sqlState = ExtractSqlStateFromMessage(pgEx.Message);
        var vibeCode = SqlStateToVibeCode.GetValueOrDefault(sqlState, VibeErrorCodes.InternalError);
        var message = GetMessageForCode(vibeCode, pgEx.Message);
        var detail = $"PostgreSQL error: {pgEx.Message}";

        return new VibeQueryError(vibeCode, message, detail);
    }

    private static string ExtractSqlStateFromMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        var lowerMessage = message.ToLowerInvariant();

        if (lowerMessage.Contains("syntax error"))
            return "42601";
        if (lowerMessage.Contains("column") && lowerMessage.Contains("does not exist"))
            return "42703";
        if (lowerMessage.Contains("relation") && lowerMessage.Contains("does not exist"))
            return "42P01";
        if (lowerMessage.Contains("function") && lowerMessage.Contains("does not exist"))
            return "42883";
        if (lowerMessage.Contains("type mismatch") || lowerMessage.Contains("cannot be cast"))
            return "42804";
        if (lowerMessage.Contains("canceling statement due to"))
            return "57014";
        if (lowerMessage.Contains("connection") && (lowerMessage.Contains("refused") || lowerMessage.Contains("failed")))
            return "08006";
        if (lowerMessage.Contains("too many connections"))
            return "53300";

        var sqlStatePattern = System.Text.RegularExpressions.Regex.Match(message, @"\b([0-9]{2}[0-9A-Z]{3})\b");
        if (sqlStatePattern.Success)
            return sqlStatePattern.Groups[1].Value;

        return string.Empty;
    }

    /// <summary>
    /// Translate generic exception to VibeQueryError
    /// </summary>
    public static VibeQueryError TranslateError(Exception ex)
    {
        if (ex is Devart.Data.PostgreSql.PgSqlException pgEx)
            return TranslateDevartError(pgEx);

        if (ex is OperationCanceledException or TaskCanceledException)
        {
            return new VibeQueryError(
                VibeErrorCodes.QueryTimeout,
                "Query execution timeout",
                "Query exceeded the maximum execution time");
        }

        return new VibeQueryError(
            VibeErrorCodes.InternalError,
            "An internal error occurred",
            ex.Message);
    }

    private static string GetMessageForCode(string vibeCode, string pgMessage) => vibeCode switch
    {
        VibeErrorCodes.InvalidSQL => "Invalid SQL syntax",
        VibeErrorCodes.QueryTimeout => "Query execution timeout",
        VibeErrorCodes.DatabaseUnavailable => "Database is unavailable",
        VibeErrorCodes.DocumentTooLarge => "Document too large",
        _ => !string.IsNullOrEmpty(pgMessage) ? pgMessage : "An error occurred"
    };
}
