using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using System.Text.Json;

namespace VibeSQL.Core.Services.Logging;

/// <summary>
/// Service for logging Vibe data operations to vibe_app.data_logs.
/// Uses the Vibe document system (vibe.documents table).
/// Thread-safe with per-request context tracking.
/// </summary>
public class VibeLoggerService : IVibeLoggerService
{
    private readonly IVibeDataLogRepository _logRepository;
    private readonly ILogger<VibeLoggerService> _logger;

    // Per-request context (set via SetContext)
    private int _clientId;
    private int? _userId;
    private string? _requestId;
    private string? _collection;
    private string? _tableName;

    // Minimum log level (can be overridden per-client)
    private VibeLogLevel _minimumLevel = VibeLogLevel.Info;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public VibeLoggerService(
        IVibeDataLogRepository logRepository,
        ILogger<VibeLoggerService> logger)
    {
        _logRepository = logRepository;
        _logger = logger;
    }

    public void SetContext(int clientId, int? userId = null, string? requestId = null)
    {
        _clientId = clientId;
        _userId = userId;
        _requestId = requestId;
    }

    public void SetCollectionContext(string collection, string? table = null)
    {
        _collection = collection;
        _tableName = table;
    }

    public async Task LogAsync(VibeLogLevel level, string category, string message, object? details = null)
    {
        if (level < _minimumLevel) return;
        if (_clientId <= 0) return; // No client context, skip logging

        try
        {
            var logData = new Dictionary<string, object?>
            {
                ["level"] = level.ToString().ToLowerInvariant(),
                ["category"] = category,
                ["user_id"] = _userId,
                ["collection_name"] = _collection,
                ["table_name"] = _tableName,
                ["message"] = message,
                ["details"] = details,
                ["request_id"] = _requestId,
                ["created_at"] = DateTimeOffset.UtcNow.ToString("o"),
                ["created_by"] = _userId
            };

            await _logRepository.CreateLogDocumentAsync(_clientId, logData);
        }
        catch (Exception ex)
        {
            // Don't let logging failures break the main operation
            _logger.LogError(ex, "Failed to write Vibe data log: {Message}", message);
        }
    }

    public Task LogDebugAsync(string category, string message, object? details = null)
        => LogAsync(VibeLogLevel.Debug, category, message, details);

    public Task LogInfoAsync(string category, string message, object? details = null)
        => LogAsync(VibeLogLevel.Info, category, message, details);

    public Task LogWarnAsync(string category, string message, object? details = null)
        => LogAsync(VibeLogLevel.Warn, category, message, details);

    public async Task LogErrorAsync(string category, Exception ex, string message, object? details = null)
    {
        if (_clientId <= 0) return;

        try
        {
            var logData = new Dictionary<string, object?>
            {
                ["level"] = "error",
                ["category"] = category,
                ["user_id"] = _userId,
                ["collection_name"] = _collection,
                ["table_name"] = _tableName,
                ["message"] = message,
                ["details"] = details,
                ["error_code"] = GetErrorCodeFromException(ex),
                ["stack_trace"] = ex.ToString(),
                ["request_id"] = _requestId,
                ["created_at"] = DateTimeOffset.UtcNow.ToString("o"),
                ["created_by"] = _userId
            };

            await _logRepository.CreateLogDocumentAsync(_clientId, logData);
        }
        catch (Exception logEx)
        {
            _logger.LogError(logEx, "Failed to write Vibe error log: {Message}", message);
        }
    }

    public async Task LogErrorAsync(string category, string errorCode, string message, object? details = null)
    {
        if (_clientId <= 0) return;

        try
        {
            var logData = new Dictionary<string, object?>
            {
                ["level"] = "error",
                ["category"] = category,
                ["user_id"] = _userId,
                ["collection_name"] = _collection,
                ["table_name"] = _tableName,
                ["message"] = message,
                ["details"] = details,
                ["error_code"] = errorCode,
                ["request_id"] = _requestId,
                ["created_at"] = DateTimeOffset.UtcNow.ToString("o"),
                ["created_by"] = _userId
            };

            await _logRepository.CreateLogDocumentAsync(_clientId, logData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write Vibe error log: {Message}", message);
        }
    }

    public async Task LogFatalAsync(string category, Exception ex, string message, object? details = null)
    {
        if (_clientId <= 0) return;

        try
        {
            var logData = new Dictionary<string, object?>
            {
                ["level"] = "fatal",
                ["category"] = category,
                ["user_id"] = _userId,
                ["collection_name"] = _collection,
                ["table_name"] = _tableName,
                ["message"] = message,
                ["details"] = details,
                ["error_code"] = GetErrorCodeFromException(ex),
                ["stack_trace"] = ex.ToString(),
                ["request_id"] = _requestId,
                ["created_at"] = DateTimeOffset.UtcNow.ToString("o"),
                ["created_by"] = _userId
            };

            await _logRepository.CreateLogDocumentAsync(_clientId, logData);
        }
        catch (Exception logEx)
        {
            _logger.LogError(logEx, "Failed to write Vibe fatal log: {Message}", message);
        }
    }

    public async Task LogOperationAsync(string operation, string collection, string? table, TimeSpan duration, object? details = null)
    {
        if (_clientId <= 0) return;

        // Log slow operations as warnings (>500ms)
        var level = duration.TotalMilliseconds > 500 ? "warn" : "info";
        var message = duration.TotalMilliseconds > 500
            ? $"Slow operation: {operation} on {collection}.{table} took {duration.TotalMilliseconds:F0}ms"
            : $"Operation: {operation} on {collection}.{table} completed in {duration.TotalMilliseconds:F0}ms";

        try
        {
            var logData = new Dictionary<string, object?>
            {
                ["level"] = level,
                ["category"] = VibeLogCategory.Document,
                ["user_id"] = _userId,
                ["collection_name"] = collection,
                ["table_name"] = table,
                ["operation"] = operation,
                ["message"] = message,
                ["details"] = details,
                ["duration_ms"] = (int)duration.TotalMilliseconds,
                ["request_id"] = _requestId,
                ["created_at"] = DateTimeOffset.UtcNow.ToString("o"),
                ["created_by"] = _userId
            };

            await _logRepository.CreateLogDocumentAsync(_clientId, logData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write Vibe operation log: {Message}", message);
        }
    }

    private static string? GetErrorCodeFromException(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();

        if (message.Contains("foreign key") || message.Contains("fk"))
            return VibeErrorCode.FkInvalidValue;

        if (message.Contains("validation"))
            return VibeErrorCode.ValidationFailed;

        if (message.Contains("required"))
            return VibeErrorCode.RequiredFieldMissing;

        if (message.Contains("type") && message.Contains("mismatch"))
            return VibeErrorCode.TypeMismatch;

        if (message.Contains("encrypt"))
            return VibeErrorCode.EncryptionFailed;

        if (message.Contains("decrypt"))
            return VibeErrorCode.DecryptionFailed;

        if (message.Contains("timeout"))
            return VibeErrorCode.QueryTimeout;

        if (message.Contains("locked"))
            return VibeErrorCode.SchemaLocked;

        return null;
    }
}
