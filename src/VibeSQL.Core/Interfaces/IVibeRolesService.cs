namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service interface for managing vibe_app roles and user role assignments.
/// Used by both Vibe.Public.Api and External.Id.Api.
/// </summary>
public interface IVibeRolesService
{
    // Role CRUD
    Task<List<VibeRoleDto>> GetRolesAsync(string clientId);
    Task<VibeRoleDto?> GetRoleAsync(string clientId, string roleId);
    Task<VibeRoleDto> CreateRoleAsync(string clientId, CreateVibeRoleRequest request, int? createdByUserId = null);
    Task<VibeRoleDto?> UpdateRoleAsync(string clientId, string roleId, UpdateVibeRoleRequest request, int? updatedByUserId = null);
    Task<bool> DeleteRoleAsync(string clientId, string roleId);

    // User Role Assignments
    Task<List<UserVibeRoleDto>> GetUserRolesAsync(string clientId, string userId);
    Task<UserVibeRoleDto> AssignRoleToUserAsync(string clientId, AssignVibeRoleRequest request, int? assignedByUserId = null);
    Task<bool> UnassignRoleFromUserAsync(string clientId, string userId, string roleId);
    Task<bool> BulkAssignRolesToUserAsync(string clientId, string userId, List<string> roleIds, int? assignedByUserId = null);

    // Seed default roles for new clients
    Task SeedDefaultRolesAsync(string clientId);
}

/// <summary>
/// Role data from vibe_app collection
/// </summary>
public class VibeRoleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new();
    public bool IsSystemRole { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
}

/// <summary>
/// User role assignment from user_roles collection
/// </summary>
public class UserVibeRoleDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string? RoleName { get; set; }
    public DateTime AssignedAt { get; set; }
    public int? AssignedBy { get; set; }
}

public class CreateVibeRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? Permissions { get; set; }
    public bool IsSystemRole { get; set; }
}

public class UpdateVibeRoleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<string>? Permissions { get; set; }
}

public class AssignVibeRoleRequest
{
    public string UserId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
}
