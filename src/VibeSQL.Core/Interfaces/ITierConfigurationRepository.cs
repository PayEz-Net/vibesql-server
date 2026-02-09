using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for tier configuration and feature management.
/// </summary>
public interface ITierConfigurationRepository
{
    /// <summary>
    /// Get all tier configurations
    /// </summary>
    Task<List<TierConfiguration>> GetAllAsync();

    /// <summary>
    /// Get all tier configurations with their features
    /// </summary>
    Task<List<TierConfiguration>> GetAllWithFeaturesAsync();

    /// <summary>
    /// Get a tier by ID
    /// </summary>
    Task<TierConfiguration?> GetByIdAsync(int tierId);

    /// <summary>
    /// Get a tier by ID with features
    /// </summary>
    Task<TierConfiguration?> GetByIdWithFeaturesAsync(int tierId);

    /// <summary>
    /// Get a tier by tier key
    /// </summary>
    Task<TierConfiguration?> GetByTierKeyAsync(string tierKey);

    /// <summary>
    /// Get a tier by tier key and client ID
    /// </summary>
    Task<TierConfiguration?> GetByTierKeyAsync(int clientId, string tierKey);

    /// <summary>
    /// Get a tier by Stripe price ID
    /// </summary>
    Task<TierConfiguration?> GetByStripePriceIdAsync(string stripePriceId);

    /// <summary>
    /// Get the default tier
    /// </summary>
    Task<TierConfiguration?> GetDefaultTierAsync();

    /// <summary>
    /// Create a new tier configuration
    /// </summary>
    Task<TierConfiguration> CreateAsync(TierConfiguration tier);

    /// <summary>
    /// Update a tier configuration
    /// </summary>
    Task<bool> UpdateAsync(TierConfiguration tier);

    /// <summary>
    /// Delete a tier configuration
    /// </summary>
    Task<bool> DeleteAsync(int tierId);

    /// <summary>
    /// Set a tier as default (and unset others)
    /// </summary>
    Task SetDefaultTierAsync(int tierId);

    /// <summary>
    /// Check if a tier key exists
    /// </summary>
    Task<bool> TierKeyExistsAsync(string tierKey, int? excludeTierId = null);

    /// <summary>
    /// Get a tier feature by ID
    /// </summary>
    Task<TierFeature?> GetFeatureByIdAsync(int featureId);

    /// <summary>
    /// Delete a tier feature
    /// </summary>
    Task<bool> DeleteFeatureAsync(int featureId);
}
