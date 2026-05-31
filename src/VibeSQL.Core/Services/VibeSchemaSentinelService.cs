using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Models;

namespace VibeSQL.Core.Services;

/// <summary>
/// VS-SS Schema Sentinel Service Implementation
/// Monitors schema health, detects corruption, validates schemas, and performs rollbacks.
/// </summary>
public class VibeSchemaSentinelService : IVibeSchemaSentinelService
{
    private readonly IVibeSchemaRepository _schemaRepository;
    private readonly string _connectionString;
    private readonly ILogger<VibeSchemaSentinelService> _logger;

    public VibeSchemaSentinelService(
        IVibeSchemaRepository schemaRepository,
        IConfiguration configuration,
        ILogger<VibeSchemaSentinelService> logger)
    {
        _schemaRepository = schemaRepository;
        _connectionString = configuration.GetConnectionString("VibeDb")
            ?? throw new InvalidOperationException("VibeDb connection string not configured for Schema Sentinel");
        _logger = logger;
    }

    public async Task<List<SchemaHealthInfo>> GetSchemaHealthAsync(int? clientId = null)
    {
        var sql = @"
            SELECT 
                cs.collection_schema_id,
                cs.client_id,
                cs.collection,
                cs.version,
                cs.is_active,
                cs.is_locked,
                cs.is_system,
                cs.created_at,
                cs.created_by,
                v.table_count,
                v.is_valid,
                v.error_message,
                octet_length(cs.json_schema::text) as json_size_bytes,
                CASE 
                    WHEN v.table_count > 100 THEN 'CRITICAL'
                    WHEN v.table_count > 50 THEN 'WARNING'
                    WHEN NOT v.is_valid THEN 'INVALID'
                    ELSE 'HEALTHY'
                END as health_status
            FROM vibe.collection_schemas cs
            CROSS JOIN LATERAL vibe.validate_schema_json(cs.json_schema) v
            WHERE (@clientId IS NULL OR cs.client_id = @clientId)
            ORDER BY cs.client_id, cs.collection, cs.version DESC";

        var parameters = new[] { new NpgsqlParameter("clientId", clientId ?? (object)DBNull.Value) };
        var result = await ExecuteQueryAsync(sql, parameters);

        return result.Select(row => new SchemaHealthInfo
        {
            CollectionSchemaId = GetInt(row, "collection_schema_id"),
            ClientId = GetInt(row, "client_id"),
            Collection = GetString(row, "collection") ?? string.Empty,
            Version = GetInt(row, "version"),
            IsActive = GetBool(row, "is_active"),
            IsLocked = GetBool(row, "is_locked"),
            IsSystem = GetBool(row, "is_system"),
            CreatedAt = GetDateTimeOffset(row, "created_at"),
            CreatedBy = GetNullableInt(row, "created_by"),
            TableCount = GetInt(row, "table_count"),
            IsValid = GetBool(row, "is_valid"),
            ErrorMessage = GetString(row, "error_message"),
            JsonSizeBytes = GetInt(row, "json_size_bytes"),
            HealthStatus = ParseHealthStatus(GetString(row, "health_status"))
        }).ToList();
    }

    public async Task<List<CorruptedSchemaInfo>> DetectCorruptionAsync(int? clientId = null, int maxTableThreshold = 100)
    {
        var allHealth = await GetSchemaHealthAsync(clientId);
        var corrupted = allHealth
            .Where(h => !h.IsValid || h.TableCount > maxTableThreshold)
            .Select(h => new CorruptedSchemaInfo
            {
                CollectionSchemaId = h.CollectionSchemaId,
                ClientId = h.ClientId,
                Collection = h.Collection,
                Version = h.Version,
                TableCount = h.TableCount,
                ErrorMessage = h.ErrorMessage
            })
            .ToList();

        // Check for clean versions
        foreach (var item in corrupted)
        {
            var cleanVersion = allHealth
                .FirstOrDefault(h => h.ClientId == item.ClientId
                    && h.Collection == item.Collection
                    && h.IsValid
                    && h.TableCount <= maxTableThreshold
                    && h.Version < item.Version);

            if (cleanVersion != null)
            {
                item.HasCleanVersionAvailable = true;
                item.CleanVersionId = cleanVersion.CollectionSchemaId;
            }
        }

        return corrupted;
    }

