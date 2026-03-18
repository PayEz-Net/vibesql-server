namespace VibeSQL.Edge.Data.Entities;

public class OidcProvider
{
    public string ProviderKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string DiscoveryUrl { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsBootstrap { get; set; }
    public bool AutoProvision { get; set; }
    public string? ProvisionDefaultRole { get; set; }
    public string SubjectClaimPath { get; set; } = "sub";
    public string RoleClaimPath { get; set; } = "roles";
    public string EmailClaimPath { get; set; } = "email";
    public int ClockSkewSeconds { get; set; } = 60;
    public int DisableGraceMinutes { get; set; }
    public DateTimeOffset? DisabledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<OidcProviderRoleMapping> RoleMappings { get; set; } = new List<OidcProviderRoleMapping>();
    public ICollection<OidcProviderClientMapping> ClientMappings { get; set; } = new List<OidcProviderClientMapping>();
    public ICollection<FederatedIdentity> FederatedIdentities { get; set; } = new List<FederatedIdentity>();
}
