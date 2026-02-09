namespace VibeSQL.Core.Entities;

/// <summary>
/// Per-user override for page permissions (grant or deny specific users regardless of role).
/// </summary>
public class PagePermissionOverride
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int OverrideId { get; set; }

    /// <summary>
    /// FK to page_permissions
    /// </summary>
    public int PagePermissionId { get; set; }

    /// <summary>
    /// The user ID this override applies to
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Override type: "grant" or "deny"
    /// </summary>
    public string OverrideType { get; set; } = string.Empty;

    /// <summary>
    /// Reason for the override (for audit purposes)
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// When this override expires (null = never)
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// When this override was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// User ID who created this override
    /// </summary>
    public int? CreatedBy { get; set; }

    /// <summary>
    /// Navigation property to the parent permission
    /// </summary>
    public virtual PagePermission? PagePermission { get; set; }
}
