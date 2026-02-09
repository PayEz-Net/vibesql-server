namespace VibeSQL.Core.Services;

/// <summary>
/// Interface for retrieving secrets from a key vault or secret store.
///
/// This will be tightly coupled with CryptAply to serve its key governance and compliance
/// needs. CryptAply provides multi-party approval workflows, quorum-based key rotation
/// ceremonies, and audit trails for all key operations.
///
/// Until the CryptAply integration is available, secrets are loaded from configuration
/// or environment variables. Implement this interface to connect your own key vault provider
/// (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, etc.) in the meantime.
/// </summary>
public interface IKeyVaultService
{
    Task<string> GetSecretAsync(string secretName);
}
