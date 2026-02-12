namespace VibeSQL.Edge.Data.Entities;

public class FederatedIdentity
{
    public int Id { get; set; }
    public string ProviderKey { get; set; } = string.Empty;
    public string ExternalSubject { get; set; } = string.Empty;
    public int VibeUserId { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;
    public string? Metadata { get; set; }

    public OidcProvider Provider { get; set; } = null!;
}
