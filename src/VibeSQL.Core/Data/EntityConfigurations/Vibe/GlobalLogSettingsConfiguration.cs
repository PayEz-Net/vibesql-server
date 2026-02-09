using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class GlobalLogSettingsConfiguration : IEntityTypeConfiguration<GlobalLogSettings>
{
    public void Configure(EntityTypeBuilder<GlobalLogSettings> builder)
    {
        builder.ToTable("global_log_settings", "vibe");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.LevelApi)
            .HasColumnName("level_api");

        builder.Property(e => e.LevelAuth)
            .HasColumnName("level_auth");

        builder.Property(e => e.LevelDatabase)
            .HasColumnName("level_database");

        builder.Property(e => e.LevelAgent)
            .HasColumnName("level_agent");

        builder.Property(e => e.LevelSystem)
            .HasColumnName("level_system");

        builder.Property(e => e.RetentionDebugDays)
            .HasColumnName("retention_debug_days");

        builder.Property(e => e.RetentionInfoDays)
            .HasColumnName("retention_info_days");

        builder.Property(e => e.RetentionWarnDays)
            .HasColumnName("retention_warn_days");

        builder.Property(e => e.RetentionErrorDays)
            .HasColumnName("retention_error_days");

        builder.Property(e => e.RetentionCriticalDays)
            .HasColumnName("retention_critical_days");

        builder.Property(e => e.MaxSizeMb)
            .HasColumnName("max_size_mb");

        builder.Property(e => e.MaxRows)
            .HasColumnName("max_rows");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by");
    }
}
