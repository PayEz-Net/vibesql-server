namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service interface for managing site_secrets in vibe_app collection.
/// Used for auto-storing Vibe credentials and other sensitive configuration.
/// </summary>
public interface IVibeSecretsService
{
    /// <summary>
    /// Ensures vibe_app schema exists for the client, creating it if needed.
    /// </summary>
    Task<bool> EnsureVibeAppSchemaExistsAsync(int clientId);

    /// <summary>
    /// Stores or updates a secret in the site_secrets table.
    /// Value is stored encrypted via x-vibe-tokenize.
    /// </summary>
    Task<bool> StoreSecretAsync(int clientId, string secretName, string secretValue, int? createdByUserId = null);

    /// <summary>
    /// Gets a secret by name. Returns null if not found.
    /// Value is auto-decrypted.
    /// </summary>
    Task<VibeSecretDto?> GetSecretAsync(int clientId, string secretName);

    /// <summary>
    /// Deletes a secret by name.
    /// </summary>
    Task<bool> DeleteSecretAsync(int clientId, string secretName);
}

/// <summary>
/// Secret data from site_secrets table
/// </summary>
public class VibeSecretDto
{
    public int Id { get; set; }
    public string SecretName { get; set; } = string.Empty;
    /// <summary>
    /// The decrypted secret value (plaintext)
    /// </summary>
    public string SecretValue { get; set; } = string.Empty;
    /// <summary>
    /// Encryption key ID used (for key rotation tracking)
    /// </summary>
    public int? EncKeyId { get; set; }
    /// <summary>
    /// When the encryption key expires (for re-encryption planning)
    /// </summary>
    public DateTime? ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
}
