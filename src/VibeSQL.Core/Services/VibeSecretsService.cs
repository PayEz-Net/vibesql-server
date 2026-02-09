using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Services;
using VibeSQL.Core.Data;
using VibeSQL.Core.Interfaces;
using System.Text.Json;

namespace VibeSQL.Core.Services;

/// <summary>
/// Service for managing site_secrets in vibe_app collection.
/// Uses VibeDbContext directly for database access.
/// Secrets are encrypted using ITokenizationService (General key).
/// </summary>
public class VibeSecretsService : IVibeSecretsService
{
    private readonly VibeDbContext _vibeContext;
    private readonly ITokenizationService _tokenizationService;
    private readonly ILogger<VibeSecretsService> _logger;

    private const string VIBE_APP_COLLECTION = "vibe_app";
    private const string SITE_SECRETS_TABLE = "site_secrets";
    private const int SYSTEM_USER_ID = 0;

    public VibeSecretsService(
        VibeDbContext vibeContext,
        ITokenizationService tokenizationService,
        ILogger<VibeSecretsService> logger)
    {
        _vibeContext = vibeContext;
        _tokenizationService = tokenizationService;
        _logger = logger;
    }

    public async Task<bool> EnsureVibeAppSchemaExistsAsync(int clientId)
    {
        _logger.LogWarning("[VIBE_SECRET_SVC] EnsureVibeAppSchemaExistsAsync called for client {ClientId}", clientId);

        var existingSchema = await _vibeContext.CollectionSchemas
            .FirstOrDefaultAsync(s => s.ClientId == clientId
                        && s.Collection == VIBE_APP_COLLECTION
                        && s.IsActive);

        if (existingSchema != null)
        {
            _logger.LogWarning("[VIBE_SECRET_SVC] Schema already exists for client {ClientId}, schemaId={SchemaId}",
                clientId, existingSchema.CollectionSchemaId);
            return true;
        }

        // Schema doesn't exist - CREATE it
        _logger.LogWarning("[VIBE_SECRET_SVC] Schema does not exist for client {ClientId}, creating...", clientId);

        try
        {
            var schema = await CreateVibeAppSchemaAsync(clientId);
            _logger.LogWarning("[VIBE_SECRET_SVC] Created schema for client {ClientId}, schemaId={SchemaId}",
                clientId, schema.CollectionSchemaId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VIBE_SECRET_SVC] FAILED to create schema for client {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// Creates a minimal vibe_app schema with site_secrets table for storing encrypted secrets.
    /// This is a simplified version - the full starter kit schema is created by the Vibe API's AgentContextService.
    /// </summary>
    private async Task<VibeCollectionSchema> CreateVibeAppSchemaAsync(int clientId)
    {
        const string minimalVibeAppSchema = @"{
  ""tableGroup"": ""vibe_app"",
  ""tables"": {
    ""site_secrets"": {
      ""type"": ""object"",
      ""description"": ""Encrypted site secrets and API keys"",
      ""properties"": {
        ""secret_id"": {
          ""type"": ""integer"",
          ""description"": ""Primary key"",
          ""x-vibe-pk"": true,
          ""x-vibe-auto-increment"": true
        },
        ""secret_name"": {
          ""type"": ""string"",
          ""description"": ""Unique identifier for the secret (e.g. STRIPE_API_KEY)""
        },
        ""secret_value"": {
          ""type"": ""string"",
          ""description"": ""Encrypted secret value"",
          ""x-vibe-tokenize"": true
        },
        ""enc_key_id"": {
          ""type"": ""integer"",
          ""description"": ""Encryption key ID for decryption""
        },
        ""valid_until"": {
          ""type"": ""string"",
          ""format"": ""date-time"",
          ""description"": ""When this encrypted token is no longer valid""
        },
        ""created_at"": {
          ""type"": ""string"",
          ""format"": ""date-time"",
          ""description"": ""When the secret was created""
        },
        ""updated_at"": {
          ""type"": ""string"",
          ""format"": ""date-time"",
          ""description"": ""When the secret was last updated""
        },
        ""created_by"": {
          ""type"": ""integer"",
          ""description"": ""User ID who created this secret""
        }
      },
      ""required"": [""secret_id"", ""secret_name"", ""secret_value""]
    }
  }
}";

        var schema = new VibeCollectionSchema
        {
            ClientId = clientId,
            Collection = VIBE_APP_COLLECTION,
            JsonSchema = minimalVibeAppSchema,
            Version = 1,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = SYSTEM_USER_ID
        };

        _vibeContext.CollectionSchemas.Add(schema);
        await _vibeContext.SaveChangesAsync();

        return schema;
    }

    public async Task<bool> StoreSecretAsync(int clientId, string secretName, string secretValue, int? createdByUserId = null)
    {
        _logger.LogWarning("[VIBE_SECRET_SVC] StoreSecretAsync called for client {ClientId}, secretName={SecretName}",
            clientId, secretName);

        try
        {
            // Encrypt the secret value using General key
            var encryptResult = await _tokenizationService.TokenizeAsync(secretValue);

            _logger.LogWarning(
                "[VIBE_SECRET_SVC] ENCRYPT client={ClientId} secret={SecretName} keyId={KeyId} validUntil={ValidUntil}",
                clientId, secretName, encryptResult.KeyId, encryptResult.ValidUntil?.ToString("o") ?? "none");

            // Check if secret already exists - fetch all and filter (site_secrets has few rows)
            var existing = await FindSecretDocumentAsync(clientId, secretName);

            var now = DateTimeOffset.UtcNow;

            // Build the encrypted wrapper object (matches Vibe API auto-tokenization format)
            var encryptedWrapper = new Dictionary<string, object?>
            {
                ["_enc"] = encryptResult.TokenizedValue,
                ["_key"] = encryptResult.KeyId,
                ["_valid_until"] = encryptResult.ValidUntil?.ToString("o")
            };

            if (existing != null)
            {
                // Update existing secret with encrypted value
                var data = new Dictionary<string, object?>
                {
                    ["secret_name"] = secretName,
                    ["secret_value"] = encryptedWrapper,
                    ["updated_at"] = now.ToString("o"),
                    ["created_at"] = existing.CreatedAt.ToString("o")
                };

                existing.Data = JsonSerializer.Serialize(data);
                existing.UpdatedAt = now;
                existing.UpdatedBy = createdByUserId;

                // Ensure CollectionSchemaId is set (needed for is_system check in trigger)
                if (existing.CollectionSchemaId == null)
                {
                    var schema = await _vibeContext.CollectionSchemas
                        .FirstOrDefaultAsync(s => s.ClientId == clientId && s.Collection == VIBE_APP_COLLECTION && s.IsActive);
                    existing.CollectionSchemaId = schema?.CollectionSchemaId;
                }

                await _vibeContext.SaveChangesAsync();
                _logger.LogWarning("[VIBE_SECRET_SVC] Updated secret {SecretName} for client {ClientId}, docId={DocId}",
                    secretName, clientId, existing.DocumentId);
            }
            else
            {
                // Create new secret with encrypted value
                var data = new Dictionary<string, object?>
                {
                    ["secret_name"] = secretName,
                    ["secret_value"] = encryptedWrapper,
                    ["created_at"] = now.ToString("o"),
                    ["updated_at"] = now.ToString("o")
                };

                // Look up vibe_app schema for this client (needed for tier limit exemption)
                var schema = await _vibeContext.CollectionSchemas
                    .FirstOrDefaultAsync(s => s.ClientId == clientId && s.Collection == VIBE_APP_COLLECTION && s.IsActive);

                var doc = new VibeDocument
                {
                    ClientId = clientId,
                    OwnerUserId = SYSTEM_USER_ID,  // System-owned secret document
                    Collection = VIBE_APP_COLLECTION,
                    CollectionSchemaId = schema?.CollectionSchemaId,  // Required for is_system check in trigger
                    TableName = SITE_SECRETS_TABLE,
                    Data = JsonSerializer.Serialize(data),
                    CreatedAt = now,
                    CreatedBy = createdByUserId
                };

                _vibeContext.Documents.Add(doc);
                await _vibeContext.SaveChangesAsync();
                _logger.LogWarning("[VIBE_SECRET_SVC] Created secret {SecretName} for client {ClientId}, docId={DocId}",
                    secretName, clientId, doc.DocumentId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VIBE_SECRET_SVC] FAILED to store secret {SecretName} for client {ClientId}", secretName, clientId);
            return false;
        }
    }

    public async Task<VibeSecretDto?> GetSecretAsync(int clientId, string secretName)
    {
        var doc = await FindSecretDocumentAsync(clientId, secretName);

        if (doc == null)
            return null;

        return await MapToSecretDtoAsync(doc, clientId);
    }

    public async Task<bool> DeleteSecretAsync(int clientId, string secretName)
    {
        var doc = await FindSecretDocumentAsync(clientId, secretName);

        if (doc == null)
            return false;

        doc.DeletedAt = DateTimeOffset.UtcNow;
        await _vibeContext.SaveChangesAsync();

        _logger.LogInformation("Deleted secret {SecretName} for client {ClientId}", secretName, clientId);
        return true;
    }

    /// <summary>
    /// Finds a secret document by name. Fetches all site_secrets for client and filters in memory.
    /// This is efficient since site_secrets typically has only a few rows per client.
    /// </summary>
    private async Task<VibeDocument?> FindSecretDocumentAsync(int clientId, string secretName)
    {
        var docs = await _vibeContext.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == VIBE_APP_COLLECTION
                     && d.TableName == SITE_SECRETS_TABLE
                     && d.DeletedAt == null)
            .ToListAsync();

        return docs.FirstOrDefault(d =>
        {
            if (string.IsNullOrEmpty(d.Data)) return false;
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(d.Data);
                return data != null
                    && data.TryGetValue("secret_name", out var name)
                    && name.GetString() == secretName;
            }
            catch
            {
                return false;
            }
        });
    }

    private async Task<VibeSecretDto> MapToSecretDtoAsync(VibeDocument doc, int clientId)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(doc.Data ?? "{}")
            ?? new Dictionary<string, JsonElement>();

        var secretName = data.TryGetValue("secret_name", out var name) ? name.GetString() ?? "" : "";

        string? encryptedValue = null;
        int? encKeyId = null;
        DateTime? validUntil = null;
        string? decryptedValue = null;

        // Check for new wrapper format: secret_value = {_enc, _key, _valid_until}
        if (data.TryGetValue("secret_value", out var secretValueEl))
        {
            if (secretValueEl.ValueKind == JsonValueKind.Object)
            {
                // New wrapper format
                if (secretValueEl.TryGetProperty("_enc", out var encEl))
                    encryptedValue = encEl.GetString();
                if (secretValueEl.TryGetProperty("_key", out var keyEl) && keyEl.ValueKind == JsonValueKind.Number)
                    encKeyId = keyEl.GetInt32();
                if (secretValueEl.TryGetProperty("_valid_until", out var validEl) && validEl.ValueKind == JsonValueKind.String)
                {
                    var validStr = validEl.GetString();
                    if (!string.IsNullOrEmpty(validStr))
                        validUntil = DateTime.Parse(validStr);
                }
            }
            else if (secretValueEl.ValueKind == JsonValueKind.String)
            {
                // Legacy format: secret_value is plain string, enc_key_id is separate field
                encryptedValue = secretValueEl.GetString();
                if (data.TryGetValue("enc_key_id", out var keyIdEl) && keyIdEl.ValueKind == JsonValueKind.Number)
                    encKeyId = keyIdEl.GetInt32();
                if (data.TryGetValue("valid_until", out var validEl) && validEl.ValueKind == JsonValueKind.String)
                {
                    var validStr = validEl.GetString();
                    if (!string.IsNullOrEmpty(validStr))
                        validUntil = DateTime.Parse(validStr);
                }
            }
        }

        // Decrypt if we have both ciphertext and key ID
        if (!string.IsNullOrEmpty(encryptedValue) && encKeyId.HasValue)
        {
            try
            {
                decryptedValue = await _tokenizationService.DetokenizeAsync(encryptedValue, encKeyId.Value);
                _logger.LogDebug("[VIBE_SECRET] DECRYPT client={ClientId} secret={SecretName} keyId={KeyId}",
                    clientId, secretName, encKeyId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VIBE_SECRET] DECRYPT_FAILED client={ClientId} secret={SecretName} keyId={KeyId}",
                    clientId, secretName, encKeyId);
                // Return null for secret value on decrypt failure
            }
        }
        else if (!string.IsNullOrEmpty(encryptedValue) && !encKeyId.HasValue)
        {
            // Legacy unencrypted value (no key ID) - return as-is with warning
            _logger.LogWarning("[VIBE_SECRET] UNENCRYPTED client={ClientId} secret={SecretName} - missing enc_key_id, returning raw value",
                clientId, secretName);
            decryptedValue = encryptedValue;
        }

        return new VibeSecretDto
        {
            Id = doc.DocumentId,
            SecretName = secretName,
            SecretValue = decryptedValue ?? "",
            EncKeyId = encKeyId,
            ValidUntil = validUntil,
            CreatedAt = doc.CreatedAt.DateTime,
            UpdatedAt = doc.UpdatedAt?.DateTime,
            CreatedBy = doc.CreatedBy
        };
    }
}
