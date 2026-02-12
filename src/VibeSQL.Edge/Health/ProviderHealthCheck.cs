using Microsoft.AspNetCore.Mvc;
using VibeSQL.Edge.Authentication;

namespace VibeSQL.Edge.Health;

[ApiController]
[Route("health")]
public class ProviderHealthCheck : ControllerBase
{
    private readonly IProviderRegistry _registry;
    private readonly IDynamicSchemeRegistrar _registrar;

    public ProviderHealthCheck(IProviderRegistry registry, IDynamicSchemeRegistrar registrar)
    {
        _registry = registry;
        _registrar = registrar;
    }

    [HttpGet("providers")]
    public IActionResult GetProviderHealth()
    {
        var providers = _registry.GetAll();

        var status = providers.Select(p => new
        {
            p.ProviderKey,
            p.Issuer,
            p.IsActive,
            p.IsBootstrap,
            SchemeRegistered = _registrar.IsRegistered(DynamicSchemeRegistrar.ToSchemeId(p.ProviderKey)),
            p.DisabledAt,
            p.DisableGraceMinutes
        }).ToList();

        var allHealthy = status.All(s => s.IsActive && s.SchemeRegistered);

        return Ok(new
        {
            success = true,
            healthy = allHealthy,
            providers = status,
            totalProviders = status.Count,
            activeProviders = status.Count(s => s.IsActive),
            disabledProviders = status.Count(s => !s.IsActive)
        });
    }
}