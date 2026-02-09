namespace VibeSQL.Core.Entities;

/// <summary>
/// Defines a feature limit for a specific tier.
/// All limits are data-driven, configurable via admin UI.
/// </summary>
public class TierFeature
{
    public int TierFeatureId { get; set; }

    /// <summary>
    /// Foreign key to the tier configuration
    /// </summary>
    public int TierConfigurationId { get; set; }

    /// <summary>
    /// Unique feature key (e.g., "resume_generations", "vault_slots", "ai_analysis")
    /// </summary>
    public string FeatureKey { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the feature (e.g., "Resume Generations", "Vault Storage Slots")
    /// </summary>
    public string FeatureName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this feature is enabled for the tier
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Usage limit value. -1 = unlimited, 0 = disabled, positive = limit
    /// </summary>
    public int LimitValue { get; set; }

    /// <summary>
    /// Period for limit reset: "daily", "weekly", "monthly", "lifetime", or null for no period
    /// </summary>
    public string? LimitPeriod { get; set; }

    /// <summary>
    /// Optional description of the feature
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order for UI display
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// JSON metadata for extensibility
    /// </summary>
    public string? Metadata { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    // Navigation property
    public virtual TierConfiguration? TierConfiguration { get; set; }
}
