namespace VibeSQL.Core.Entities;

/// <summary>
/// Tracks admin actions for audit trail and compliance.
/// All admin operations are logged for accountability.
/// </summary>
public class AuditLog
{
    public long AuditLogId { get; set; }

    /// <summary>
    /// The IDP client identifier (tenant)
    /// </summary>
    public int ClientId { get; set; }

    /// <summary>
    /// The admin user who performed the action
    /// </summary>
    public int AdminUserId { get; set; }

    /// <summary>
    /// Admin user's email (denormalized for display)
    /// </summary>
    public string? AdminEmail { get; set; }

    /// <summary>
    /// Action category (e.g., "user", "tier", "session", "config")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Specific action taken (e.g., "set_tier", "revoke_session", "reset_credits")
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Target resource type (e.g., "user", "tier", "feature")
    /// </summary>
    public string? TargetType { get; set; }

    /// <summary>
    /// Target resource ID (e.g., user_id, tier_key)
    /// </summary>
    public string? TargetId { get; set; }

    /// <summary>
    /// Human-readable description of the action
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Previous state (JSON) for change tracking
    /// </summary>
    public string? PreviousValue { get; set; }

    /// <summary>
    /// New state (JSON) for change tracking
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Additional metadata (JSON) - IP address, user agent, etc.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// IP address of the admin
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Request path that triggered the action
    /// </summary>
    public string? RequestPath { get; set; }

    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE)
    /// </summary>
    public string? HttpMethod { get; set; }

    /// <summary>
    /// Whether the action succeeded
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// Error message if action failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the action occurred
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
