using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Admin;

[ApiController]
[Route("v1/admin/oidc-providers/{providerKey}/clients")]
[Authorize]
[ServiceFilter(typeof(AdminPermissionFilter))]
public class ClientMappingsController : ControllerBase
{
    private readonly EdgeDbContext _db;

    public ClientMappingsController(EdgeDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List(string providerKey, CancellationToken ct)
    {
        var providerExists = await _db.OidcProviders.AnyAsync(p => p.ProviderKey == providerKey, ct);
        if (!providerExists)
            return NotFound(new { success = false, error = new { code = "PROVIDER_NOT_FOUND", message = $"Provider '{providerKey}' not found" } });

        var mappings = await _db.OidcProviderClientMappings
            .AsNoTracking()
            .Where(m => m.ProviderKey == providerKey)
            .OrderBy(m => m.VibeClientId)
            .ToListAsync(ct);

        return Ok(new { success = true, data = mappings });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(string providerKey, int id, CancellationToken ct)
    {
        var mapping = await _db.OidcProviderClientMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.ProviderKey == providerKey, ct);

        if (mapping is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Client mapping not found" } });

        return Ok(new { success = true, data = mapping });
    }

    [HttpPost]
    public async Task<IActionResult> Create(string providerKey, [FromBody] CreateClientMappingRequest request, CancellationToken ct)
    {
        var providerExists = await _db.OidcProviders.AnyAsync(p => p.ProviderKey == providerKey, ct);
        if (!providerExists)
            return NotFound(new { success = false, error = new { code = "PROVIDER_NOT_FOUND", message = $"Provider '{providerKey}' not found" } });

        var duplicate = await _db.OidcProviderClientMappings
            .AnyAsync(m => m.ProviderKey == providerKey && m.VibeClientId == request.VibeClientId, ct);
        if (duplicate)
            return Conflict(new { success = false, error = new { code = "DUPLICATE", message = $"Client mapping for '{request.VibeClientId}' already exists" } });

        if (VibePermissionLevelExtensions.Parse(request.MaxPermission) == VibePermissionLevel.None
            && !request.MaxPermission.Equals("none", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, error = new { code = "INVALID_PERMISSION", message = $"Invalid permission level: '{request.MaxPermission}'" } });

        var mapping = new OidcProviderClientMapping
        {
            ProviderKey = providerKey,
            VibeClientId = request.VibeClientId,
            IsActive = request.IsActive,
            MaxPermission = request.MaxPermission.ToLowerInvariant()
        };

        _db.OidcProviderClientMappings.Add(mapping);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { providerKey, id = mapping.Id }, new { success = true, data = mapping });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(string providerKey, int id, [FromBody] UpdateClientMappingRequest request, CancellationToken ct)
    {
        var mapping = await _db.OidcProviderClientMappings
            .FirstOrDefaultAsync(m => m.Id == id && m.ProviderKey == providerKey, ct);

        if (mapping is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Client mapping not found" } });

        if (request.IsActive.HasValue)
            mapping.IsActive = request.IsActive.Value;

        if (request.MaxPermission is not null)
        {
            if (VibePermissionLevelExtensions.Parse(request.MaxPermission) == VibePermissionLevel.None
                && !request.MaxPermission.Equals("none", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, error = new { code = "INVALID_PERMISSION", message = $"Invalid permission level: '{request.MaxPermission}'" } });

            mapping.MaxPermission = request.MaxPermission.ToLowerInvariant();
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true, data = mapping });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(string providerKey, int id, CancellationToken ct)
    {
        var mapping = await _db.OidcProviderClientMappings
            .FirstOrDefaultAsync(m => m.Id == id && m.ProviderKey == providerKey, ct);

        if (mapping is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Client mapping not found" } });

        _db.OidcProviderClientMappings.Remove(mapping);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }
}

public record CreateClientMappingRequest
{
    public required string VibeClientId { get; init; }
    public bool IsActive { get; init; } = true;
    public required string MaxPermission { get; init; }
}

public record UpdateClientMappingRequest
{
    public bool? IsActive { get; init; }
    public string? MaxPermission { get; init; }
}
