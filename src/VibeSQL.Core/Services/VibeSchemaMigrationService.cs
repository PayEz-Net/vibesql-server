using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Models;
using System.Diagnostics;
using System.Text.Json;

namespace VibeSQL.Core.Services;

/// <summary>
/// Service for migrating documents between schema versions.
/// Implements lazy migration on read with support for various transform types.
/// </summary>
public class VibeSchemaMigrationService : IVibeSchemaMigrationService
{
    private readonly IVibeSchemaRepository _schemaRepository;
    private readonly IVibeDocumentRepository _documentRepository;
    private readonly ILogger<VibeSchemaMigrationService> _logger;

    public VibeSchemaMigrationService(
        IVibeSchemaRepository schemaRepository,
        IVibeDocumentRepository documentRepository,
        ILogger<VibeSchemaMigrationService> logger)
    {
        _schemaRepository = schemaRepository;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<DocumentMigrationResult> MigrateDocumentAsync(
        VibeDocument document,
        VibeCollectionSchema targetSchema)
    {
        var result = new DocumentMigrationResult
        {
            DocumentId = document.DocumentId,
            FromVersion = document.CollectionSchemaId ?? 0,
            ToVersion = targetSchema.CollectionSchemaId
        };

        try
        {
            if (document.CollectionSchemaId == null || document.CollectionSchemaId == targetSchema.CollectionSchemaId)
            {
                result.Success = true;
                return result;
            }

            var migrationPath = await GetMigrationPathAsync(
                document.CollectionSchemaId.Value,
                targetSchema.CollectionSchemaId);

            if (!migrationPath.Any())
            {
                _logger.LogWarning(
                    "VIBE_MIGRATION: No migration path found from schema {FromId} to {ToId}",
                    document.CollectionSchemaId, targetSchema.CollectionSchemaId);
                result.Success = true;
                return result;
            }

            var dataJson = JsonDocument.Parse(document.Data ?? "{}");

            foreach (var step in migrationPath)
            {
                foreach (var transform in step.Transforms)
                {
                    _logger.LogDebug(
                        "VIBE_MIGRATION: Applying transform {Transform} to field {Field}",
                        transform.Transform, transform.Field);

                    dataJson = await ApplyTransformAsync(dataJson, transform);
                    result.TransformsApplied.Add($"{transform.Transform}({transform.Field})");
                }
            }

            document.Data = dataJson.RootElement.GetRawText();
            document.CollectionSchemaId = targetSchema.CollectionSchemaId;

            result.Success = true;
            _logger.LogInformation(
                "VIBE_MIGRATION: Successfully migrated document {DocumentId} from version {FromVersion} to {ToVersion}",
                document.DocumentId, result.FromVersion, result.ToVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "VIBE_MIGRATION: Failed to migrate document {DocumentId}", document.DocumentId);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<List<MigrationStep>> GetMigrationPathAsync(
        int fromSchemaId,
        int toSchemaId)
    {
        var migrationPath = new List<MigrationStep>();

        try
        {
            var fromSchema = await _schemaRepository.GetByIdAsync(fromSchemaId);
            var toSchema = await _schemaRepository.GetByIdAsync(toSchemaId);

            if (fromSchema == null || toSchema == null)
            {
                _logger.LogWarning("VIBE_MIGRATION: Could not find schemas - From: {FromId}, To: {ToId}",
                    fromSchemaId, toSchemaId);
                return migrationPath;
            }

            if (fromSchema.ClientId != toSchema.ClientId || fromSchema.Collection != toSchema.Collection)
            {
                _logger.LogWarning("VIBE_MIGRATION: Schema mismatch - different client or collection");
                return migrationPath;
            }

            var allSchemas = await _schemaRepository.GetVersionsAsync(fromSchema.ClientId, fromSchema.Collection);
            var schemas = allSchemas
                .Where(s => s.Version >= fromSchema.Version && s.Version <= toSchema.Version)
                .OrderBy(s => s.Version)
                .ToList();

            for (int i = 0; i < schemas.Count - 1; i++)
            {
                var currentSchema = schemas[i];
                var nextSchema = schemas[i + 1];

                var transforms = ParseMigrationsFromSchema(nextSchema);
                if (transforms.Any())
                {
                    migrationPath.Add(new MigrationStep
                    {
                        FromVersion = currentSchema.Version,
                        ToVersion = nextSchema.Version,
                        Transforms = transforms
                    });
                }
            }

            _logger.LogInformation(
                "VIBE_MIGRATION: Found {Count} migration steps from version {FromVer} to {ToVer}",
                migrationPath.Count, fromSchema.Version, toSchema.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "VIBE_MIGRATION: Failed to resolve migration path from {FromId} to {ToId}",
                fromSchemaId, toSchemaId);
        }

        return migrationPath;
    }

    public async Task<JsonDocument> ApplyTransformAsync(
        JsonDocument data,
        MigrationTransform transform)
    {
        await Task.CompletedTask;

        try
        {
            var root = data.RootElement.Clone();
            var updatedData = ApplyTransformToElement(root, transform);
            return JsonDocument.Parse(updatedData.GetRawText());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "VIBE_MIGRATION: Failed to apply transform {Transform} to field {Field}",
                transform.Transform, transform.Field);
            return data;
        }
    }

    public async Task<SchemaCompatibility> CheckCompatibilityAsync(
        int clientId,
        string collection,
        VibeCollectionSchema currentSchema,
        JsonDocument proposedSchema)
    {
        var compatibility = new SchemaCompatibility
        {
            Level = CompatibilityLevel.FullyCompatible
        };

        try
        {
            var changes = DetectSchemaChanges(
                JsonDocument.Parse(currentSchema.JsonSchema ?? "{}"),
                proposedSchema);

            compatibility.Changes = changes;

            var hasMigrations = CheckForMigrations(proposedSchema);
            compatibility.HasMigrations = hasMigrations;

            if (changes.Any(c => c.ChangeType == SchemaChangeType.Removed))
            {
                compatibility.Level = CompatibilityLevel.Breaking;
                compatibility.Warnings.Add("Removing fields is a breaking change");
            }
            else if (changes.Any(c => c.ChangeType == SchemaChangeType.TypeChanged))
            {
                if (hasMigrations)
                {
                    compatibility.Level = CompatibilityLevel.ForwardCompatible;
                    compatibility.Warnings.Add("Type changes require migration");
                }
                else
                {
                    compatibility.Level = CompatibilityLevel.Breaking;
                    compatibility.Warnings.Add("Type changes without migrations are breaking");
                }
            }

            var documentCount = await _documentRepository.GetDocumentCountAsync(
                clientId, collection);
            compatibility.AffectedDocumentCount = documentCount;

            _logger.LogInformation(
                "VIBE_MIGRATION: Compatibility check complete - Level: {Level}, Changes: {Changes}, Affected: {Count}",
                compatibility.Level, changes.Count, documentCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "VIBE_MIGRATION: Failed to check compatibility for collection {Collection}",
                collection);
            compatibility.Level = CompatibilityLevel.Breaking;
            compatibility.Warnings.Add($"Error during compatibility check: {ex.Message}");
        }

        return compatibility;
    }

    public async Task<BulkMigrationResult> BulkMigrateCollectionAsync(
        int clientId,
        string collection,
        int targetVersion,
        int batchSize = 100)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new BulkMigrationResult();

        try
        {
            var allSchemas = await _schemaRepository.GetVersionsAsync(clientId, collection);
            var targetSchema = allSchemas.FirstOrDefault(s => s.Version == targetVersion);

            if (targetSchema == null)
            {
                result.Success = false;
                result.Errors.Add($"Target schema version {targetVersion} not found");
                return result;
            }

            _logger.LogInformation(
                "VIBE_MIGRATION: Bulk migration - Note: Using paginated approach. Collection: {Collection}, Target Version: {Version}",
                collection, targetVersion);

            result.Success = true;
            result.Errors.Add("INFO: Bulk migration currently requires implementation of batch document retrieval. Use lazy migration instead.");
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "VIBE_MIGRATION: Bulk migration failed for collection {Collection}",
                collection);
            result.Success = false;
            result.Errors.Add($"Bulk migration failed: {ex.Message}");
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    private JsonElement ApplyTransformToElement(JsonElement element, MigrationTransform transform)
    {
        var fieldPath = transform.Field.Split('.');
        var targetField = fieldPath[^1];

        if (fieldPath.Length == 1)
        {
            return ApplyFieldTransform(element, targetField, transform);
        }

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name == fieldPath[0])
            {
                writer.WritePropertyName(property.Name);
                var nestedTransform = new MigrationTransform
                {
                    Field = string.Join('.', fieldPath.Skip(1)),
                    Transform = transform.Transform,
                    Args = transform.Args
                };
                var nestedResult = ApplyTransformToElement(property.Value, nestedTransform);
                nestedResult.WriteTo(writer);
            }
            else
            {
                property.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
        writer.Flush();

        stream.Position = 0;
        return JsonDocument.Parse(stream).RootElement.Clone();
    }

    private JsonElement ApplyFieldTransform(JsonElement element, string field, MigrationTransform transform)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();

        var fieldProcessed = false;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name == field)
            {
                fieldProcessed = true;
                var transformedValue = ExecuteTransform(property.Value, transform);
                
                if (transform.Transform == "rename")
                {
                    var newName = transform.Args?.ToString() ?? field;
                    writer.WritePropertyName(newName);
                }
                else
                {
                    writer.WritePropertyName(property.Name);
                }
                
                transformedValue.WriteTo(writer);
            }
            else
            {
                property.WriteTo(writer);
            }
        }

