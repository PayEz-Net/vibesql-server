namespace VibeSQL.Edge.Configuration;

public class VibeEdgeOptions
{
    public const string SectionName = "VibeEdge";

    public string ServerUrl { get; set; } = "http://localhost:52411";
    public string? HmacSecret { get; set; }
    public int RefreshIntervalMinutes { get; set; } = 30;
    public List<BootstrapProviderConfig> BootstrapProviders { get; set; } = new();
}

public class BootstrapProviderConfig
{
    public string ProviderKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string DiscoveryUrl { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public bool IsBootstrap { get; set; }
    public int ClockSkewSeconds { get; set; } = 60;
}
