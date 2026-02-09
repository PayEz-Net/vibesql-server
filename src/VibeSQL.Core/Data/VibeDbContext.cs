using System.Reflection;
using Microsoft.EntityFrameworkCore;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Data.Extensions;
using VibeSQL.Core.Data.Repositories;

namespace VibeSQL.Core.Data;

/// <summary>
/// DbContext for the vibe database - user data storage for MVP sites
/// </summary>
public class VibeDbContext : DbContext
{
    public VibeDbContext(DbContextOptions<VibeDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<VibeDocument> Documents { get; set; } = null!;
    public virtual DbSet<VibeCollectionSchema> CollectionSchemas { get; set; } = null!;
    public virtual DbSet<VirtualIndex> VirtualIndexes { get; set; } = null!;
    public virtual DbSet<VibeEncryptedValueOwnership> EncryptedValueOwnerships { get; set; } = null!;
    public virtual DbSet<TierConfiguration> TierConfigurations { get; set; } = null!;
    public virtual DbSet<TierFeature> TierFeatures { get; set; } = null!;
    public virtual DbSet<FeatureUsageLog> FeatureUsageLogs { get; set; } = null!;
    public virtual DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public virtual DbSet<EmailPreferences> EmailPreferences { get; set; } = null!;
    public virtual DbSet<AccessControlConfig> AccessControlConfigs { get; set; } = null!;
    public virtual DbSet<EmailAccessListEntry> EmailAccessListEntries { get; set; } = null!;
    public virtual DbSet<PagePermission> PagePermissions { get; set; } = null!;
    public virtual DbSet<PageRoleRequirement> PageRoleRequirements { get; set; } = null!;
    public virtual DbSet<PagePermissionOverride> PagePermissionOverrides { get; set; } = null!;
    public virtual DbSet<PageClaimRequirement> PageClaimRequirements { get; set; } = null!;
    public virtual DbSet<GlobalLogSettings> GlobalLogSettings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Set default schema for Vibe tables
        modelBuilder.HasDefaultSchema("vibe");

        // Apply all Vibe-specific entity configurations from EntityConfigurations/Vibe folder
        modelBuilder.ApplySchemaConfigurations("Vibe");

        // Configure keyless entity types for raw SQL query results (EF Core 6 compatibility)
        modelBuilder.Entity<ProfileQueryResult>().HasNoKey();
        modelBuilder.Entity<TierKeyResult>().HasNoKey();
        modelBuilder.Entity<CreditsResult>().HasNoKey();
        modelBuilder.Entity<SubscriptionCountResult>().HasNoKey();
        modelBuilder.Entity<RevenueTotalResult>().HasNoKey();
        modelBuilder.Entity<TierDistributionRaw>().HasNoKey();
        modelBuilder.Entity<SubscriptionCountRaw>().HasNoKey();
        modelBuilder.Entity<RevenueTotalRaw>().HasNoKey();
        modelBuilder.Entity<UsageIncrementResult>().HasNoKey();
        modelBuilder.Entity<UserEmailInfo>().HasNoKey();
    }
}
