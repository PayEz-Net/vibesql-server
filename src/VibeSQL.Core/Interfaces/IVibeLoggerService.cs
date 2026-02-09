namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for logging Vibe data operations to the data_logs table.
/// Provides centralized, queryable logging for debugging and monitoring.
/// </summary>
public interface IVibeLoggerService
{
    /// <summary>
    /// Logs a message at the specified level.
    /// </summary>
    Task LogAsync(VibeLogLevel level, string category, string message, object? details = null);

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    Task LogDebugAsync(string category, string message, object? details = null);

    /// <summary>
    /// Logs an info message.
    /// </summary>
    Task LogInfoAsync(string category, string message, object? details = null);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    Task LogWarnAsync(string category, string message, object? details = null);

    /// <summary>
    /// Logs an error with exception details.
    /// </summary>
    Task LogErrorAsync(string category, Exception ex, string message, object? details = null);

    /// <summary>
    /// Logs an error with error code.
    /// </summary>
    Task LogErrorAsync(string category, string errorCode, string message, object? details = null);

    /// <summary>
    /// Logs a fatal error.
    /// </summary>
    Task LogFatalAsync(string category, Exception ex, string message, object? details = null);

    /// <summary>
    /// Logs an operation with timing information.
    /// </summary>
    Task LogOperationAsync(string operation, string collection, string? table, TimeSpan duration, object? details = null);

    /// <summary>
    /// Sets the context for subsequent log calls (client, user, request).
    /// </summary>
    void SetContext(int clientId, int? userId = null, string? requestId = null);

    /// <summary>
    /// Sets collection context for subsequent log calls.
    /// </summary>
    void SetCollectionContext(string collection, string? table = null);
}

/// <summary>
/// Log levels for Vibe data logging.
/// </summary>
public enum VibeLogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
    Fatal = 4
}

/// <summary>
/// Log categories for Vibe data logging.
/// </summary>
public static class VibeLogCategory
{
    public const string Schema = "schema";
    public const string Document = "document";
    public const string Query = "query";
    public const string Auth = "auth";
    public const string ForeignKey = "fk";
    public const string Encryption = "encryption";
    public const string Migration = "migration";
    public const string System = "system";
}

/// <summary>
/// Error codes for Vibe data logging.
/// </summary>
public static class VibeErrorCode
{
    // FK errors
    public const string FkInvalidValue = "FK_INVALID_VALUE";
    public const string FkParentNotFound = "FK_PARENT_NOT_FOUND";
    public const string FkCircularRef = "FK_CIRCULAR_REF";

    // Document errors
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string RequiredFieldMissing = "REQUIRED_FIELD_MISSING";
    public const string TypeMismatch = "TYPE_MISMATCH";

    // Schema errors
    public const string SchemaInvalid = "SCHEMA_INVALID";
    public const string SchemaLocked = "SCHEMA_LOCKED";

    // Encryption errors
    public const string EncryptionFailed = "ENCRYPTION_FAILED";
    public const string DecryptionFailed = "DECRYPTION_FAILED";

    // Auth errors
    public const string AuthFailed = "AUTH_FAILED";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string TokenInvalid = "TOKEN_INVALID";

    // Query errors
    public const string QueryTimeout = "QUERY_TIMEOUT";
    public const string QueryInvalid = "QUERY_INVALID";

    // Migration errors
    public const string MigrationFailed = "MIGRATION_FAILED";
}
