using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", "vibe");

        builder.HasKey(a => a.AuditLogId)
            .HasName("audit_logs_pkey");

        builder.Property(a => a.AuditLogId)
            .HasColumnName("audit_log_id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.ClientId)
            .HasColumnName("client_id")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(a => a.AdminUserId)
            .HasColumnName("admin_user_id")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(a => a.AdminEmail)
            .HasColumnName("admin_email")
            .HasColumnType("varchar(255)")
            .HasMaxLength(255);

        builder.Property(a => a.Category)
            .HasColumnName("category")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Action)
            .HasColumnName("action")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.TargetType)
            .HasColumnName("target_type")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50);

        builder.Property(a => a.TargetId)
            .HasColumnName("target_id")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100);

        builder.Property(a => a.Description)
            .HasColumnName("description")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(a => a.PreviousValue)
            .HasColumnName("previous_value")
            .HasColumnType("jsonb");

        builder.Property(a => a.NewValue)
            .HasColumnName("new_value")
            .HasColumnType("jsonb");

        builder.Property(a => a.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(a => a.IpAddress)
            .HasColumnName("ip_address")
            .HasColumnType("varchar(45)")
            .HasMaxLength(45);

        builder.Property(a => a.UserAgent)
            .HasColumnName("user_agent")
            .HasColumnType("varchar(500)")
            .HasMaxLength(500);

        builder.Property(a => a.RequestPath)
            .HasColumnName("request_path")
            .HasColumnType("varchar(500)")
            .HasMaxLength(500);

        builder.Property(a => a.HttpMethod)
            .HasColumnName("http_method")
            .HasColumnType("varchar(10)")
            .HasMaxLength(10);

        builder.Property(a => a.IsSuccess)
            .HasColumnName("is_success")
            .HasColumnType("boolean")
            .HasDefaultValue(true);

        builder.Property(a => a.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Indexes for common queries
        builder.HasIndex(a => a.ClientId)
            .HasDatabaseName("idx_audit_logs_client");

        builder.HasIndex(a => a.AdminUserId)
            .HasDatabaseName("idx_audit_logs_admin_user");

        builder.HasIndex(a => a.CreatedAt)
            .HasDatabaseName("idx_audit_logs_created_at");

        builder.HasIndex(a => new { a.ClientId, a.CreatedAt })
            .HasDatabaseName("idx_audit_logs_client_created");

        builder.HasIndex(a => new { a.ClientId, a.Category })
            .HasDatabaseName("idx_audit_logs_client_category");

        builder.HasIndex(a => new { a.ClientId, a.TargetType, a.TargetId })
            .HasDatabaseName("idx_audit_logs_client_target");
    }
}
