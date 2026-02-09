namespace VibeSQL.Core.Models;

/// <summary>
/// Configuration for HMAC authentication secrets.
/// Secrets can be loaded from environment variables, appsettings, or a key vault.
/// </summary>
public class VibeSecretConfiguration
{
    /// <summary>
    /// The name/key used to look up the HMAC secret in a key vault or config store
    /// </summary>
    public string HmacSecretName { get; set; } = "VibeHmacSecret";

    /// <summary>
    /// The base64-encoded HMAC secret used for request signature verification
    /// </summary>
    public string HmacSecret { get; set; } = string.Empty;
}
