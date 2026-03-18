using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VibeSQL.Edge.Authentication;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Admin;

[ApiController]
[Route("v1/admin/oidc-providers")]
[Authorize]
[ServiceFilter(typeof(AdminPermissionFilter))]
public class OidcProvidersController : ControllerBase
{
    private readonly EdgeDbContext _db;
    private readonly IProviderRegistry _registry;
    private readonly IDynamicSchemeRegistrar _registrar;
    private readonly IProviderRefreshTrigger _refreshTrigger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OidcProvidersController> _logger;

    public OidcProvidersController(
        EdgeDbContext db,
        IProviderRegistry registry,
        IDynamicSchemeRegistrar registrar,
        IProviderRefreshTrigger refreshTrigger,
        IHttpClientFactory httpClientFactory,
        ILogger<OidcProvidersController> logger)
    {
        _db = db;
        _registry = registry;
        _registrar = registrar;
        _refreshTrigger = refreshTrigger;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var providers = await _db.OidcProviders
            .AsNoTracking()
            .OrderBy(p => p.ProviderKey)
            .Select(p => new
            {
                p.ProviderKey,
                p.DisplayName,
                p.Issuer,
                p.Audience,
                p.IsActive,
                p.IsBootstrap,
                p.AutoProvision,
                p.CreatedAt,
                p.UpdatedAt
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = providers });
    }

    [HttpGet("{providerKey}")]
    public async Task<IActionResult> Get(string providerKey, CancellationToken ct)
    {
        var provider = await _db.OidcProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProviderKey == providerKey, ct);

