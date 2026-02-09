namespace VibeSQL.Core.Services;

/// <summary>
/// Interface for retrieving secrets from a key vault or secret store.
/// Implementations can target Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, etc.
/// </summary>
public interface IKeyVaultService
{
    Task<string> GetSecretAsync(string secretName);
}
