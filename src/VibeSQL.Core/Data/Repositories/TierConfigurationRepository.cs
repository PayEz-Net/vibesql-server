using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for tier configuration and feature management.
/// </summary>
public class TierConfigurationRepository : ITierConfigurationRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<TierConfigurationRepository> _logger;

    public TierConfigurationRepository(VibeDbContext context, ILogger<TierConfigurationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<TierConfiguration>> GetAllAsync()
    {
        return await _context.TierConfigurations
            .OrderBy(t => t.SortOrder)
            .ToListAsync();
    }

    public async Task<List<TierConfiguration>> GetAllWithFeaturesAsync()
    {
        return await _context.TierConfigurations
            .Include(t => t.Features)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();
    }

    public async Task<TierConfiguration?> GetByIdAsync(int tierId)
    {
        return await _context.TierConfigurations.FindAsync(tierId);
    }

    public async Task<TierConfiguration?> GetByIdWithFeaturesAsync(int tierId)
    {
        return await _context.TierConfigurations
            .Include(t => t.Features)
            .FirstOrDefaultAsync(t => t.TierConfigurationId == tierId);
    }

    public async Task<TierConfiguration?> GetByTierKeyAsync(string tierKey)
    {
        return await _context.TierConfigurations
            .Include(t => t.Features)
            .FirstOrDefaultAsync(t => t.TierKey == tierKey);
    }

    public async Task<TierConfiguration?> GetByTierKeyAsync(int clientId, string tierKey)
    {
        return await _context.TierConfigurations
            .Include(t => t.Features)
            .FirstOrDefaultAsync(t => t.ClientId == clientId && t.TierKey == tierKey);
    }

    public async Task<TierConfiguration?> GetByStripePriceIdAsync(string stripePriceId)
    {
        return await _context.TierConfigurations
            .Include(t => t.Features)
            .FirstOrDefaultAsync(t => t.StripePriceId == stripePriceId);
    }

    public async Task<TierConfiguration?> GetDefaultTierAsync()
    {
        return await _context.TierConfigurations
            .Include(t => t.Features)
            .FirstOrDefaultAsync(t => t.IsDefault);
    }

    public async Task<TierConfiguration> CreateAsync(TierConfiguration tier)
    {
        tier.CreatedAt = DateTimeOffset.UtcNow;
        _context.TierConfigurations.Add(tier);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_TIER_CREATED: TierKey={TierKey}, DisplayName={DisplayName}",
            tier.TierKey, tier.DisplayName);

        return tier;
    }

    public async Task<bool> UpdateAsync(TierConfiguration tier)
    {
        tier.UpdatedAt = DateTimeOffset.UtcNow;
        _context.TierConfigurations.Update(tier);
        var result = await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_TIER_UPDATED: TierKey={TierKey}", tier.TierKey);

        return result > 0;
    }

    public async Task<bool> DeleteAsync(int tierId)
    {
        var tier = await _context.TierConfigurations
            .Include(t => t.Features)
            .FirstOrDefaultAsync(t => t.TierConfigurationId == tierId);

        if (tier == null) return false;

        _context.TierConfigurations.Remove(tier);
        var result = await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_TIER_DELETED: TierId={TierId}, TierKey={TierKey}", tierId, tier.TierKey);

        return result > 0;
    }

    public async Task SetDefaultTierAsync(int tierId)
    {
        // Clear all existing defaults
        var defaultTiers = await _context.TierConfigurations
            .Where(t => t.IsDefault)
            .ToListAsync();

        foreach (var tier in defaultTiers)
        {
            tier.IsDefault = false;
            tier.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // Set new default
        var newDefault = await _context.TierConfigurations.FindAsync(tierId);
        if (newDefault != null)
        {
            newDefault.IsDefault = true;
            newDefault.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_TIER_DEFAULT_SET: TierId={TierId}", tierId);
    }

    public async Task<bool> TierKeyExistsAsync(string tierKey, int? excludeTierId = null)
    {
        var query = _context.TierConfigurations.Where(t => t.TierKey == tierKey);

        if (excludeTierId.HasValue)
            query = query.Where(t => t.TierConfigurationId != excludeTierId.Value);

        return await query.AnyAsync();
    }

    public async Task<TierFeature?> GetFeatureByIdAsync(int featureId)
    {
        return await _context.TierFeatures.FindAsync(featureId);
    }

    public async Task<bool> DeleteFeatureAsync(int featureId)
    {
        var feature = await _context.TierFeatures.FindAsync(featureId);
        if (feature == null) return false;

        _context.TierFeatures.Remove(feature);
        var result = await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_TIER_FEATURE_DELETED: FeatureId={FeatureId}", featureId);

        return result > 0;
    }
}
