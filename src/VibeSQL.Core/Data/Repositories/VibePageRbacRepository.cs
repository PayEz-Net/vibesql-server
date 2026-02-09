using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for page-level RBAC operations.
/// </summary>
public class VibePageRbacRepository : IVibePageRbacRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<VibePageRbacRepository> _logger;

    public VibePageRbacRepository(VibeDbContext context, ILogger<VibePageRbacRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ============================================================
    // Page Permissions
    // ============================================================

    public async Task<List<PagePermission>> GetActivePermissionsForClientAsync(int clientId)
    {
        return await _context.PagePermissions
            .Include(p => p.RoleRequirements)
            .Include(p => p.ClaimRequirements)
            .Include(p => p.Overrides.Where(o => o.ExpiresAt == null || o.ExpiresAt > DateTimeOffset.UtcNow))
            .Where(p => p.ClientId == clientId && p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();
    }

    public async Task<PagePermission?> GetPermissionByIdAsync(int pagePermissionId)
    {
        return await _context.PagePermissions
            .Include(p => p.RoleRequirements)
            .Include(p => p.ClaimRequirements)
            .Include(p => p.Overrides)
            .FirstOrDefaultAsync(p => p.PagePermissionId == pagePermissionId);
    }

    public async Task<List<PagePermission>> GetAllPermissionsForClientAsync(int clientId)
    {
        return await _context.PagePermissions
            .Include(p => p.RoleRequirements)
            .Include(p => p.ClaimRequirements)
            .Where(p => p.ClientId == clientId)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.RoutePattern)
            .ToListAsync();
    }

    public async Task<List<PagePermission>> GetPermissionsForRolesAsync(int clientId, IEnumerable<string> roleNames)
    {
        var roleList = roleNames.ToList();
        if (!roleList.Any())
            return new List<PagePermission>();

        return await _context.PagePermissions
            .Include(p => p.RoleRequirements)
            .Where(p => p.ClientId == clientId && p.IsActive)
            .Where(p => p.RoleRequirements.Any(r => roleList.Contains(r.RoleName)))
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.RoutePattern)
            .ToListAsync();
    }

    public async Task<PagePermission> CreatePermissionAsync(PagePermission permission)
    {
        permission.CreatedAt = DateTimeOffset.UtcNow;
        permission.UpdatedAt = DateTimeOffset.UtcNow;

        _context.PagePermissions.Add(permission);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_PAGE_RBAC: Created permission {PermissionId} for route {Route} on ClientId={ClientId}",
            permission.PagePermissionId, permission.RoutePattern, permission.ClientId);

        return permission;
    }

    public async Task<PagePermission> UpdatePermissionAsync(PagePermission permission)
    {
        permission.UpdatedAt = DateTimeOffset.UtcNow;

        _context.PagePermissions.Update(permission);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_PAGE_RBAC: Updated permission {PermissionId} for route {Route} on ClientId={ClientId}",
            permission.PagePermissionId, permission.RoutePattern, permission.ClientId);

        return permission;
    }

    public async Task<bool> DeletePermissionAsync(int clientId, int pagePermissionId)
    {
        var permission = await _context.PagePermissions
            .FirstOrDefaultAsync(p => p.PagePermissionId == pagePermissionId && p.ClientId == clientId);

        if (permission == null)
        {
            return false;
        }

        _context.PagePermissions.Remove(permission);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_PAGE_RBAC: Deleted permission {PermissionId} for route {Route} on ClientId={ClientId}",
            pagePermissionId, permission.RoutePattern, clientId);

        return true;
    }

    public async Task<bool> RoutePatternExistsAsync(int clientId, string routePattern, int? excludePermissionId = null)
    {
        var query = _context.PagePermissions
            .Where(p => p.ClientId == clientId && p.RoutePattern == routePattern);

        if (excludePermissionId.HasValue)
        {
            query = query.Where(p => p.PagePermissionId != excludePermissionId.Value);
        }

        return await query.AnyAsync();
    }

    // ============================================================
    // Role Requirements
    // ============================================================

    public async Task<PageRoleRequirement> AddRoleRequirementAsync(int pagePermissionId, string roleName)
    {
        var requirement = new PageRoleRequirement
        {
            PagePermissionId = pagePermissionId,
            RoleName = roleName,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.PageRoleRequirements.Add(requirement);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_PAGE_RBAC: Added role {Role} to permission {PermissionId}",
            roleName, pagePermissionId);

        return requirement;
    }

    public async Task<bool> RemoveRoleRequirementAsync(int pagePermissionId, string roleName)
    {
        var requirement = await _context.PageRoleRequirements
            .FirstOrDefaultAsync(r => r.PagePermissionId == pagePermissionId && r.RoleName == roleName);

        if (requirement == null)
        {
            return false;
        }

        _context.PageRoleRequirements.Remove(requirement);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_PAGE_RBAC: Removed role {Role} from permission {PermissionId}",
            roleName, pagePermissionId);

        return true;
    }

    public async Task<List<PageRoleRequirement>> GetRoleRequirementsAsync(int pagePermissionId)
    {
        return await _context.PageRoleRequirements
            .Where(r => r.PagePermissionId == pagePermissionId)
            .OrderBy(r => r.RoleName)
            .ToListAsync();
    }

    public async Task SetRoleRequirementsAsync(int pagePermissionId, IEnumerable<string> roleNames)
    {
        // Remove existing requirements
        var existingRequirements = await _context.PageRoleRequirements
            .Where(r => r.PagePermissionId == pagePermissionId)
            .ToListAsync();

        _context.PageRoleRequirements.RemoveRange(existingRequirements);

        // Add new requirements
        var now = DateTimeOffset.UtcNow;
        var newRequirements = roleNames.Distinct().Select(roleName => new PageRoleRequirement
        {
            PagePermissionId = pagePermissionId,
            RoleName = roleName,
            CreatedAt = now
        });

        _context.PageRoleRequirements.AddRange(newRequirements);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_PAGE_RBAC: Set roles [{Roles}] on permission {PermissionId}",
            string.Join(", ", roleNames), pagePermissionId);
    }

    // ============================================================
    // User Overrides
    // ============================================================

    public async Task<PagePermissionOverride?> GetActiveOverrideAsync(int pagePermissionId, int userId)
    {
        return await _context.PagePermissionOverrides
            .FirstOrDefaultAsync(o =>
                o.PagePermissionId == pagePermissionId &&
                o.UserId == userId &&
                (o.ExpiresAt == null || o.ExpiresAt > DateTimeOffset.UtcNow));
    }

    public async Task<List<PagePermissionOverride>> GetActiveOverridesForUserAsync(int clientId, int userId)
    {
        return await _context.PagePermissionOverrides
            .Include(o => o.PagePermission)
            .Where(o =>
                o.PagePermission!.ClientId == clientId &&
                o.UserId == userId &&
                (o.ExpiresAt == null || o.ExpiresAt > DateTimeOffset.UtcNow))
            .ToListAsync();
    }

    public async Task<PagePermissionOverride> UpsertOverrideAsync(PagePermissionOverride permissionOverride)
    {
        var existing = await _context.PagePermissionOverrides
            .FirstOrDefaultAsync(o =>
                o.PagePermissionId == permissionOverride.PagePermissionId &&
                o.UserId == permissionOverride.UserId);

        if (existing != null)
        {
            existing.OverrideType = permissionOverride.OverrideType;
            existing.Reason = permissionOverride.Reason;
            existing.ExpiresAt = permissionOverride.ExpiresAt;
            existing.CreatedBy = permissionOverride.CreatedBy;

            _logger.LogInformation("VIBE_PAGE_RBAC: Updated override for user {UserId} on permission {PermissionId} to {Type}",
                permissionOverride.UserId, permissionOverride.PagePermissionId, permissionOverride.OverrideType);
        }
        else
        {
            permissionOverride.CreatedAt = DateTimeOffset.UtcNow;
            _context.PagePermissionOverrides.Add(permissionOverride);
            existing = permissionOverride;

            _logger.LogInformation("VIBE_PAGE_RBAC: Created override for user {UserId} on permission {PermissionId} as {Type}",
                permissionOverride.UserId, permissionOverride.PagePermissionId, permissionOverride.OverrideType);
        }

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteOverrideAsync(int clientId, int overrideId)
    {
        // Join to PagePermission to validate client ownership - SECURITY FIX
        var existingOverride = await _context.PagePermissionOverrides
            .Include(o => o.PagePermission)
            .FirstOrDefaultAsync(o => o.OverrideId == overrideId);

        if (existingOverride == null)
        {
            return false;
        }

        // Validate client ownership - prevent cross-client data manipulation
        if (existingOverride.PagePermission?.ClientId != clientId)
        {
            _logger.LogWarning("VIBE_PAGE_RBAC: Attempted cross-client delete of override {OverrideId} by ClientId={ClientId} (owner: {OwnerClientId})",
                overrideId, clientId, existingOverride.PagePermission?.ClientId);
            return false;
        }

        _context.PagePermissionOverrides.Remove(existingOverride);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_PAGE_RBAC: Deleted override {OverrideId} for ClientId={ClientId}", overrideId, clientId);

        return true;
    }

    public async Task<List<PagePermissionOverride>> GetOverridesForPermissionAsync(int pagePermissionId)
    {
        return await _context.PagePermissionOverrides
            .Where(o => o.PagePermissionId == pagePermissionId)
            .OrderBy(o => o.UserId)
            .ToListAsync();
    }

    // ============================================================
    // Claim Requirements
    // ============================================================

    public async Task<PageClaimRequirement> AddClaimRequirementAsync(int pagePermissionId, string claimType, string? claimValue, bool isRequired = true)
    {
        var requirement = new PageClaimRequirement
        {
            PagePermissionId = pagePermissionId,
            ClaimType = claimType,
            ClaimValue = claimValue,
            IsRequired = isRequired,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.PageClaimRequirements.Add(requirement);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_PAGE_RBAC: Added claim {ClaimType}:{ClaimValue} to permission {PermissionId}",
            claimType, claimValue ?? "(any)", pagePermissionId);

        return requirement;
    }

    public async Task<bool> RemoveClaimRequirementAsync(int pagePermissionId, string claimType, string? claimValue)
    {
        var requirement = await _context.PageClaimRequirements
            .FirstOrDefaultAsync(c =>
                c.PagePermissionId == pagePermissionId &&
                c.ClaimType == claimType &&
                c.ClaimValue == claimValue);

        if (requirement == null)
        {
            return false;
        }

        _context.PageClaimRequirements.Remove(requirement);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_PAGE_RBAC: Removed claim {ClaimType}:{ClaimValue} from permission {PermissionId}",
            claimType, claimValue ?? "(any)", pagePermissionId);

        return true;
    }

    public async Task<List<PageClaimRequirement>> GetClaimRequirementsAsync(int pagePermissionId)
    {
        return await _context.PageClaimRequirements
            .Where(c => c.PagePermissionId == pagePermissionId)
            .OrderBy(c => c.ClaimType)
            .ThenBy(c => c.ClaimValue)
            .ToListAsync();
    }

    public async Task SetClaimRequirementsAsync(int pagePermissionId, IEnumerable<(string ClaimType, string? ClaimValue, bool IsRequired)> claims)
    {
        // Remove existing requirements
        var existingRequirements = await _context.PageClaimRequirements
            .Where(c => c.PagePermissionId == pagePermissionId)
            .ToListAsync();

        _context.PageClaimRequirements.RemoveRange(existingRequirements);

        // Add new requirements
        var now = DateTimeOffset.UtcNow;
        var newRequirements = claims.Select(c => new PageClaimRequirement
        {
            PagePermissionId = pagePermissionId,
            ClaimType = c.ClaimType,
            ClaimValue = c.ClaimValue,
            IsRequired = c.IsRequired,
            CreatedAt = now
        });

        _context.PageClaimRequirements.AddRange(newRequirements);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_PAGE_RBAC: Set claims on permission {PermissionId}", pagePermissionId);
    }

    // ============================================================
    // User Roles
    // ============================================================

    public async Task<List<string>> GetUserRolesFromDocumentsAsync(int clientId, int userId)
    {
        // Query the vibe_app/users document for the user
        var userDocument = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.OwnerUserId == userId
                     && d.Collection == "vibe_app"
                     && d.TableName == "users"
                     && d.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (userDocument?.Data == null)
        {
            _logger.LogDebug("VIBE_PAGE_RBAC: No vibe_app/users document found for UserId={UserId}, ClientId={ClientId}",
                userId, clientId);
            return new List<string>();
        }

        // Parse the JSON data to extract roles
        try
        {
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(userDocument.Data);
            if (jsonDoc.RootElement.TryGetProperty("roles", out var rolesElement) &&
                rolesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var roles = rolesElement.EnumerateArray()
                    .Where(r => r.ValueKind == System.Text.Json.JsonValueKind.String)
                    .Select(r => r.GetString()!)
                    .Where(r => !string.IsNullOrEmpty(r))
                    .ToList();

                _logger.LogDebug("VIBE_PAGE_RBAC: Found {Count} roles for UserId={UserId}, ClientId={ClientId}: [{Roles}]",
                    roles.Count, userId, clientId, string.Join(", ", roles));

                return roles;
            }

            _logger.LogDebug("VIBE_PAGE_RBAC: No roles array in vibe_app/users document for UserId={UserId}, ClientId={ClientId}",
                userId, clientId);
            return new List<string>();
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "VIBE_PAGE_RBAC: Failed to parse JSON data for UserId={UserId}, ClientId={ClientId}",
                userId, clientId);
            return new List<string>();
        }
    }
}
