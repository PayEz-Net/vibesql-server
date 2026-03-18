using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Admin;

[ApiController]
[Route("v1/admin/oidc-providers/{providerKey}/roles")]
[Authorize]
[ServiceFilter(typeof(AdminPermissionFilter))]
public class RoleMappingsController : ControllerBase
{
    private readonly EdgeDbContext _db;

    public RoleMappingsController(EdgeDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List(string providerKey, CancellationToken ct)
    {
        var providerExists = await _db.OidcProviders.AnyAsync(p => p.ProviderKey == providerKey, ct);
        if (!providerExists)
            return NotFound(new { success = false, error = new { code = "PROVIDER_NOT_FOUND", message = $"Provider '{providerKey}' not found" } });

        var mappings = await _db.OidcProviderRoleMappings
            .AsNoTracking()
            .Where(m => m.ProviderKey == providerKey)
            .OrderBy(m => m.ExternalRole)
            .ToListAsync(ct);

        return Ok(new { success = true, data = mappings });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(string providerKey, int id, CancellationToken ct)
    {
        var mapping = await _db.OidcProviderRoleMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.ProviderKey == providerKey, ct);

        if (mapping is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Role mapping not found" } });

        return Ok(new { success = true, data = mapping });
    }

    [HttpPost]
    public async Task<IActionResult> Create(string providerKey, [FromBody] CreateRoleMappingRequest request, CancellationToken ct)
    {
        var providerExists = await _db.OidcProviders.AnyAsync(p => p.ProviderKey == providerKey, ct);
        if (!providerExists)
            return NotFound(new { success = false, error = new { code = "PROVIDER_NOT_FOUND", message = $"Provider '{providerKey}' not found" } });

        var duplicate = await _db.OidcProviderRoleMappings
            .AnyAsync(m => m.ProviderKey == providerKey && m.ExternalRole == request.ExternalRole, ct);
        if (duplicate)
            return Conflict(new { success = false, error = new { code = "DUPLICATE", message = $"Role mapping for '{request.ExternalRole}' already exists" } });

        if (VibePermissionLevelExtensions.Parse(request.VibePermission) == VibePermissionLevel.None
            && !request.VibePermission.Equals("none", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, error = new { code = "INVALID_PERMISSION", message = $"Invalid permission level: '{request.VibePermission}'" } });

        var mapping = new OidcProviderRoleMapping
        {
            ProviderKey = providerKey,
            ExternalRole = request.ExternalRole,
            VibePermission = request.VibePermission.ToLowerInvariant(),
            DeniedStatements = request.DeniedStatements,
            AllowedCollections = request.AllowedCollections,
            Description = request.Description
        };

        _db.OidcProviderRoleMappings.Add(mapping);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { providerKey, id = mapping.Id }, new { success = true, data = mapping });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(string providerKey, int id, [FromBody] UpdateRoleMappingRequest request, CancellationToken ct)
    {
        var mapping = await _db.OidcProviderRoleMappings
            .FirstOrDefaultAsync(m => m.Id == id && m.ProviderKey == providerKey, ct);

        if (mapping is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Role mapping not found" } });

        if (request.VibePermission is not null)
        {
            if (VibePermissionLevelExtensions.Parse(request.VibePermission) == VibePermissionLevel.None
                && !request.VibePermission.Equals("none", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, error = new { code = "INVALID_PERMISSION", message = $"Invalid permission level: '{request.VibePermission}'" } });

            mapping.VibePermission = request.VibePermission.ToLowerInvariant();
        }

        if (request.DeniedStatements is not null)
            mapping.DeniedStatements = request.DeniedStatements;

        if (request.AllowedCollections is not null)
            mapping.AllowedCollections = request.AllowedCollections;

        if (request.Description is not null)
            mapping.Description = request.Description;

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = mapping });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(string providerKey, int id, CancellationToken ct)
    {
        var mapping = await _db.OidcProviderRoleMappings
            .FirstOrDefaultAsync(m => m.Id == id && m.ProviderKey == providerKey, ct);

        if (mapping is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Role mapping not found" } });

        _db.OidcProviderRoleMappings.Remove(mapping);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }
}

public record CreateRoleMappingRequest
{
    public required string ExternalRole { get; init; }
    public required string VibePermission { get; init; }
    public string[]? DeniedStatements { get; init; }
    public string[]? AllowedCollections { get; init; }
    public string? Description { get; init; }
}

public record UpdateRoleMappingRequest
{
    public string? VibePermission { get; init; }
    public string[]? DeniedStatements { get; init; }
    public string[]? AllowedCollections { get; init; }
    public string? Description { get; init; }
}
