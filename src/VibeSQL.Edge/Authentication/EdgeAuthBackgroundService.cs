using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeSQL.Edge.Configuration;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Authentication;

public interface IProviderRefreshTrigger
{
    Task RefreshNowAsync(CancellationToken ct = default);
}

public sealed class EdgeAuthBackgroundService : BackgroundService, IProviderRefreshTrigger
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IProviderRegistry _registry;
    private readonly IDynamicSchemeRegistrar _registrar;
    private readonly IOptions<VibeEdgeOptions> _options;
    private readonly ILogger<EdgeAuthBackgroundService> _logger;

    public EdgeAuthBackgroundService(
        IServiceScopeFactory scopeFactory,
        IProviderRegistry registry,
        IDynamicSchemeRegistrar registrar,
        IOptions<VibeEdgeOptions> options,
        ILogger<EdgeAuthBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _registrar = registrar;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        await LoadProvidersAsync(stoppingToken);

        var interval = TimeSpan.FromMinutes(_options.Value.RefreshIntervalMinutes);
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await LoadProvidersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EDGE_AUTH: Error during provider refresh cycle, keeping existing providers");
            }
        }
    }

    public async Task RefreshNowAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("EDGE_AUTH: Manual refresh triggered");
        await LoadProvidersAsync(ct);
    }

    internal async Task LoadProvidersAsync(CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EdgeDbContext>();

            await SeedBootstrapProvidersAsync(db, ct);

            var dbProviders = await db.OidcProviders
                .AsNoTracking()
                .ToListAsync(ct);

            var currentSchemes = _registry.GetAll()
                .Select(p => p.SchemeId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var newSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var provider in dbProviders)
            {
                var schemeId = DynamicSchemeRegistrar.ToSchemeId(provider.ProviderKey);
                newSchemes.Add(schemeId);

                if (provider.IsActive)
                {
                    await _registrar.RegisterProviderAsync(provider);
                }
                else
                {
                    if (_registrar.IsRegistered(schemeId))
                    {
                        await _registrar.UnregisterProviderAsync(schemeId);
                        _logger.LogInformation("EDGE_AUTH: Removed disabled provider scheme {SchemeId}", schemeId);
                    }
                }
            }

            foreach (var orphan in currentSchemes.Except(newSchemes))
            {
                await _registrar.UnregisterProviderAsync(orphan);
                _logger.LogInformation("EDGE_AUTH: Removed orphan scheme {SchemeId} (no longer in DB)", orphan);
            }

            var records = dbProviders.Select(p => new ProviderRecord
            {
                ProviderKey = p.ProviderKey,
                Issuer = p.Issuer,
                SchemeId = DynamicSchemeRegistrar.ToSchemeId(p.ProviderKey),
                IsActive = p.IsActive,
                IsBootstrap = p.IsBootstrap,
                DisabledAt = p.DisabledAt,
                DisableGraceMinutes = p.DisableGraceMinutes,
                SubjectClaimPath = p.SubjectClaimPath,
                RoleClaimPath = p.RoleClaimPath,
                EmailClaimPath = p.EmailClaimPath,
                AutoProvision = p.AutoProvision,
                ProvisionDefaultRole = p.ProvisionDefaultRole
            }).ToList();

            _registry.Replace(records);

            _logger.LogInformation("EDGE_AUTH: Provider refresh complete. {Active} active, {Disabled} disabled, {Total} total",
                records.Count(r => r.IsActive),
                records.Count(r => !r.IsActive),
                records.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "EDGE_AUTH: Failed to load providers from database, keeping existing registry");
        }
    }

    private async Task SeedBootstrapProvidersAsync(EdgeDbContext db, CancellationToken ct)
    {
        var bootstraps = _options.Value.BootstrapProviders;
        if (bootstraps is not { Count: > 0 })
            return;

        foreach (var cfg in bootstraps)
        {
            var exists = await db.OidcProviders
                .AnyAsync(p => p.ProviderKey == cfg.ProviderKey, ct);

            if (exists)
                continue;

            db.OidcProviders.Add(new OidcProvider
            {
                ProviderKey = cfg.ProviderKey,
                DisplayName = cfg.DisplayName,
                Issuer = cfg.Issuer,
                DiscoveryUrl = cfg.DiscoveryUrl,
                Audience = cfg.Audience,
                IsActive = true,
                IsBootstrap = cfg.IsBootstrap,
                ClockSkewSeconds = cfg.ClockSkewSeconds
            });

            _logger.LogInformation("EDGE_AUTH: Seeding bootstrap provider {ProviderKey} from config", cfg.ProviderKey);
        }

        await db.SaveChangesAsync(ct);
    }
}