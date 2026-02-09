namespace VibeSQL.Core.Entities;

/// <summary>
/// Links a role to a page permission - defines which roles can access a page.
/// </summary>
public class PageRoleRequirement
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int PageRoleRequirementId { get; set; }

    /// <summary>
    /// FK to page_permissions
    /// </summary>
    public int PagePermissionId { get; set; }

    /// <summary>
    /// The role name required for access
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// When this requirement was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Navigation property to the parent permission
    /// </summary>
    public virtual PagePermission? PagePermission { get; set; }
}
