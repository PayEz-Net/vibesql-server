using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Data;
using VibeSQL.Core.Interfaces;
using System.Text.Json;

namespace VibeSQL.Core.Services;

/// <summary>
/// Service for managing vibe_app roles and user role assignments.
/// Uses VibeDbContext directly for database access.
/// </summary>
public class VibeRolesService : IVibeRolesService
{
    private readonly VibeDbContext _vibeContext;
    private readonly ILogger<VibeRolesService> _logger;

    private const string VIBE_APP_COLLECTION = "vibe_app";
    private const string ROLES_TABLE = "roles";
    private const string USER_ROLES_TABLE = "user_roles";
    private const int SYSTEM_USER_ID = 0; // Used for system-level documents like roles

    public VibeRolesService(VibeDbContext vibeContext, ILogger<VibeRolesService> logger)
    {
        _vibeContext = vibeContext;
        _logger = logger;
    }

    #region Role CRUD

    public async Task<List<VibeRoleDto>> GetRolesAsync(string clientId)
    {
        if (!int.TryParse(clientId, out var clientIdInt))
            return new List<VibeRoleDto>();

        var docs = await _vibeContext.Documents
            .Where(d => d.ClientId == clientIdInt
                     && d.Collection == VIBE_APP_COLLECTION
                     && d.TableName == ROLES_TABLE
                     && d.DeletedAt == null)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();

        return docs.Select(MapToRoleDto).ToList();
    }

    public async Task<VibeRoleDto?> GetRoleAsync(string clientId, string roleId)
    {
        if (!int.TryParse(clientId, out var clientIdInt) ||
            !int.TryParse(roleId, out var roleIdInt))
            return null;

        var doc = await _vibeContext.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == roleIdInt
                                   && d.ClientId == clientIdInt
                                   && d.Collection == VIBE_APP_COLLECTION
                                   && d.TableName == ROLES_TABLE
                                   && d.DeletedAt == null);

