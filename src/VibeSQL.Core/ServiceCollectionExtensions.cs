using Microsoft.Extensions.DependencyInjection;
using VibeSQL.Core.Data.Repositories;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Services;

namespace VibeSQL.Core;

/// <summary>
/// Extension methods for registering VibeSQL Core services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds VibeSQL Core services to the DI container.
    /// Requires VibeDbContext to be registered.
    /// </summary>
    public static IServiceCollection AddVibeApplicationServices(this IServiceCollection services)
    {
        // Singleton for in-memory sequence block cache (uses IServiceScopeFactory for DB access)
        services.AddSingleton<IVibeSequenceService, VibeSequenceService>();

        // Schema migration service for lazy document migration
        services.AddScoped<IVibeSchemaMigrationService, VibeSchemaMigrationService>();

        // Card 186214: every repository below was implemented, entity-configured and
        // table-backed — and never registered, so nothing could inject it and the
        // tables sat empty or stale (feature_usage_logs and virtual_indexes 0 rows
        // ever; tier_configurations last write 2026-07-22 — measured 2026-08-08).
        // The first two also unblock VibeSchemaMigrationService, which was registered
        // above but UNRESOLVABLE: its constructor needs IVibeSchemaRepository and
        // IVibeDocumentRepository, so any host resolving it would have thrown.
        //
        // Registration makes a service RESOLVABLE — it does not produce rows. Most of
        // these still have no production call sites, and this extension only matters
        // if a host calls it with VibeDbContext registered. Do not read a green
        // resolution test as "the feature works"; instrumenting the call sites is
        // the other half of the fix.
        services.AddScoped<IVibeDocumentRepository, VibeDocumentRepository>();
        services.AddScoped<IVibeSchemaRepository, VibeSchemaRepository>();
        services.AddScoped<IVirtualIndexRepository, VirtualIndexRepository>();
        services.AddScoped<ITierConfigurationRepository, TierConfigurationRepository>();
        services.AddScoped<IVibeUsageRepository, VibeUsageRepository>();
        services.AddScoped<IVibeDataLogRepository, VibeDataLogRepository>();

        // Index management sits on top of IVirtualIndexRepository.
        services.AddScoped<IVibeIndexManagementService, VibeIndexManagementService>();

        return services;
    }
}
