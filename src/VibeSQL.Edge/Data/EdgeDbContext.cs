using Microsoft.EntityFrameworkCore;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Data;

public class EdgeDbContext : DbContext
{
    public EdgeDbContext(DbContextOptions<EdgeDbContext> options)
        : base(options)
    {
    }

    public DbSet<OidcProvider> OidcProviders { get; set; } = null!;
    public DbSet<OidcProviderRoleMapping> OidcProviderRoleMappings { get; set; } = null!;
    public DbSet<OidcProviderClientMapping> OidcProviderClientMappings { get; set; } = null!;
    public DbSet<FederatedIdentity> FederatedIdentities { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("vibe_system");

        modelBuilder.Entity<OidcProvider>(entity =>
        {
            entity.ToTable("oidc_providers");
            entity.HasKey(e => e.ProviderKey).HasName("oidc_providers_pkey");

            entity.Property(e => e.ProviderKey).HasColumnName("provider_key").HasColumnType("varchar(50)").HasMaxLength(50);
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Issuer).HasColumnName("issuer").HasColumnType("varchar(500)").HasMaxLength(500).IsRequired();
            entity.HasIndex(e => e.Issuer).IsUnique().HasDatabaseName("idx_oidc_providers_issuer");
            entity.Property(e => e.DiscoveryUrl).HasColumnName("discovery_url").HasColumnType("varchar(500)").HasMaxLength(500).IsRequired();
            entity.Property(e => e.Audience).HasColumnName("audience").HasColumnType("varchar(500)").HasMaxLength(500).IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasColumnType("boolean").HasDefaultValue(true);
            entity.Property(e => e.IsBootstrap).HasColumnName("is_bootstrap").HasColumnType("boolean").HasDefaultValue(false);
            entity.Property(e => e.AutoProvision).HasColumnName("auto_provision").HasColumnType("boolean").HasDefaultValue(false);
            entity.Property(e => e.ProvisionDefaultRole).HasColumnName("provision_default_role").HasColumnType("varchar(100)").HasMaxLength(100);
            entity.Property(e => e.SubjectClaimPath).HasColumnName("subject_claim_path").HasColumnType("varchar(100)").HasMaxLength(100).HasDefaultValue("sub");
            entity.Property(e => e.RoleClaimPath).HasColumnName("role_claim_path").HasColumnType("varchar(100)").HasMaxLength(100).HasDefaultValue("roles");
            entity.Property(e => e.EmailClaimPath).HasColumnName("email_claim_path").HasColumnType("varchar(100)").HasMaxLength(100).HasDefaultValue("email");
            entity.Property(e => e.ClockSkewSeconds).HasColumnName("clock_skew_seconds").HasColumnType("integer").HasDefaultValue(60);
            entity.Property(e => e.DisableGraceMinutes).HasColumnName("disable_grace_minutes").HasColumnType("integer").HasDefaultValue(0);
            entity.Property(e => e.DisabledAt).HasColumnName("disabled_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired().HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired().HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<OidcProviderRoleMapping>(entity =>
        {
            entity.ToTable("oidc_provider_role_mappings");
            entity.HasKey(e => e.Id).HasName("oidc_provider_role_mappings_pkey");

            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("integer").ValueGeneratedOnAdd();
            entity.Property(e => e.ProviderKey).HasColumnName("provider_key").HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ExternalRole).HasColumnName("external_role").HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
            entity.Property(e => e.VibePermission).HasColumnName("vibe_permission").HasColumnType("varchar(20)").HasMaxLength(20).IsRequired();
            entity.Property(e => e.DeniedStatements).HasColumnName("denied_statements").HasColumnType("text[]");
            entity.Property(e => e.AllowedCollections).HasColumnName("allowed_collections").HasColumnType("text[]");
            entity.Property(e => e.Description).HasColumnName("description").HasColumnType("varchar(500)").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired().HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.ProviderKey, e.ExternalRole }).IsUnique().HasDatabaseName("uq_provider_role");
            entity.HasOne(e => e.Provider)
                .WithMany(p => p.RoleMappings)
                .HasForeignKey(e => e.ProviderKey)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OidcProviderClientMapping>(entity =>
        {
            entity.ToTable("oidc_provider_client_mappings");
            entity.HasKey(e => e.Id).HasName("oidc_provider_client_mappings_pkey");

            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("integer").ValueGeneratedOnAdd();
            entity.Property(e => e.ProviderKey).HasColumnName("provider_key").HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
            entity.Property(e => e.VibeClientId).HasColumnName("vibe_client_id").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasColumnType("boolean").HasDefaultValue(true);
            entity.Property(e => e.MaxPermission).HasColumnName("max_permission").HasColumnType("varchar(20)").HasMaxLength(20).HasDefaultValue("write");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired().HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.ProviderKey, e.VibeClientId }).IsUnique().HasDatabaseName("uq_provider_client");
            entity.HasOne(e => e.Provider)
                .WithMany(p => p.ClientMappings)
                .HasForeignKey(e => e.ProviderKey)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FederatedIdentity>(entity =>
        {
            entity.ToTable("federated_identities");
            entity.HasKey(e => e.Id).HasName("federated_identities_pkey");

            entity.Property(e => e.Id).HasColumnName("id").HasColumnType("integer").ValueGeneratedOnAdd();
            entity.Property(e => e.ProviderKey).HasColumnName("provider_key").HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ExternalSubject).HasColumnName("external_subject").HasColumnType("varchar(255)").HasMaxLength(255).IsRequired();
            entity.Property(e => e.VibeUserId).HasColumnName("vibe_user_id").HasColumnType("integer").IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasColumnType("varchar(255)").HasMaxLength(255);
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasColumnType("varchar(255)").HasMaxLength(255);
            entity.Property(e => e.FirstSeenAt).HasColumnName("first_seen_at").HasColumnType("timestamp with time zone").IsRequired().HasDefaultValueSql("now()");
            entity.Property(e => e.LastSeenAt).HasColumnName("last_seen_at").HasColumnType("timestamp with time zone").IsRequired().HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasColumnType("boolean").HasDefaultValue(true);
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");

            entity.HasIndex(e => new { e.ProviderKey, e.ExternalSubject }).IsUnique().HasDatabaseName("idx_federated_lookup");
            entity.HasIndex(e => e.VibeUserId).HasDatabaseName("idx_federated_vibe_user");
            entity.HasOne(e => e.Provider)
                .WithMany(p => p.FederatedIdentities)
                .HasForeignKey(e => e.ProviderKey)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
