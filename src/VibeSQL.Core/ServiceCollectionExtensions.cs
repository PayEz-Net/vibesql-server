using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Options;
using VibeSQL.Core.Services;
using VibeSQL.Core.Sentinel;
using VibeSQL.Sentinel;

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

    /// <summary>
    /// Adds VibeSQL Schema Sentinel services to the DI container.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Application configuration for binding VibeSentinelOptions</param>
    public static IServiceCollection AddVibeSentinelServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<VibeSentinelOptions>(
            configuration.GetSection("VibeSQL:Sentinel"));

        // Core Sentinel services (singletons - stateless)
        services.AddSingleton<ISchemaDiffEngine, SchemaDiffEngine>();
        services.AddSingleton<IChangeClassifier, ChangeClassifier>();

        // IDataInspector is optional - default to PostgresTableInspector
        // Consumers can replace this with their own implementation
        services.TryAddSingleton<IDataInspector>(sp =>
        {
            var connectionString = configuration.GetConnectionString("VibeDb")
                ?? throw new InvalidOperationException("VibeDb connection string not configured for Schema Sentinel");
            var options = sp.GetRequiredService<IOptions<VibeSentinelOptions>>();
            var logger = sp.GetService<ILogger<PostgresTableInspector>>();
            return new PostgresTableInspector(connectionString, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds VibeSQL Schema Sentinel services to the DI container with a custom inspector.
    /// Use this when you need to inspect data outside of standard PostgreSQL tables
    /// (e.g., JSONB document storage in vibe.documents).
    /// </summary>
    /// <typeparam name="TInspector">Custom IDataInspector implementation</typeparam>
    public static IServiceCollection AddVibeSentinelServices<TInspector>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TInspector : class, IDataInspector
    {
        // Bind configuration
        services.Configure<VibeSentinelOptions>(
            configuration.GetSection("VibeSQL:Sentinel"));

        // Core Sentinel services
        services.AddSingleton<ISchemaDiffEngine, SchemaDiffEngine>();
        services.AddSingleton<IChangeClassifier, ChangeClassifier>();

        // Use custom inspector
        services.AddSingleton<IDataInspector, TInspector>();

        return services;
    }
}
