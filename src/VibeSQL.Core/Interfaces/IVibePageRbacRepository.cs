using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository interface for page-level RBAC operations.
/// </summary>
public interface IVibePageRbacRepository
{
    // ============================================================
    // Page Permissions
    // ============================================================

    /// <summary>
    /// Gets all active page permissions for a client, ordered by specificity (most specific first).
    /// </summary>
    Task<List<PagePermission>> GetActivePermissionsForClientAsync(int clientId);

    /// <summary>
    /// Gets a page permission by ID with role requirements and overrides.
    /// </summary>
    Task<PagePermission?> GetPermissionByIdAsync(int pagePermissionId);

    /// <summary>
    /// Gets all page permissions for a client (including inactive), with role requirements.
    /// </summary>
    Task<List<PagePermission>> GetAllPermissionsForClientAsync(int clientId);

    /// <summary>
    /// Gets all page permissions accessible by the specified roles for a client.
    /// </summary>
    Task<List<PagePermission>> GetPermissionsForRolesAsync(int clientId, IEnumerable<string> roleNames);

    /// <summary>
    /// Creates a new page permission.
    /// </summary>
    Task<PagePermission> CreatePermissionAsync(PagePermission permission);

    /// <summary>
    /// Updates an existing page permission.
    /// </summary>
    Task<PagePermission> UpdatePermissionAsync(PagePermission permission);

    /// <summary>
    /// Deletes a page permission.
    /// </summary>
    Task<bool> DeletePermissionAsync(int clientId, int pagePermissionId);

    /// <summary>
    /// Checks if a route pattern already exists for a client.
    /// </summary>
    Task<bool> RoutePatternExistsAsync(int clientId, string routePattern, int? excludePermissionId = null);

    // ============================================================
    // Role Requirements
    // ============================================================

    /// <summary>
    /// Adds a role requirement to a page permission.
    /// </summary>
    Task<PageRoleRequirement> AddRoleRequirementAsync(int pagePermissionId, string roleName);

    /// <summary>
    /// Removes a role requirement from a page permission.
    /// </summary>
    Task<bool> RemoveRoleRequirementAsync(int pagePermissionId, string roleName);

    /// <summary>
    /// Gets all role requirements for a page permission.
    /// </summary>
    Task<List<PageRoleRequirement>> GetRoleRequirementsAsync(int pagePermissionId);

    /// <summary>
    /// Sets the role requirements for a page permission (replaces existing).
    /// </summary>
    Task SetRoleRequirementsAsync(int pagePermissionId, IEnumerable<string> roleNames);

    // ============================================================
    // User Overrides
    // ============================================================

    /// <summary>
    /// Gets active (non-expired) override for a user on a specific page permission.
    /// </summary>
    Task<PagePermissionOverride?> GetActiveOverrideAsync(int pagePermissionId, int userId);

    /// <summary>
    /// Gets all active overrides for a user across all permissions for a client.
    /// </summary>
    Task<List<PagePermissionOverride>> GetActiveOverridesForUserAsync(int clientId, int userId);

    /// <summary>
    /// Creates or updates an override for a user on a page permission.
    /// </summary>
    Task<PagePermissionOverride> UpsertOverrideAsync(PagePermissionOverride permissionOverride);

    /// <summary>
    /// Deletes an override after validating client ownership.
    /// </summary>
    Task<bool> DeleteOverrideAsync(int clientId, int overrideId);

    /// <summary>
    /// Gets all overrides for a page permission.
    /// </summary>
    Task<List<PagePermissionOverride>> GetOverridesForPermissionAsync(int pagePermissionId);

    // ============================================================
    // Claim Requirements
    // ============================================================

    /// <summary>
    /// Adds a claim requirement to a page permission.
    /// </summary>
    Task<PageClaimRequirement> AddClaimRequirementAsync(int pagePermissionId, string claimType, string? claimValue, bool isRequired = true);

    /// <summary>
    /// Removes a claim requirement from a page permission.
    /// </summary>
    Task<bool> RemoveClaimRequirementAsync(int pagePermissionId, string claimType, string? claimValue);

    /// <summary>
    /// Gets all claim requirements for a page permission.
    /// </summary>
    Task<List<PageClaimRequirement>> GetClaimRequirementsAsync(int pagePermissionId);

    /// <summary>
    /// Sets the claim requirements for a page permission (replaces existing).
    /// </summary>
    Task SetClaimRequirementsAsync(int pagePermissionId, IEnumerable<(string ClaimType, string? ClaimValue, bool IsRequired)> claims);

    // ============================================================
    // User Roles
    // ============================================================

    /// <summary>
    /// Gets user roles from vibe.documents (vibe_app/users table).
    /// Returns roles array from the user's document data.
    /// </summary>
    Task<List<string>> GetUserRolesFromDocumentsAsync(int clientId, int userId);
}
