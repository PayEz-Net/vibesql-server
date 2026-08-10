using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VibeSQL.Core;
using VibeSQL.Core.Data;
using VibeSQL.Core.Interfaces;

namespace VibeSQL.Edge.Tests;

/// <summary>
/// Card 186214: VibeSQL.Core interfaces were implemented but never registered in DI,
/// so nothing could inject them and their tables sat empty or stale
/// (feature_usage_logs and virtual_indexes 0 rows ever; tier_configurations stale
/// 2026-07-22 — measured 2026-08-08). These tests pin the registration surface of
/// <see cref="ServiceCollectionExtensions.AddVibeApplicationServices"/>: every Core
/// application service must RESOLVE from the container.
///
/// IVibeAuditLogRepository is deliberately absent from the list — it is registered on
/// the in-flight branch fix/wire-audit-log-repository and is not this card's scope.
///
/// Placement note: this exercises VibeSQL.Core from VibeSQL.Edge.Tests because master
/// has no Core test project yet and two in-flight branches each add one — adding a
/// third here would guarantee an add/add merge conflict.
///
/// A green test means RESOLVABLE, not LIVE: it does not prove any host calls
/// AddVibeApplicationServices, that VibeDbContext is registered by a host, or that
/// the resolved services have production call sites. Registration is half the fix;
/// instrumentation is the other half.
/// </summary>
public class CoreServiceResolutionTests
{
    public static IEnumerable<object[]> CoreServices =>
        new[]
        {
            typeof(IVibeSequenceService),
            typeof(IVibeSchemaMigrationService),
            typeof(IVibeDocumentRepository),
            typeof(IVibeSchemaRepository),
            typeof(IVirtualIndexRepository),
            typeof(ITierConfigurationRepository),
            typeof(IVibeUsageRepository),
            typeof(IVibeDataLogRepository),
            typeof(IVibeIndexManagementService),
        }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(CoreServices))]
    public void Core_service_resolves_from_container(Type serviceType)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddDbContext<VibeDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddVibeApplicationServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetService(serviceType);

        resolved.Should().NotBeNull(
            $"{serviceType.Name} has an implementation in VibeSQL.Core but no DI registration, " +
            "so nothing can inject it (card 186214)");
    }
}
