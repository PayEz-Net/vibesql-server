namespace VibeSQL.Core.Entities;

/// <summary>
/// Defines a protected route with role requirements for page-level RBAC.
/// Each permission defines which roles can access a specific route pattern.
/// </summary>
public class PagePermission
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int PagePermissionId { get; set; }

    /// <summary>
    /// The IDP client identifier (FK to clients)
    /// </summary>
    public int ClientId { get; set; }

    /// <summary>
    /// Route pattern to match (e.g., "/admin/*", "/admin/users")
    /// Supports exact match and wildcard patterns
    /// </summary>
    public string RoutePattern { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for the permission
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what this permission controls
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// How to evaluate role requirements: "any" (OR) or "all" (AND)
    /// </summary>
    public string RoleLogic { get; set; } = "any";

    /// <summary>
    /// Whether 2FA is required for this route
    /// </summary>
    public bool Requires2fa { get; set; }

    /// <summary>
    /// Whether elevated authentication is required
    /// </summary>
    public bool RequiresElevatedAuth { get; set; }

    /// <summary>
    /// Whether this permission is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Display order for admin UI
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// When this permission was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When this permission was last updated
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// User ID who last updated this permission
    /// </summary>
    public int? UpdatedBy { get; set; }

    /// <summary>
    /// Navigation property for role requirements
    /// </summary>
    public virtual ICollection<PageRoleRequirement> RoleRequirements { get; set; } = new List<PageRoleRequirement>();

    /// <summary>
    /// Navigation property for user overrides
    /// </summary>
    public virtual ICollection<PagePermissionOverride> Overrides { get; set; } = new List<PagePermissionOverride>();

    /// <summary>
    /// Navigation property for claim requirements
    /// </summary>
    public virtual ICollection<PageClaimRequirement> ClaimRequirements { get; set; } = new List<PageClaimRequirement>();
}