        if (provider is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = $"Provider '{providerKey}' not found" } });

        return Ok(new { success = true, data = provider });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProviderRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ModelState } });

        var exists = await _db.OidcProviders.AnyAsync(p => p.ProviderKey == request.ProviderKey, ct);
        if (exists)
            return Conflict(new { success = false, error = new { code = "ALREADY_EXISTS", message = $"Provider '{request.ProviderKey}' already exists" } });

        var provider = new OidcProvider
        {
            ProviderKey = request.ProviderKey,
            DisplayName = request.DisplayName,
            Issuer = request.Issuer,
            DiscoveryUrl = request.DiscoveryUrl,
            Audience = request.Audience,
            IsActive = request.IsActive,
            AutoProvision = request.AutoProvision,
            ProvisionDefaultRole = request.ProvisionDefaultRole,
            SubjectClaimPath = request.SubjectClaimPath ?? "sub",
            RoleClaimPath = request.RoleClaimPath ?? "roles",
            EmailClaimPath = request.EmailClaimPath ?? "email",
            ClockSkewSeconds = request.ClockSkewSeconds ?? 60
        };

        _db.OidcProviders.Add(provider);
        await _db.SaveChangesAsync(ct);

        if (provider.IsActive)
            await _registrar.RegisterProviderAsync(provider);

        _logger.LogInformation("EDGE_ADMIN: Created provider {ProviderKey}", provider.ProviderKey);
        return CreatedAtAction(nameof(Get), new { providerKey = provider.ProviderKey }, new { success = true, data = provider });
    }

    [HttpPut("{providerKey}")]
    public async Task<IActionResult> Update(string providerKey, [FromBody] UpdateProviderRequest request, CancellationToken ct)
    {
        var provider = await _db.OidcProviders.FirstOrDefaultAsync(p => p.ProviderKey == providerKey, ct);
        if (provider is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = $"Provider '{providerKey}' not found" } });

        provider.DisplayName = request.DisplayName ?? provider.DisplayName;
        provider.Audience = request.Audience ?? provider.Audience;
        provider.AutoProvision = request.AutoProvision ?? provider.AutoProvision;
        provider.ProvisionDefaultRole = request.ProvisionDefaultRole ?? provider.ProvisionDefaultRole;
        provider.SubjectClaimPath = request.SubjectClaimPath ?? provider.SubjectClaimPath;
        provider.RoleClaimPath = request.RoleClaimPath ?? provider.RoleClaimPath;
        provider.EmailClaimPath = request.EmailClaimPath ?? provider.EmailClaimPath;
        provider.ClockSkewSeconds = request.ClockSkewSeconds ?? provider.ClockSkewSeconds;
        provider.UpdatedAt = DateTimeOffset.UtcNow;

        if (request.IsActive.HasValue && request.IsActive.Value != provider.IsActive)
        {
            provider.IsActive = request.IsActive.Value;
            if (!provider.IsActive)
            {
                provider.DisabledAt = DateTimeOffset.UtcNow;
                var schemeId = DynamicSchemeRegistrar.ToSchemeId(providerKey);
                if (_registrar.IsRegistered(schemeId))
                    await _registrar.UnregisterProviderAsync(schemeId);
            }
            else
            {
                provider.DisabledAt = null;
                await _registrar.RegisterProviderAsync(provider);
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("EDGE_ADMIN: Updated provider {ProviderKey}", providerKey);
        return Ok(new { success = true, data = provider });
    }

    [HttpDelete("{providerKey}")]
    public async Task<IActionResult> Delete(string providerKey, CancellationToken ct)
    {
        var provider = await _db.OidcProviders.FirstOrDefaultAsync(p => p.ProviderKey == providerKey, ct);
        if (provider is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = $"Provider '{providerKey}' not found" } });

        if (provider.IsBootstrap)
            return BadRequest(new { success = false, error = new { code = "BOOTSTRAP_PROTECTED", message = "Bootstrap providers cannot be deleted" } });

        var schemeId = DynamicSchemeRegistrar.ToSchemeId(providerKey);
        if (_registrar.IsRegistered(schemeId))
            await _registrar.UnregisterProviderAsync(schemeId);

        _db.OidcProviders.Remove(provider);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("EDGE_ADMIN: Deleted provider {ProviderKey}", providerKey);
        return Ok(new { success = true });
    }

    [HttpPost("{providerKey}/test")]
    public async Task<IActionResult> TestDiscovery(string providerKey, CancellationToken ct)
    {
        var provider = await _db.OidcProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProviderKey == providerKey, ct);

        if (provider is null)
            return NotFound(new { success = false, error = new { code = "NOT_FOUND", message = $"Provider '{providerKey}' not found" } });

        try
        {
            var http = _httpClientFactory.CreateClient("VibeServer");
            http.Timeout = TimeSpan.FromSeconds(10);
            var response = await http.GetAsync(provider.DiscoveryUrl, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            return Ok(new
            {
                success = true,
                data = new
                {
                    provider.ProviderKey,
                    provider.DiscoveryUrl,
                    StatusCode = (int)response.StatusCode,
                    Reachable = response.IsSuccessStatusCode,
                    BodyPreview = body.Length > 500 ? body[..500] + "..." : body
                }
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                success = false,
                data = new
                {
                    provider.ProviderKey,
                    provider.DiscoveryUrl,
                    Reachable = false,
                    Error = ex.Message
                }
            });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> ForceRefresh(CancellationToken ct)
    {
        _logger.LogInformation("EDGE_ADMIN: Manual provider refresh triggered");
        await _refreshTrigger.RefreshNowAsync(ct);
        return Ok(new { success = true, message = "Provider refresh completed" });
    }
}

public record CreateProviderRequest
{
    [Required, StringLength(50, MinimumLength = 1), RegularExpression(@"^[a-z0-9\-]+$", ErrorMessage = "ProviderKey must be lowercase alphanumeric with hyphens")]
    public required string ProviderKey { get; init; }

    [Required, StringLength(200, MinimumLength = 1)]
    public required string DisplayName { get; init; }

    [Required, Url]
    public required string Issuer { get; init; }

    [Required, Url]
    public required string DiscoveryUrl { get; init; }

    [Required, StringLength(500, MinimumLength = 1)]
    public required string Audience { get; init; }

    public bool IsActive { get; init; } = true;
    public bool AutoProvision { get; init; }

    [StringLength(100)]
    public string? ProvisionDefaultRole { get; init; }

    [StringLength(100)]
    public string? SubjectClaimPath { get; init; }

    [StringLength(100)]
    public string? RoleClaimPath { get; init; }

    [StringLength(100)]
    public string? EmailClaimPath { get; init; }

    [Range(0, 600)]
    public int? ClockSkewSeconds { get; init; }
}

public record UpdateProviderRequest
{
    [StringLength(200)]
    public string? DisplayName { get; init; }

    [StringLength(500)]
    public string? Audience { get; init; }

    public bool? IsActive { get; init; }
    public bool? AutoProvision { get; init; }

    [StringLength(100)]
    public string? ProvisionDefaultRole { get; init; }

    [StringLength(100)]
    public string? SubjectClaimPath { get; init; }

    [StringLength(100)]
    public string? RoleClaimPath { get; init; }

    [StringLength(100)]
    public string? EmailClaimPath { get; init; }

    [Range(0, 600)]
    public int? ClockSkewSeconds { get; init; }
}