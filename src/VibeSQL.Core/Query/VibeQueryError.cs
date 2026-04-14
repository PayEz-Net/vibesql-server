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

    // Integrity constraint violations (PostgreSQL SQLSTATE class 23).
    // Distinct codes for each so clients can branch; all emit structured
    // CONSTRAINT_VIOLATION observability events.
    public const string ConstraintViolation = "CONSTRAINT_VIOLATION";
    public const string UniqueViolation = "UNIQUE_VIOLATION";
    public const string ForeignKeyViolation = "FOREIGN_KEY_VIOLATION";
    public const string NotNullViolation = "NOT_NULL_VIOLATION";
    public const string CheckViolation = "CHECK_VIOLATION";
    public const string ExclusionViolation = "EXCLUSION_VIOLATION";
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
        VibeErrorCodes.NotNullViolation => 400,
        VibeErrorCodes.CheckViolation => 400,
        VibeErrorCodes.Unauthorized => 401,
        VibeErrorCodes.QueryTimeout => 408,
        VibeErrorCodes.UniqueViolation => 409,
        VibeErrorCodes.ForeignKeyViolation => 409,
        VibeErrorCodes.ExclusionViolation => 409,
        VibeErrorCodes.ConstraintViolation => 409,
        VibeErrorCodes.QueryTooLarge => 413,
        VibeErrorCodes.ResultTooLarge => 413,
        VibeErrorCodes.DocumentTooLarge => 413,
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

        // Integrity constraint violations (PostgreSQL SQLSTATE class 23).
        // These surface as structured CONSTRAINT_VIOLATION events in the
        // observability pipeline (QueryExecutor emits ForContext scope on 23*).
        ["23000"] = VibeErrorCodes.ConstraintViolation,       // integrity_constraint_violation (generic)
        ["23001"] = VibeErrorCodes.ConstraintViolation,       // restrict_violation
        ["23502"] = VibeErrorCodes.NotNullViolation,          // not_null_violation
        ["23503"] = VibeErrorCodes.ForeignKeyViolation,       // foreign_key_violation
        ["23505"] = VibeErrorCodes.UniqueViolation,           // unique_violation
        ["23514"] = VibeErrorCodes.CheckViolation,            // check_violation
        ["23P01"] = VibeErrorCodes.ExclusionViolation,        // exclusion_violation
    };

    /// <summary>
    /// True if the SQLSTATE is in class 23 (integrity constraint violation).
    /// Used by the query executor to emit structured observability events.
    /// </summary>
    public static bool IsConstraintViolation(string? sqlState) =>
        !string.IsNullOrEmpty(sqlState) && sqlState.StartsWith("23", StringComparison.Ordinal);

    /// <summary>
    /// Translate Npgsql PostgresException to VibeQueryError
    /// </summary>
    public static VibeQueryError TranslatePostgresError(Npgsql.PostgresException pgEx)
    {
        var sqlState = pgEx.SqlState ?? string.Empty;
        var vibeCode = SqlStateToVibeCode.GetValueOrDefault(sqlState, VibeErrorCodes.InternalError);
        var message = GetMessageForCode(vibeCode, pgEx.MessageText);
        var detail = $"PostgreSQL error: {pgEx.MessageText}";

        return new VibeQueryError(vibeCode, message, detail);
    }

    /// <summary>
    /// Translate generic exception to VibeQueryError
    /// </summary>
    public static VibeQueryError TranslateError(Exception ex)
    {
        if (ex is Npgsql.PostgresException pgEx)
            return TranslatePostgresError(pgEx);

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
        // Constraint messages: prefer PostgreSQL's own text — it's already
        // human-readable and names the constraint/table/column.
        VibeErrorCodes.UniqueViolation => !string.IsNullOrEmpty(pgMessage) ? pgMessage : "Unique constraint violated",
        VibeErrorCodes.ForeignKeyViolation => !string.IsNullOrEmpty(pgMessage) ? pgMessage : "Foreign key constraint violated",
        VibeErrorCodes.NotNullViolation => !string.IsNullOrEmpty(pgMessage) ? pgMessage : "Not-null constraint violated",
        VibeErrorCodes.CheckViolation => !string.IsNullOrEmpty(pgMessage) ? pgMessage : "Check constraint violated",
        VibeErrorCodes.ExclusionViolation => !string.IsNullOrEmpty(pgMessage) ? pgMessage : "Exclusion constraint violated",
        VibeErrorCodes.ConstraintViolation => !string.IsNullOrEmpty(pgMessage) ? pgMessage : "Integrity constraint violated",
        _ => !string.IsNullOrEmpty(pgMessage) ? pgMessage : "An error occurred"
    };
}
