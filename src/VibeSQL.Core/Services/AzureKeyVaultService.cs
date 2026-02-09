using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VibeSQL.Core.Services;

/// <summary>
/// Azure Key Vault implementation of IKeyVaultService.
/// Used when VibeSQL:UseKeyVault is true in configuration.
/// </summary>
public class AzureKeyVaultService : IKeyVaultService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AzureKeyVaultService> _logger;
    private readonly SecretClient _secretClient;

    public AzureKeyVaultService(IConfiguration configuration, IHostEnvironment environment, ILogger<AzureKeyVaultService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
        _secretClient = InitializeSecretClient();
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        const int maxRetries = 3;
        const int baseDelayMs = 500;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Retrieving secret: {SecretName} (attempt {Attempt}/{MaxRetries})", secretName, attempt, maxRetries);
                var secret = await _secretClient.GetSecretAsync(secretName);
                _logger.LogInformation("Successfully retrieved secret: {SecretName}", secretName);
                return secret.Value.Value;
            }
            catch (Exception ex) when (attempt < maxRetries && IsRetryableException(ex))
            {
                var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                _logger.LogWarning(ex, "Failed to retrieve secret {SecretName} on attempt {Attempt}. Retrying in {Delay}ms...",
                    secretName, attempt, delay);
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving secret: {SecretName} after {Attempts} attempts", secretName, attempt);
                throw;
            }
        }

        throw new InvalidOperationException($"Failed to retrieve secret {secretName} after {maxRetries} attempts");
    }

    private static bool IsRetryableException(Exception ex)
    {
        return ex is Azure.RequestFailedException rfe &&
               (rfe.Status == 429 || rfe.Status >= 500 || rfe.Status == 408);
    }

    private Uri GetVaultUri()
    {
        var vaultUri = _configuration["VibeSQL:KeyVaultUri"]
            ?? throw new InvalidOperationException("VibeSQL:KeyVaultUri is not configured. Set it in appsettings or environment variable VIBESQL__KeyVaultUri");

        _logger.LogInformation("Using Key Vault URI: {VaultUri}", vaultUri);
        return new Uri(vaultUri);
    }

    private SecretClient InitializeSecretClient()
    {
        var credential = GetCredential();
        var vaultUri = GetVaultUri();
        return new SecretClient(vaultUri, credential);
    }

    private TokenCredential GetCredential()
    {
        // In development, you can use client credentials or Azure CLI auth.
        // In production, prefer managed identity (DefaultAzureCredential).
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("Using DefaultAzureCredential for Key Vault access (development)");
        }
        else
        {
            _logger.LogInformation("Using DefaultAzureCredential for Key Vault access (production)");
        }

        return new DefaultAzureCredential();
    }
}
