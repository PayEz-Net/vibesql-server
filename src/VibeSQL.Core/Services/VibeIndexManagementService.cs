using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Models;
using VibeSQL.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VibeSQL.Core.Services;

/// <summary>
/// Service for managing virtual indexes on JSONB fields.
/// Creates physical PostgreSQL indexes based on schema hints (x-vibe-index, x-vibe-indexes).
/// </summary>
public class VibeIndexManagementService : IVibeIndexManagementService
{
    private readonly IVirtualIndexRepository _repository;
    private readonly ILogger<VibeIndexManagementService> _logger;

    public VibeIndexManagementService(
        IVirtualIndexRepository repository,
        ILogger<VibeIndexManagementService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<List<IndexCreationResult>> SyncIndexesForSchemaAsync(
        int clientId,
        string collection,
        JsonDocument jsonSchema)
    {
        _logger.LogInformation("VIBE_INDEX_SYNC: Syncing indexes for client={ClientId}, collection={Collection}",
            clientId, collection);

        var results = new List<IndexCreationResult>();

        try
        {
            // Parse index definitions from schema
            var indexDefs = ParseIndexDefinitionsFromSchema(jsonSchema);

            _logger.LogInformation("VIBE_INDEX_SYNC: Found {Count} index definitions", indexDefs.Count);

            // Get partition name for this client
            var partitionName = await _repository.GetPartitionNameAsync(clientId);
            if (string.IsNullOrEmpty(partitionName))
            {
                _logger.LogWarning("VIBE_INDEX_SYNC: No partition found for client={ClientId}", clientId);
                return results;
            }

            // Get existing indexes for this collection
            var existingIndexes = await _repository.GetActiveIndexesAsync(clientId, collection);

            // Create new indexes
            foreach (var indexDef in indexDefs)
            {
                var indexName = GenerateIndexName(indexDef);
                var existing = existingIndexes.FirstOrDefault(e =>
                    e.TableName == indexDef.TableName && e.IndexName == indexName);

                if (existing == null)
                {
                    var result = await CreateVirtualIndexAsync(clientId, collection, indexDef.TableName, indexDef);
                    results.Add(result);
                }
            }

            // Drop orphaned indexes (indexes that no longer exist in schema)
            var indexNamesInSchema = indexDefs.Select(d => GenerateIndexName(d)).ToHashSet();
            foreach (var existing in existingIndexes)
            {
                if (!indexNamesInSchema.Contains(existing.IndexName))
                {
                    _logger.LogInformation("VIBE_INDEX_SYNC: Dropping orphaned index={IndexName}", existing.IndexName);
                    await DropVirtualIndexAsync(clientId, collection, existing.IndexName);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VIBE_INDEX_SYNC: Failed to sync indexes for client={ClientId}", clientId);
            results.Add(new IndexCreationResult
            {
                Success = false,
                ErrorMessage = $"Failed to sync indexes: {ex.Message}"
            });
            return results;
        }
    }

    public async Task<IndexCreationResult> CreateVirtualIndexAsync(
        int clientId,
        string collection,
        string tableName,
        IndexDefinition indexDef)
    {
        _logger.LogInformation("VIBE_INDEX_CREATE: Creating index for client={ClientId}, table={Table}, fields={Fields}",
            clientId, tableName, string.Join(",", indexDef.Fields));

        try
        {
            // Check tier limits
            var indexCount = await _repository.GetActiveIndexCountAsync(clientId);
            var tierLimit = await _repository.GetTierLimitAsync(clientId);
            if (indexCount >= tierLimit)
            {
                return new IndexCreationResult
                {
                    Success = false,
                    ErrorMessage = $"Index limit reached ({indexCount}/{tierLimit}). Upgrade tier for more indexes."
                };
            }

            // Get partition info
            var partitionInfo = await _repository.GetPartitionInfoAsync(clientId);
            if (partitionInfo == null)
            {
                return new IndexCreationResult
                {
                    Success = false,
                    ErrorMessage = "Client partition not found"
                };
            }

            // Generate physical index name
            var physicalIndexName = GeneratePhysicalIndexName(clientId, tableName, indexDef);

            // Build CREATE INDEX DDL
            var ddl = BuildCreateIndexDDL(
                physicalIndexName,
                partitionInfo.PartitionName,
                partitionInfo.IsShared,
                clientId,
                collection,
                tableName,
                indexDef);

            _logger.LogInformation("VIBE_INDEX_CREATE: Executing DDL: {DDL}", ddl);

            // Execute CREATE INDEX CONCURRENTLY
            await _repository.ExecuteDDLAsync(ddl);

            // Save to virtual_indexes table
            var virtualIndex = new VirtualIndex
            {
                ClientId = clientId,
                Collection = collection,
                TableName = tableName,
                IndexName = GenerateIndexName(indexDef),
                PhysicalIndexName = physicalIndexName,
                IndexDefinition = JsonSerializer.Serialize(indexDef),
                PartitionName = partitionInfo.PartitionName,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var createdIndex = await _repository.CreateAsync(virtualIndex);

            _logger.LogInformation("VIBE_INDEX_CREATE: Index created successfully: {PhysicalName}", physicalIndexName);

            return new IndexCreationResult
            {
                Success = true,
                PhysicalIndexName = physicalIndexName,
                VirtualIndexId = createdIndex.VirtualIndexId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VIBE_INDEX_CREATE: Failed to create index");
            return new IndexCreationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> DropVirtualIndexAsync(int clientId, string collection, string indexName)
    {
        _logger.LogInformation("VIBE_INDEX_DROP: Dropping index={IndexName} for client={ClientId}", indexName, clientId);

        try
        {
            var virtualIndex = await _repository.GetByNameAsync(clientId, collection, indexName);

            if (virtualIndex == null)
            {
                _logger.LogWarning("VIBE_INDEX_DROP: Index not found: {IndexName}", indexName);
                return false;
            }

            // Drop physical index
            var ddl = $"DROP INDEX CONCURRENTLY IF EXISTS vibe.\"{virtualIndex.PhysicalIndexName}\"";
            await _repository.ExecuteDDLAsync(ddl);

            // Mark as dropped
            virtualIndex.DroppedAt = DateTimeOffset.UtcNow;
            await _repository.UpdateAsync(virtualIndex);

            _logger.LogInformation("VIBE_INDEX_DROP: Index dropped successfully: {PhysicalName}", virtualIndex.PhysicalIndexName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VIBE_INDEX_DROP: Failed to drop index");
            return false;
        }
    }

    public async Task<List<IndexInfo>> ListIndexesForClientAsync(int clientId, string collection)
    {
        var indexes = await _repository.GetActiveIndexesAsync(clientId, collection);

        return indexes.Select(v => new IndexInfo
        {
            VirtualIndexId = v.VirtualIndexId,
            IndexName = v.IndexName,
            TableName = v.TableName,
            PhysicalIndexName = v.PhysicalIndexName,
            PartitionName = v.PartitionName,
            Fields = JsonSerializer.Deserialize<IndexDefinition>(v.IndexDefinition)?.Fields ?? new List<string>(),
            CreatedAt = v.CreatedAt
        }).ToList();
    }

    public async Task<int> GetIndexCountAsync(int clientId)
    {
        return await _repository.GetActiveIndexCountAsync(clientId);
    }

    // ============================================================
    // Helper Methods
    // ============================================================

    private List<IndexDefinition> ParseIndexDefinitionsFromSchema(JsonDocument jsonSchema)
    {
        var indexDefs = new List<IndexDefinition>();

        try
        {
            var root = jsonSchema.RootElement;
            if (!root.TryGetProperty("tables", out var tables))
                return indexDefs;

            foreach (var table in tables.EnumerateObject())
            {
                var tableName = table.Name;
                var tableSchema = table.Value;

                // Check for x-vibe-indexes array (composite indexes)
                if (tableSchema.TryGetProperty("x-vibe-indexes", out var vibeIndexes) && vibeIndexes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var indexDef in vibeIndexes.EnumerateArray())
                    {
                        var def = ParseIndexDefinition(tableName, indexDef);
                        if (def != null)
                            indexDefs.Add(def);
                    }
                }

                // Check for x-vibe-index: true on individual properties (field-level indexes)
                if (tableSchema.TryGetProperty("properties", out var properties))
                {
                    foreach (var prop in properties.EnumerateObject())
                    {
                        var fieldName = prop.Name;
                        if (prop.Value.TryGetProperty("x-vibe-index", out var xVibeIndex) && xVibeIndex.GetBoolean())
                        {
                            indexDefs.Add(new IndexDefinition
                            {
                                TableName = tableName,
                                Fields = new List<string> { fieldName },
                                IndexType = "btree"
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VIBE_INDEX_PARSE: Failed to parse index definitions from schema");
        }

        return indexDefs;
    }

    private IndexDefinition? ParseIndexDefinition(string tableName, JsonElement indexDef)
    {
        try
        {
            var fields = new List<string>();
            if (indexDef.TryGetProperty("fields", out var fieldsArray))
            {
                foreach (var field in fieldsArray.EnumerateArray())
                {
                    fields.Add(field.GetString() ?? "");
                }
            }

            if (fields.Count == 0)
                return null;

            return new IndexDefinition
            {
                IndexName = indexDef.TryGetProperty("name", out var name) ? name.GetString() : null,
                TableName = tableName,
                Fields = fields,
                PartialCondition = indexDef.TryGetProperty("partial", out var partial) ? partial.GetString() : null,
                IsUnique = indexDef.TryGetProperty("unique", out var unique) && unique.GetBoolean(),
                IndexType = indexDef.TryGetProperty("type", out var type) ? type.GetString() ?? "btree" : "btree"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VIBE_INDEX_PARSE: Failed to parse index definition");
            return null;
        }
    }

    private string GenerateIndexName(IndexDefinition indexDef)
    {
        if (!string.IsNullOrEmpty(indexDef.IndexName))
            return indexDef.IndexName;

        // Generate name from fields: idx_table_field1_field2
        var fieldsStr = string.Join("_", indexDef.Fields.Take(3));
        return $"idx_{indexDef.TableName}_{fieldsStr}";
    }

    private string GeneratePhysicalIndexName(int clientId, string tableName, IndexDefinition indexDef)
    {
        // Format: idx_c{clientId}_{table}_{field}_{hash}
        var fieldsStr = string.Join("_", indexDef.Fields.Take(2));
        var hashInput = $"{clientId}_{tableName}_{string.Join("_", indexDef.Fields)}_{indexDef.PartialCondition}";
        var hash = ComputeMD5Hash(hashInput).Substring(0, 4);

        return $"idx_c{clientId}_{tableName}_{fieldsStr}_{hash}".ToLowerInvariant();
    }

    private string BuildCreateIndexDDL(
        string physicalIndexName,
        string partitionName,
        bool isShared,
        int clientId,
        string collection,
        string tableName,
        IndexDefinition indexDef)
    {
        var sb = new StringBuilder();
        sb.Append($"CREATE INDEX CONCURRENTLY IF NOT EXISTS \"{physicalIndexName}\" ");
        sb.Append($"ON vibe.\"{partitionName}\" ");

        // Index type
        sb.Append($"USING {indexDef.IndexType} (");

        // Index expressions
        var expressions = indexDef.Fields.Select(f => $"(data->>'{f}')").ToList();
        sb.Append(string.Join(", ", expressions));
        sb.Append(") ");

        // WHERE clause (always include deleted_at IS NULL + partition filter)
        var whereClauses = new List<string> { "deleted_at IS NULL" };

        if (isShared)
        {
            whereClauses.Add($"client_id = {clientId}");
        }

        whereClauses.Add($"collection = '{EscapeSQLString(collection)}'");
        whereClauses.Add($"table_name = '{EscapeSQLString(tableName)}'");

        if (!string.IsNullOrEmpty(indexDef.PartialCondition))
        {
            whereClauses.Add($"({indexDef.PartialCondition})");
        }

        sb.Append($"WHERE {string.Join(" AND ", whereClauses)}");

        return sb.ToString();
    }

    private string ComputeMD5Hash(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    private string EscapeSQLString(string input)
    {
        return input.Replace("'", "''");
    }
}