    public async Task<RollbackResult> RollbackToCleanVersionAsync(int schemaId, int adminUserId)
    {
        // Get the corrupted schema
        var corrupted = await _schemaRepository.GetByIdAsync(schemaId);
        if (corrupted == null)
        {
            return new RollbackResult
            {
                Success = false,
                ErrorCode = "SCHEMA_NOT_FOUND",
                ErrorMessage = $"Schema {schemaId} not found"
            };
        }

        // Validate it's actually corrupted
        var validation = await ValidateSchemaJsonAsync(corrupted.JsonSchema ?? "{}");
        if (validation.IsValid && validation.TableCount <= 100)
        {
            return new RollbackResult
            {
                Success = false,
                ErrorCode = "SCHEMA_NOT_CORRUPTED",
                ErrorMessage = "Schema is not corrupted, no rollback needed"
            };
        }

        // Find a clean version
        var allVersions = await _schemaRepository.GetVersionsAsync(corrupted.ClientId, corrupted.Collection);
        var cleanVersion = allVersions
            .Where(v => v.Version < corrupted.Version && v.CollectionSchemaId != corrupted.CollectionSchemaId)
            .OrderByDescending(v => v.Version)
            .FirstOrDefault();

        if (cleanVersion == null)
        {
            return new RollbackResult
            {
                Success = false,
                ErrorCode = "NO_CLEAN_VERSION",
                ErrorMessage = "No clean version found to rollback to"
            };
        }

        try
        {
            // Deactivate corrupted version
            corrupted.IsActive = false;
            corrupted.UpdatedBy = adminUserId;
            corrupted.UpdatedAt = DateTimeOffset.UtcNow;
            await _schemaRepository.UpdateAsync(corrupted);

            // Create new version with clean schema
            var maxVersion = await _schemaRepository.GetMaxVersionAsync(corrupted.ClientId, corrupted.Collection);
            var newSchema = new VibeCollectionSchema
            {
                ClientId = corrupted.ClientId,
                Collection = corrupted.Collection,
                JsonSchema = cleanVersion.JsonSchema,
                Version = maxVersion + 1,
                IsActive = true,
                IsSystem = corrupted.IsSystem,
                IsLocked = corrupted.IsLocked,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = adminUserId
            };

            await _schemaRepository.CreateAsync(newSchema);

            var newValidation = await ValidateSchemaJsonAsync(newSchema.JsonSchema ?? "{}");

            _logger.LogWarning("VSS_ROLLBACK_SUCCESS: SchemaId={SchemaId}, Collection={Collection}, FromVersion={FromVersion}, ToVersion={ToVersion}, Tables={Tables}",
                schemaId, corrupted.Collection, corrupted.Version, newSchema.Version, newValidation.TableCount);

            return new RollbackResult
            {
                Success = true,
                RolledBackFromVersion = corrupted.Version,
                NewVersion = newSchema.Version,
                TableCountAfter = newValidation.TableCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VSS_ROLLBACK_FAILED: SchemaId={SchemaId}", schemaId);
            return new RollbackResult
            {
                Success = false,
                ErrorCode = "ROLLBACK_FAILED",
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ValidationResult> ValidateSchemaJsonAsync(string jsonSchema)
    {
        var sql = "SELECT * FROM vibe.validate_schema_json(@schema::jsonb)";
        var parameters = new[] { new NpgsqlParameter("schema", jsonSchema) };
        var result = await ExecuteQueryAsync(sql, parameters);

        if (result.Count == 0)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "Failed to validate schema"
            };
        }

        var row = result[0];
        var tableNames = new List<string>();

        // If valid, extract table names
        if (GetBool(row, "is_valid"))
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonSchema);
                var tables = doc.RootElement.TryGetProperty("tables", out var t) ? t : doc.RootElement;
                foreach (var prop in tables.EnumerateObject())
                {
                    if (!new[] { "$schema", "title", "description", "tableGroup", "x-vibe-migrations" }.Contains(prop.Name))
                    {
                        tableNames.Add(prop.Name);
                    }
                }
            }
            catch { }
        }

