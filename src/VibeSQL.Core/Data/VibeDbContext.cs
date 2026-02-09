using System.Reflection;
using Microsoft.EntityFrameworkCore;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Data.Extensions;
using VibeSQL.Core.Data.Repositories;

namespace VibeSQL.Core.Data;

/// <summary>
/// DbContext for the vibe database - document storage, schema management, and tier tracking
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
    public virtual DbSet<GlobalLogSettings> GlobalLogSettings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Set default schema for Vibe tables
        modelBuilder.HasDefaultSchema("vibe");

        // Apply all Vibe-specific entity configurations from EntityConfigurations/Vibe folder
        modelBuilder.ApplySchemaConfigurations("Vibe");

        // Configure keyless entity types for raw SQL query results
        modelBuilder.Entity<TierKeyResult>().HasNoKey();
        modelBuilder.Entity<UsageIncrementResult>().HasNoKey();
    }
}
