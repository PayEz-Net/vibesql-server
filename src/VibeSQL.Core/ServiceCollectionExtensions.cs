using Microsoft.Extensions.DependencyInjection;
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

        return services;
    }
}
