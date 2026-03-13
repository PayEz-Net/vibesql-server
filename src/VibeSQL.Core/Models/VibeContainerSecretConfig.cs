namespace VibeSQL.Core.Models;

/// <summary>
/// Configuration for container secret authentication.
/// Used in internal/k8s deployments where services authenticate
/// via a shared secret instead of HMAC signing.
/// </summary>
public class VibeContainerSecretConfig
{
    /// <summary>
    /// The shared secret used for Authorization: Secret {key} validation
    /// </summary>
    public string Secret { get; set; } = string.Empty;
}
