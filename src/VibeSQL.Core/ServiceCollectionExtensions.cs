using Microsoft.Extensions.DependencyInjection;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Services;
using VibeSQL.Core.Services.AgentMail;

namespace VibeSQL.Core;

/// <summary>
/// Extension methods for registering Vibe Application services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Vibe Application services to the DI container.
    /// Requires VibeDbContext to be registered.
    /// </summary>
    public static IServiceCollection AddVibeApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IVibeRolesService, VibeRolesService>();
        services.AddScoped<IVibeSecretsService, VibeSecretsService>();
        // NOTE: IVibeSecretCacheService registered in API layer where IDistributedCacheService is available

        // Singleton for in-memory block cache (uses IServiceScopeFactory for DB access)
        services.AddSingleton<IVibeSequenceService, VibeSequenceService>();

        // Agent schema provisioning (for enabling Vibe Agents on clients)
        services.AddScoped<IAgentSchemaProvisioningService, AgentSchemaProvisioningService>();

        // Schema migration service for lazy document migration
        services.AddScoped<IVibeSchemaMigrationService, VibeSchemaMigrationService>();

        return services;
    }
}
