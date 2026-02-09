namespace VibeSQL.Core.Models;

/// <summary>
/// Configuration model for query limits
/// </summary>
public class VibeQueryLimitsConfig
{
    public int MaxResultRows { get; set; } = 1000;
    public int MaxQuerySizeBytes { get; set; } = 10 * 1024;
}

/// <summary>
/// Configuration model for query timeouts by tier
/// </summary>
public class VibeQueryTimeoutsConfig
{
    public int FreeSeconds { get; set; } = 2;
    public int StarterSeconds { get; set; } = 5;
    public int ProSeconds { get; set; } = 10;
    public int EnterpriseSeconds { get; set; } = 30;
    public int DefaultSeconds { get; set; } = 5;
}
