namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Query result types for raw SQL queries in Vibe repositories.
/// These are keyless entity types registered in VibeDbContext.
/// </summary>

public class TierKeyResult
{
    public string? TierKey { get; set; }
}

public class UsageIncrementResult
{
    public int UsageCount { get; set; }
    public bool LimitExceeded { get; set; }
}
