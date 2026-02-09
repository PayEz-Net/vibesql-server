namespace VibeSQL.Core.Entities;

/// <summary>
/// Singleton table for system-wide logging defaults.
/// Lives in vibe.global_log_settings (physical table, not VSQL).
/// Per-client settings in vibe_app.client_log_settings inherit from these if not specified.
/// </summary>
public class GlobalLogSettings
{
    /// <summary>
    /// Primary key (always 1 - singleton pattern)
    /// </summary>
    public int Id { get; set; } = 1;

    // Category log levels (0=debug, 1=info, 2=warn, 3=error, 4=critical)
    public int LevelApi { get; set; } = 1;
    public int LevelAuth { get; set; } = 1;
    public int LevelDatabase { get; set; } = 2;
    public int LevelAgent { get; set; } = 1;
    public int LevelSystem { get; set; } = 2;

    // Retention by level (days)
    public int RetentionDebugDays { get; set; } = 7;
    public int RetentionInfoDays { get; set; } = 30;
    public int RetentionWarnDays { get; set; } = 60;
    public int RetentionErrorDays { get; set; } = 90;
    public int RetentionCriticalDays { get; set; } = 180;

    // Size limits
    public int MaxSizeMb { get; set; } = 10;
    public int MaxRows { get; set; } = 20000;

    // Audit
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int? UpdatedBy { get; set; }
}

/// <summary>
/// Log level constants for MVP logging
/// </summary>
public static class LogLevels
{
    public const int Debug = 0;
    public const int Info = 1;
    public const int Warn = 2;
    public const int Error = 3;
    public const int Critical = 4;

    public static string ToName(int level) => level switch
    {
        Debug => "debug",
        Info => "info",
        Warn => "warn",
        Error => "error",
        Critical => "critical",
        _ => "unknown"
    };

    public static int FromName(string? name) => name?.ToLowerInvariant() switch
    {
        "debug" => Debug,
        "info" => Info,
        "warn" or "warning" => Warn,
        "error" => Error,
        "critical" or "fatal" => Critical,
        _ => Info
    };
}
