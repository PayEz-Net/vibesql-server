using System;

namespace VibeSQL.Core.Entities;

/// <summary>
/// Tracks ownership of encrypted values for cross-tenant decryption prevention.
/// Stores a hash of the ciphertext mapped to the owning client.
/// </summary>
public class VibeEncryptedValueOwnership
{
    public int Id { get; set; }

    /// <summary>
    /// SHA256 hash of the ciphertext (base64 encoded)
    /// </summary>
    public string CiphertextHash { get; set; } = string.Empty;

    /// <summary>
    /// The IDP client identifier that created this encrypted value
    /// </summary>
    public int ClientId { get; set; }

    /// <summary>
    /// The encryption key ID used
    /// </summary>
    public int KeyId { get; set; }

    /// <summary>
    /// When the encrypted value was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
