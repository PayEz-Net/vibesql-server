namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Query result types for raw SQL queries in Vibe repositories.
/// These are keyless entity types for EF Core 6 compatibility.
/// </summary>

public class ProfileQueryResult
{
    public string? Email { get; set; }
    public string? TierKey { get; set; }
    public string? SubscriptionStatus { get; set; }
    public DateTime? TrialEnd { get; set; }
    public DateTime? SubscriptionStart { get; set; }
}

public class TierKeyResult
{
    public string? TierKey { get; set; }
}

public class CreditsResult
{
    public long AiCredits { get; set; }
    public long StorageCredits { get; set; }
}

public class SubscriptionCountResult
{
    public int Count { get; set; }
    public string? TierKey { get; set; }
}

public class RevenueTotalResult
{
    public decimal Total { get; set; }
}

public class TierDistributionRaw
{
    public string? TierKey { get; set; }
    public int UserCount { get; set; }
}

public class SubscriptionCountRaw
{
    public int Count { get; set; }
}

public class RevenueTotalRaw
{
    public decimal Total { get; set; }
}

public class UsageIncrementResult
{
    public int UsageCount { get; set; }
    public bool LimitExceeded { get; set; }
}

public class UserEmailInfo
{
    public int ClientId { get; set; }
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string TierName { get; set; } = string.Empty;
    public string? TierDisplayName { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string SupportEmail { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#2563eb";
    public string DashboardUrl { get; set; } = string.Empty;
}
