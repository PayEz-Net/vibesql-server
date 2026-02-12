using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VibeSQL.Edge.Data;

namespace VibeSQL.Edge.Admin;

[ApiController]
[Route("v1/admin/federated-identities")]
[Authorize]
[ServiceFilter(typeof(AdminPermissionFilter))]
public class FederatedIdentitiesController : ControllerBase
{
    private readonly EdgeDbContext _db;

    public FederatedIdentitiesController(EdgeDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? providerKey,
        [FromQuery] int? vibeUserId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        var query = _db.FederatedIdentities.AsNoTracking().AsQueryable();

        if (providerKey is not null)
            query = query.Where(f => f.ProviderKey == providerKey);

        if (vibeUserId.HasValue)
            query = query.Where(f => f.VibeUserId == vibeUserId.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(f => f.LastSeenAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new
            {
                f.Id,
                f.ProviderKey,
                f.ExternalSubject,
                f.VibeUserId,
                f.Email,
                f.DisplayName,
                f.FirstSeenAt,
                f.LastSeenAt,
                f.IsActive
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = items, total, page, pageSize });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var identity = await _db.FederatedIdentities
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, ct);

        if (identity is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Federated identity not found" } });

        return Ok(new { success = true, data = identity });
    }

    [HttpPatch("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var identity = await _db.FederatedIdentities.FirstOrDefaultAsync(f => f.Id == id, ct);

        if (identity is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Federated identity not found" } });

        identity.IsActive = false;
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, data = new { identity.Id, identity.IsActive } });
    }

    [HttpPatch("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        var identity = await _db.FederatedIdentities.FirstOrDefaultAsync(f => f.Id == id, ct);

        if (identity is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = "Federated identity not found" } });

        identity.IsActive = true;
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, data = new { identity.Id, identity.IsActive } });
    }
}