        return new ValidationResult
        {
            IsValid = GetBool(row, "is_valid"),
            TableCount = GetInt(row, "table_count"),
            ErrorMessage = GetString(row, "error_message"),
            TableNames = tableNames
        };
    }

    public async Task<CleanupResult> CleanupCorruptedSchemasAsync(bool dryRun = true, int maxTableThreshold = 100)
    {
        var sql = "SELECT * FROM vibe.cleanup_corrupted_schemas(@dryRun, @maxTableThreshold)";
        var parameters = new[]
        {
            new NpgsqlParameter("dryRun", dryRun),
            new NpgsqlParameter("maxTableThreshold", maxTableThreshold)
        };
        var result = await ExecuteQueryAsync(sql, parameters);

        var actions = result.Select(row => new CleanupAction
        {
            Action = GetString(row, "action") ?? string.Empty,
            CollectionSchemaId = GetInt(row, "collection_schema_id"),
            ClientId = GetInt(row, "client_id"),
            Collection = GetString(row, "collection") ?? string.Empty,
            Version = GetInt(row, "version"),
            TableCount = GetInt(row, "table_count"),
            Details = GetString(row, "details")
        }).ToList();

        return new CleanupResult
        {
            Success = true,
            TotalCorrupted = actions.Count,
            RolledBack = actions.Count(a => a.Action.Contains("ROLLBACK")),
            Flagged = actions.Count(a => a.Action.Contains("FLAG")),
            Actions = actions
        };
    }

    // Npgsql query execution helper
    private async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string sql, NpgsqlParameter[]? parameters = null)
    {
        var results = new List<Dictionary<string, object?>>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        if (parameters != null)
        {
            command.Parameters.AddRange(parameters);
        }

        await using var reader = await command.ExecuteReaderAsync();
        var columnNames = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columnNames.Add(reader.GetName(i));
        }

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.GetValue(i);
                row[columnNames[i]] = value == DBNull.Value ? null : value;
            }
            results.Add(row);
        }

        return results;
    }

    // Helper methods
    private static SchemaHealthStatus ParseHealthStatus(string? status)
    {
        return status?.ToUpper() switch
        {
            "HEALTHY" => SchemaHealthStatus.Healthy,
            "WARNING" => SchemaHealthStatus.Warning,
            "INVALID" => SchemaHealthStatus.Invalid,
            "CRITICAL" => SchemaHealthStatus.Critical,
            _ => SchemaHealthStatus.Invalid
        };
    }

    private static int GetInt(Dictionary<string, object?> row, string key)
    {
        return row.TryGetValue(key, out var val) && val != null ? Convert.ToInt32(val) : 0;
    }

    private static int? GetNullableInt(Dictionary<string, object?> row, string key)
    {
        return row.TryGetValue(key, out var val) && val != null && val != DBNull.Value ? Convert.ToInt32(val) : null;
    }

    private static string? GetString(Dictionary<string, object?> row, string key)
    {
        return row.TryGetValue(key, out var val) && val != null && val != DBNull.Value ? val.ToString() : null;
    }

    private static bool GetBool(Dictionary<string, object?> row, string key)
    {
        return row.TryGetValue(key, out var val) && val != null && Convert.ToBoolean(val);
    }

    private static DateTimeOffset GetDateTimeOffset(Dictionary<string, object?> row, string key)
    {
        if (row.TryGetValue(key, out var val) && val != null)
        {
            if (val is DateTimeOffset dto) return dto;
            if (val is DateTime dt) return new DateTimeOffset(dt);
            if (DateTimeOffset.TryParse(val.ToString(), out var parsed)) return parsed;
        }
        return DateTimeOffset.MinValue;
    }
}
