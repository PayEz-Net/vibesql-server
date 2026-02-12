namespace VibeSQL.Edge.Data.Entities;

public class OidcProviderRoleMapping
{
    public int Id { get; set; }
    public string ProviderKey { get; set; } = string.Empty;
    public string ExternalRole { get; set; } = string.Empty;
    public string VibePermission { get; set; } = "none";
    public string[]? DeniedStatements { get; set; }
    public string[]? AllowedCollections { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public OidcProvider Provider { get; set; } = null!;

    public VibePermissionLevel GetPermissionLevel() => VibePermissionLevelExtensions.Parse(VibePermission);
}