        return doc != null ? MapToRoleDto(doc) : null;
    }

    public async Task<VibeRoleDto> CreateRoleAsync(string clientId, CreateVibeRoleRequest request, int? createdByUserId = null)
    {
        if (!int.TryParse(clientId, out var clientIdInt))
            throw new ArgumentException("Invalid client ID", nameof(clientId));

        var data = new Dictionary<string, object>
        {
            ["name"] = request.Name,
            ["description"] = request.Description ?? "",
            ["permissions"] = request.Permissions ?? new List<string>(),
            ["is_system_role"] = request.IsSystemRole
        };

        var doc = new VibeDocument
        {
            ClientId = clientIdInt,
            OwnerUserId = SYSTEM_USER_ID,  // System-owned role document
            Collection = VIBE_APP_COLLECTION,
            TableName = ROLES_TABLE,
            Data = JsonSerializer.Serialize(data),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdByUserId
        };

        _vibeContext.Documents.Add(doc);
        await _vibeContext.SaveChangesAsync();

        _logger.LogInformation("Created role {RoleName} for client {ClientId} with ID {RoleId}",
            request.Name, clientIdInt, doc.DocumentId);

        return MapToRoleDto(doc);
    }

    public async Task<VibeRoleDto?> UpdateRoleAsync(string clientId, string roleId, UpdateVibeRoleRequest request, int? updatedByUserId = null)
    {
        if (!int.TryParse(clientId, out var clientIdInt) ||
            !int.TryParse(roleId, out var roleIdInt))
            return null;

        var doc = await _vibeContext.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == roleIdInt
                                   && d.ClientId == clientIdInt
                                   && d.Collection == VIBE_APP_COLLECTION
                                   && d.TableName == ROLES_TABLE
                                   && d.DeletedAt == null);

        if (doc == null)
            return null;

        var existingData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(doc.Data ?? "{}")
            ?? new Dictionary<string, JsonElement>();

        // Update only provided fields
        var newData = new Dictionary<string, object>();
        foreach (var kvp in existingData)
        {
            newData[kvp.Key] = kvp.Value;
        }

        if (request.Name != null)
            newData["name"] = request.Name;
        if (request.Description != null)
            newData["description"] = request.Description;
        if (request.Permissions != null)
            newData["permissions"] = request.Permissions;

        doc.Data = JsonSerializer.Serialize(newData);
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        doc.UpdatedBy = updatedByUserId;

        await _vibeContext.SaveChangesAsync();

        _logger.LogInformation("Updated role {RoleId} for client {ClientId}", roleIdInt, clientIdInt);

        return MapToRoleDto(doc);
    }

    public async Task<bool> DeleteRoleAsync(string clientId, string roleId)
    {
        if (!int.TryParse(clientId, out var clientIdInt) ||
            !int.TryParse(roleId, out var roleIdInt))
            return false;

        var doc = await _vibeContext.Documents
            .FirstOrDefaultAsync(d => d.DocumentId == roleIdInt
                                   && d.ClientId == clientIdInt
                                   && d.Collection == VIBE_APP_COLLECTION
                                   && d.TableName == ROLES_TABLE
                                   && d.DeletedAt == null);

        if (doc == null)
            return false;

        // Soft delete the role
        doc.DeletedAt = DateTimeOffset.UtcNow;
        await _vibeContext.SaveChangesAsync();

        // Also soft delete all user_role assignments for this role
        var userRoleAssignments = await _vibeContext.Documents
            .Where(d => d.ClientId == clientIdInt
                     && d.Collection == VIBE_APP_COLLECTION
                     && d.TableName == USER_ROLES_TABLE
                     && d.DeletedAt == null)
            .ToListAsync();

        foreach (var assignment in userRoleAssignments)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(assignment.Data ?? "{}");
            if (data != null && data.TryGetValue("role_id", out var assignedRoleId))
            {
                if (assignedRoleId.GetString() == roleId || assignedRoleId.ToString() == roleId)
                {
                    assignment.DeletedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        await _vibeContext.SaveChangesAsync();

        _logger.LogInformation("Deleted role {RoleId} and associated assignments for client {ClientId}",
            roleIdInt, clientIdInt);

        return true;
    }

    #endregion

    #region User Role Assignments

    public async Task<List<UserVibeRoleDto>> GetUserRolesAsync(string clientId, string userId)
    {
        if (!int.TryParse(clientId, out var clientIdInt) ||
            !int.TryParse(userId, out var userIdInt))
            return new List<UserVibeRoleDto>();

        var docs = await _vibeContext.Documents
            .Where(d => d.ClientId == clientIdInt
                     && d.OwnerUserId == userIdInt
                     && d.Collection == VIBE_APP_COLLECTION
                     && d.TableName == USER_ROLES_TABLE
                     && d.DeletedAt == null)
            .ToListAsync();

        var result = new List<UserVibeRoleDto>();
        foreach (var doc in docs)
        {
            var dto = MapToUserRoleDto(doc);

            // Enrich with role name
            if (!string.IsNullOrEmpty(dto.RoleId))
            {
                var role = await GetRoleAsync(clientId, dto.RoleId);
                dto.RoleName = role?.Name;
            }

            result.Add(dto);
        }

        return result;
    }

    public async Task<UserVibeRoleDto> AssignRoleToUserAsync(string clientId, AssignVibeRoleRequest request, int? assignedByUserId = null)
    {
        if (!int.TryParse(clientId, out var clientIdInt) ||
            !int.TryParse(request.UserId, out var userIdInt))
            throw new ArgumentException("Invalid client or user ID");

        // Check if assignment already exists
        var existing = await _vibeContext.Documents
            .FirstOrDefaultAsync(d => d.ClientId == clientIdInt
                                   && d.OwnerUserId == userIdInt
                                   && d.Collection == VIBE_APP_COLLECTION
                                   && d.TableName == USER_ROLES_TABLE
                                   && d.DeletedAt == null
                                   && d.Data != null
                                   && d.Data.Contains($"\"role_id\":\"{request.RoleId}\""));

        if (existing != null)
        {
            // Already assigned, return existing
            return MapToUserRoleDto(existing);
        }

        var data = new Dictionary<string, object>
        {
            ["user_id"] = request.UserId,
            ["role_id"] = request.RoleId,
            ["assigned_at"] = DateTimeOffset.UtcNow,
            ["assigned_by"] = assignedByUserId ?? 0
        };

        var doc = new VibeDocument
        {
            ClientId = clientIdInt,
            OwnerUserId = userIdInt,
            Collection = VIBE_APP_COLLECTION,
            TableName = USER_ROLES_TABLE,
            Data = JsonSerializer.Serialize(data),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = assignedByUserId
        };

        _vibeContext.Documents.Add(doc);
        await _vibeContext.SaveChangesAsync();

        _logger.LogInformation("Assigned role {RoleId} to user {UserId} for client {ClientId}",
            request.RoleId, request.UserId, clientIdInt);

        var result = MapToUserRoleDto(doc);

        // Enrich with role name
        var role = await GetRoleAsync(clientId, request.RoleId);
        result.RoleName = role?.Name;

        return result;
    }

    public async Task<bool> UnassignRoleFromUserAsync(string clientId, string userId, string roleId)
    {
        if (!int.TryParse(clientId, out var clientIdInt) ||
            !int.TryParse(userId, out var userIdInt))
            return false;

        var docs = await _vibeContext.Documents
            .Where(d => d.ClientId == clientIdInt
                     && d.OwnerUserId == userIdInt
                     && d.Collection == VIBE_APP_COLLECTION
                     && d.TableName == USER_ROLES_TABLE
                     && d.DeletedAt == null)
            .ToListAsync();

        var found = false;
        foreach (var doc in docs)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(doc.Data ?? "{}");
            if (data != null && data.TryGetValue("role_id", out var assignedRoleId))
            {
                if (assignedRoleId.GetString() == roleId || assignedRoleId.ToString() == roleId)
                {
                    doc.DeletedAt = DateTimeOffset.UtcNow;
                    found = true;
                }
            }
        }

        if (found)
        {
            await _vibeContext.SaveChangesAsync();
            _logger.LogInformation("Unassigned role {RoleId} from user {UserId} for client {ClientId}",
                roleId, userId, clientIdInt);
        }

        return found;
    }

    public async Task<bool> BulkAssignRolesToUserAsync(string clientId, string userId, List<string> roleIds, int? assignedByUserId = null)
    {
        if (!int.TryParse(clientId, out var clientIdInt) ||
            !int.TryParse(userId, out var userIdInt))
            return false;

        // Get current assignments
        var currentAssignments = await GetUserRolesAsync(clientId, userId);
        var currentRoleIds = currentAssignments.Select(a => a.RoleId).ToHashSet();

        // Remove roles not in new list
        foreach (var current in currentAssignments)
        {
            if (!roleIds.Contains(current.RoleId))
            {
                await UnassignRoleFromUserAsync(clientId, userId, current.RoleId);
            }
        }

        // Add new roles
        foreach (var roleId in roleIds)
        {
            if (!currentRoleIds.Contains(roleId))
            {
                await AssignRoleToUserAsync(clientId, new AssignVibeRoleRequest
                {
                    UserId = userId,
                    RoleId = roleId
                }, assignedByUserId);
            }
        }

        _logger.LogInformation("Bulk assigned {RoleCount} roles to user {UserId} for client {ClientId}",
            roleIds.Count, userId, clientIdInt);

        return true;
    }

    #endregion

    #region Seeding

    public async Task SeedDefaultRolesAsync(string clientId)
    {
        if (!int.TryParse(clientId, out var clientIdInt))
            throw new ArgumentException("Invalid client ID", nameof(clientId));

        // Check if roles already exist
        var existingRoles = await GetRolesAsync(clientId);
        if (existingRoles.Any())
        {
            _logger.LogInformation("Default roles already exist for client {ClientId}, skipping seed", clientIdInt);
            return;
        }

        var defaultRoles = new[]
        {
            new CreateVibeRoleRequest
            {
                Name = "admin",
                Description = "Full administrative access",
                Permissions = new List<string> { "admin:*", "read:*", "write:*", "delete:*" },
                IsSystemRole = true
            },
            new CreateVibeRoleRequest
            {
                Name = "member",
                Description = "Standard member access",
                Permissions = new List<string> { "read:own", "write:own" },
                IsSystemRole = true
            },
            new CreateVibeRoleRequest
            {
                Name = "viewer",
                Description = "Read-only access",
                Permissions = new List<string> { "read:own" },
                IsSystemRole = true
            }
        };

        foreach (var role in defaultRoles)
        {
            await CreateRoleAsync(clientId, role);
        }

        _logger.LogInformation("Seeded {RoleCount} default roles for client {ClientId}",
            defaultRoles.Length, clientIdInt);
    }

    #endregion

    #region Mapping Helpers

    private static VibeRoleDto MapToRoleDto(VibeDocument doc)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(doc.Data ?? "{}")
            ?? new Dictionary<string, JsonElement>();

        return new VibeRoleDto
        {
            Id = doc.DocumentId.ToString(),
            Name = data.TryGetValue("name", out var name) ? name.GetString() ?? "" : "",
            Description = data.TryGetValue("description", out var desc) ? desc.GetString() : null,
            Permissions = data.TryGetValue("permissions", out var perms)
                ? JsonSerializer.Deserialize<List<string>>(perms.GetRawText()) ?? new List<string>()
                : new List<string>(),
            IsSystemRole = data.TryGetValue("is_system_role", out var isSys) && isSys.GetBoolean(),
            CreatedAt = doc.CreatedAt.UtcDateTime,
            UpdatedAt = doc.UpdatedAt?.UtcDateTime,
            CreatedBy = doc.CreatedBy
        };
    }

    private static UserVibeRoleDto MapToUserRoleDto(VibeDocument doc)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(doc.Data ?? "{}")
            ?? new Dictionary<string, JsonElement>();

        return new UserVibeRoleDto
        {
            Id = doc.DocumentId.ToString(),
            UserId = data.TryGetValue("user_id", out var uid) ? uid.GetString() ?? doc.OwnerUserId?.ToString() ?? "" : doc.OwnerUserId?.ToString() ?? "",
            RoleId = data.TryGetValue("role_id", out var rid) ? rid.GetString() ?? "" : "",
            AssignedAt = data.TryGetValue("assigned_at", out var at) && at.TryGetDateTimeOffset(out var dto)
                ? dto.UtcDateTime
                : doc.CreatedAt.UtcDateTime,
            AssignedBy = data.TryGetValue("assigned_by", out var ab) && ab.TryGetInt32(out var abInt)
                ? abInt
                : doc.CreatedBy
        };
    }

    #endregion
}
