namespace VibeSQL.Core.Entities;

/// <summary>
/// Links a claim requirement to a page permission.
/// Use cases: tier (premium/enterprise), feature_flag (beta_enabled), department (engineering).
/// </summary>
public class PageClaimRequirement
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int PageClaimRequirementId { get; set; }

    /// <summary>
    /// FK to page_permissions
    /// </summary>
    public int PagePermissionId { get; set; }

    /// <summary>
    /// The claim type (e.g., "tier", "feature_flag", "department")
    /// </summary>
    public string ClaimType { get; set; } = string.Empty;

    /// <summary>
    /// The claim value (e.g., "premium", "beta_enabled", "engineering")
    /// Nullable - if null, just checks that the claim type exists
    /// </summary>
    public string? ClaimValue { get; set; }

    /// <summary>
    /// Whether this claim is required (default true)
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// When this requirement was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Navigation property to the parent permission
    /// </summary>
    public virtual PagePermission? PagePermission { get; set; }
}
