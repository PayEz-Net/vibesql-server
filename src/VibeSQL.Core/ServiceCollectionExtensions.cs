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

        // Mode B cross-schema reference-constraint validator (docs/cross-schema-reference-constraints.md,
        // card 186212 / Sentinel M-201). Required by VibeDocumentRepository.
        services.AddScoped<IClientReferenceValidator, ClientReferenceValidator>();

        // Document repository - the guarded write path for vibe.documents (card 186212 /
        // M-201). CreateAsync refuses a write naming a non-existent client_id; registering
        // it here is what makes that guard reachable from any host consumer.
        services.AddScoped<IVibeDocumentRepository, VibeDocumentRepository>();

        return services;
    }
}
