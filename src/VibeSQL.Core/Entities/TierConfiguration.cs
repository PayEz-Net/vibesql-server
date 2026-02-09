namespace VibeSQL.Core.Entities;

/// <summary>
/// Defines a subscription tier for a client.
/// Tier limits and features are data-driven, configurable via admin UI.
/// </summary>
public class TierConfiguration
{
    public int TierConfigurationId { get; set; }

    /// <summary>
    /// The IDP client identifier (tenant)
    /// </summary>
    public int ClientId { get; set; }

    /// <summary>
    /// Unique tier key (e.g., "free", "premium", "ultimate", "enterprise")
    /// </summary>
    public string TierKey { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown in UI (e.g., "Free Tier", "Premium Plan")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the tier
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order for UI display (lower = first)
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this is the default tier for new users
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Whether this tier is currently available
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Monthly price in cents (0 for free tier)
    /// </summary>
    public int MonthlyPriceCents { get; set; }

    /// <summary>
    /// Stripe Price ID for this tier (nullable for free tier)
    /// </summary>
    public string? StripePriceId { get; set; }

    /// <summary>
    /// JSON metadata for extensibility
    /// </summary>
    public string? Metadata { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    // Navigation properties
    public virtual ICollection<TierFeature> Features { get; set; } = new List<TierFeature>();
}