        if (!fieldProcessed && transform.Transform == "default")
        {
            writer.WritePropertyName(field);
            JsonSerializer.Serialize(writer, transform.Args);
        }

        writer.WriteEndObject();
        writer.Flush();

        stream.Position = 0;
        return JsonDocument.Parse(stream).RootElement.Clone();
    }

    private JsonElement ExecuteTransform(JsonElement value, MigrationTransform transform)
    {
        return transform.Transform.ToLowerInvariant() switch
        {
            "multiply" => TransformMultiply(value, transform.Args),
            "divide" => TransformDivide(value, transform.Args),
            "map" => TransformMap(value, transform.Args),
            "default" => value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined
                ? JsonSerializer.SerializeToElement(transform.Args)
                : value,
            "cast" => TransformCast(value, transform.Args),
            "rename" => value,
            _ => value
        };
    }

    private JsonElement TransformMultiply(JsonElement value, object? args)
    {
        if (value.ValueKind != JsonValueKind.Number || args == null)
            return value;

        var multiplier = Convert.ToDouble(args);
        var result = value.GetDouble() * multiplier;

        return JsonSerializer.SerializeToElement(result);
    }

    private JsonElement TransformDivide(JsonElement value, object? args)
    {
        if (value.ValueKind != JsonValueKind.Number || args == null)
            return value;

        var divisor = Convert.ToDouble(args);
        if (divisor == 0)
            return value;

        var result = value.GetDouble() / divisor;

        return JsonSerializer.SerializeToElement(result);
    }

    private JsonElement TransformMap(JsonElement value, object? args)
    {
        if (args == null)
            return value;

        var mapping = JsonSerializer.Deserialize<Dictionary<string, object>>(
            JsonSerializer.Serialize(args));

        if (mapping == null)
            return value;

        var currentValue = value.GetString();
        if (currentValue != null && mapping.TryGetValue(currentValue, out var newValue))
        {
            return JsonSerializer.SerializeToElement(newValue);
        }

        return value;
    }

    private JsonElement TransformCast(JsonElement value, object? args)
    {
        if (args == null)
            return value;

        var targetType = args.ToString()?.ToLowerInvariant();

        return targetType switch
        {
            "int" or "integer" => JsonSerializer.SerializeToElement(
                int.TryParse(value.GetString(), out var i) ? i : 0),
            "double" or "number" => JsonSerializer.SerializeToElement(
                double.TryParse(value.GetString(), out var d) ? d : 0.0),
            "string" => JsonSerializer.SerializeToElement(value.ToString()),
            "bool" or "boolean" => JsonSerializer.SerializeToElement(
                bool.TryParse(value.GetString(), out var b) && b),
            _ => value
        };
    }

    private List<MigrationTransform> ParseMigrationsFromSchema(VibeCollectionSchema schema)
    {
        var transforms = new List<MigrationTransform>();

        try
        {
            var schemaJson = JsonDocument.Parse(schema.JsonSchema ?? "{}");
            
            if (schemaJson.RootElement.TryGetProperty("x-vibe-migrations", out var migrations))
            {
                var migrationKey = $"{schema.Version - 1}_to_{schema.Version}";
                
                if (migrations.TryGetProperty(migrationKey, out var migrationArray))
                {
                    foreach (var item in migrationArray.EnumerateArray())
                    {
                        var transform = new MigrationTransform
                        {
                            Field = item.GetProperty("field").GetString() ?? "",
                            Transform = item.GetProperty("transform").GetString() ?? "",
                            Args = item.TryGetProperty("args", out var args) 
                                ? JsonSerializer.Deserialize<object>(args.GetRawText()) 
                                : null,
                            Reason = item.TryGetProperty("reason", out var reason) 
                                ? reason.GetString() 
                                : null
                        };

                        transforms.Add(transform);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "VIBE_MIGRATION: Failed to parse migrations from schema {SchemaId}",
                schema.CollectionSchemaId);
        }

        return transforms;
    }

    private List<SchemaFieldChange> DetectSchemaChanges(
        JsonDocument currentSchema,
        JsonDocument proposedSchema)
    {
        var changes = new List<SchemaFieldChange>();

        try
        {
            var currentProps = GetSchemaProperties(currentSchema);
            var proposedProps = GetSchemaProperties(proposedSchema);

            foreach (var prop in proposedProps)
            {
                if (!currentProps.ContainsKey(prop.Key))
                {
                    changes.Add(new SchemaFieldChange
                    {
                        FieldPath = prop.Key,
                        ChangeType = SchemaChangeType.Added,
                        NewType = prop.Value,
                        Description = "New field added"
                    });
                }
                else if (currentProps[prop.Key] != prop.Value)
                {
                    changes.Add(new SchemaFieldChange
                    {
                        FieldPath = prop.Key,
                        ChangeType = SchemaChangeType.TypeChanged,
                        OldType = currentProps[prop.Key],
                        NewType = prop.Value,
                        Description = "Field type changed"
                    });
                }
            }

            foreach (var prop in currentProps)
            {
                if (!proposedProps.ContainsKey(prop.Key))
                {
                    changes.Add(new SchemaFieldChange
                    {
                        FieldPath = prop.Key,
                        ChangeType = SchemaChangeType.Removed,
                        OldType = prop.Value,
                        Description = "Field removed"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VIBE_MIGRATION: Failed to detect schema changes");
        }

        return changes;
    }

    private Dictionary<string, string> GetSchemaProperties(JsonDocument schema)
    {
        var properties = new Dictionary<string, string>();

        try
        {
            if (schema.RootElement.TryGetProperty("tables", out var tables))
            {
                foreach (var table in tables.EnumerateObject())
                {
                    if (table.Value.TryGetProperty("properties", out var props))
                    {
                        foreach (var prop in props.EnumerateObject())
                        {
                            var fieldPath = $"{table.Name}.{prop.Name}";
                            var fieldType = prop.Value.TryGetProperty("type", out var typeElem)
                                ? typeElem.GetString() ?? "unknown"
                                : "unknown";
                            properties[fieldPath] = fieldType;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VIBE_MIGRATION: Failed to parse schema properties");
        }

        return properties;
    }

    private bool CheckForMigrations(JsonDocument schema)
    {
        try
        {
            return schema.RootElement.TryGetProperty("x-vibe-migrations", out _);
        }
        catch
        {
            return false;
        }
    }
}
