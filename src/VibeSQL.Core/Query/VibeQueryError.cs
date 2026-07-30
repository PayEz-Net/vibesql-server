namespace VibeSQL.Core.Query;

/// <summary>
/// VibeSQL error codes.
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
    public const string TenantContextRequired = "TENANT_CONTEXT_REQUIRED";

    /// <summary>Recovered from origin/npgsql-migration 2026-07-30 — constraint-violation taxonomy
    /// (SQLSTATE class 23) that the binary reconstruction could not recover.</summary>
    public const string ConstraintViolation = "CONSTRAINT_VIOLATION";
    public const string UniqueViolation = "UNIQUE_VIOLATION";
    public const string ForeignKeyViolation = "FOREIGN_KEY_VIOLATION";
    public const string NotNullViolation = "NOT_NULL_VIOLATION";
    public const string CheckViolation = "CHECK_VIOLATION";
    public const string ExclusionViolation = "EXCLUSION_VIOLATION";
}

/// <summary>
/// VibeSQL error with code, message, and detail.
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
        // Constraint violations (recovered 2026-07-30): conflicts are 409, client-input problems 400.
        VibeErrorCodes.UniqueViolation => 409,
        VibeErrorCodes.ForeignKeyViolation => 409,
        VibeErrorCodes.ExclusionViolation => 409,
        VibeErrorCodes.ConstraintViolation => 409,
        VibeErrorCodes.NotNullViolation => 400,
        VibeErrorCodes.CheckViolation => 400,
        VibeErrorCodes.TenantContextRequired => 500,
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
