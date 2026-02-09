using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Data.EntityConfigurations.Vibe;

public class EmailPreferencesConfiguration : IEntityTypeConfiguration<EmailPreferences>
{
    public void Configure(EntityTypeBuilder<EmailPreferences> builder)
    {
        builder.ToTable("email_preferences", "vibe");

        builder.HasKey(e => e.EmailPreferencesId)
            .HasName("email_preferences_pkey");

        builder.Property(e => e.EmailPreferencesId)
            .HasColumnName("email_preferences_id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ClientId)
            .HasColumnName("client_id")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.WelcomeEmails)
            .HasColumnName("welcome_emails")
            .HasColumnType("boolean")
            .HasDefaultValue(true);

        builder.Property(e => e.PaymentReceipts)
            .HasColumnName("payment_receipts")
            .HasColumnType("boolean")
            .HasDefaultValue(true);

        builder.Property(e => e.UsageWarnings)
            .HasColumnName("usage_warnings")
            .HasColumnType("boolean")
            .HasDefaultValue(true);

        builder.Property(e => e.TrialReminders)
            .HasColumnName("trial_reminders")
            .HasColumnType("boolean")
            .HasDefaultValue(true);

        builder.Property(e => e.SecurityAlerts)
            .HasColumnName("security_alerts")
            .HasColumnType("boolean")
            .HasDefaultValue(true);

        builder.Property(e => e.MarketingEmails)
            .HasColumnName("marketing_emails")
            .HasColumnType("boolean")
            .HasDefaultValue(false);

        builder.Property(e => e.ProductUpdates)
            .HasColumnName("product_updates")
            .HasColumnType("boolean")
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        // Unique constraint: one preferences record per user per client
        builder.HasIndex(e => new { e.ClientId, e.UserId })
            .IsUnique()
            .HasDatabaseName("idx_email_preferences_client_user");
    }
}
