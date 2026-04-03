using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.Json;
using VibeSQL.Core.Options;
using VibeSQL.Core.Query;
using VibeSQL.Sentinel;

namespace VibeSQL.Server.Controllers.V1;

[ApiController]
[Route("v1")]
[Produces("application/json")]
public class SchemasController : ControllerBase
{
    private readonly string _connectionString;
    private readonly ISchemaDiffEngine _diffEngine;
    private readonly IChangeClassifier _classifier;
    private readonly IDataInspector? _inspector;
    private readonly VibeSentinelOptions _sentinelOptions;
    private readonly ILogger<SchemasController> _logger;

    public SchemasController(
        IConfiguration configuration,
        ISchemaDiffEngine diffEngine,
        IChangeClassifier classifier,
        IServiceProvider serviceProvider,
        ILogger<SchemasController> logger)
    {
        _connectionString = configuration.GetConnectionString("VibeDb")
            ?? throw new InvalidOperationException("VibeDb connection string not configured");
        _diffEngine = diffEngine;
        _classifier = classifier;
        // Inspector is optional - may be null if not registered
        _inspector = serviceProvider.GetService<IDataInspector>();
        _sentinelOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<VibeSentinelOptions>>().Value;
        _logger = logger;
    }

    /// <summary>
    /// List all schema versions for a collection
    /// </summary>
    [HttpGet("schemas/{collection}/versions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetVersions(
        string collection,
        [FromQuery(Name = "client_id")] int? clientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(collection))
            return BadRequest(ErrorResponse("MISSING_REQUIRED_FIELD", "Collection name is required"));

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = clientId.HasValue
                ? @"SELECT collection_schema_id, client_id, collection, json_schema,
                           version, is_active, is_system, is_locked,
                           created_at, created_by, updated_at, updated_by
                    FROM vibe.collection_schemas
                    WHERE collection = @collection AND client_id = @client_id
                    ORDER BY version DESC"
                : @"SELECT collection_schema_id, client_id, collection, json_schema,
                           version, is_active, is_system, is_locked,
                           created_at, created_by, updated_at, updated_by
                    FROM vibe.collection_schemas
                    WHERE collection = @collection
                    ORDER BY version DESC";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.Add(new NpgsqlParameter("@collection", collection));
            if (clientId.HasValue)
                cmd.Parameters.Add(new NpgsqlParameter("@client_id", clientId.Value));

            var rows = await ReadRowsAsync(cmd, cancellationToken);

            _logger.LogInformation("VIBESQL_SCHEMAS: Listed {Count} versions for collection '{Collection}'",
                rows.Count, collection);

            return Ok(new
            {
                success = true,
                data = rows,
                meta = new { rowCount = rows.Count }
            });
        }
        catch (PostgresException pgEx)
        {
            _logger.LogError(pgEx, "VIBESQL_SCHEMAS: PostgreSQL error listing versions");
            var error = SqlStateMapper.TranslatePostgresError(pgEx);
            return StatusCode(error.HttpStatusCode, error.ToResponse());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "VIBESQL_SCHEMAS: Unexpected error listing versions");
            return StatusCode(500, ErrorResponse("INTERNAL_ERROR", "An internal error occurred"));
        }
    }

    /// <summary>
    /// Create or update a schema for a collection.
    /// Creates a new version and deactivates previous versions.
    /// 
    /// Schema Sentinel (VS-SS) will block destructive changes unless overridden.
    /// Use X-Vibe-Force-Schema-Update: true header to override Destructive (409) changes.
    /// Prohibited (422) changes cannot be overridden.
    /// </summary>
    [HttpPut("schemas/{collection}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateOrUpdateSchema(
        string collection,
        [FromBody] SchemaUpdateRequest request,
        CancellationToken cancellationToken)
    {
        // Q7: Validate JSON before parsing
        if (string.IsNullOrWhiteSpace(collection))
            return BadRequest(ErrorResponse("MISSING_REQUIRED_FIELD", "Collection name is required"));
        if (request.ClientId <= 0)
            return BadRequest(ErrorResponse("MISSING_REQUIRED_FIELD", "client_id is required and must be positive"));
        if (string.IsNullOrWhiteSpace(request.JsonSchema))
            return BadRequest(ErrorResponse("MISSING_REQUIRED_FIELD", "json_schema is required"));

        // Parse and validate JSON schema
        JsonDocument? proposedDoc = null;
        try
        {
            proposedDoc = JsonDocument.Parse(request.JsonSchema);
        }
        catch (JsonException ex)
        {
            return BadRequest(ErrorResponse("INVALID_JSON", $"Invalid JSON schema: {ex.Message}"));
        }

        // Ensure disposal of JsonDocument when done
        using (proposedDoc)
        {
            // Q1: Structural validation on CREATE (always run)
            var structuralItems = ValidateStructuralConstraints(proposedDoc, collection);
        var prohibitedOnCreate = structuralItems.Where(i => i.Level == SentinelVerdict.Prohibited).ToList();
        if (prohibitedOnCreate.Any())
        {
            return StatusCode(422, new
            {
                success = false,
                error = new
                {
                    code = "SCHEMA_PROHIBITED",
                    message = "Schema change blocked by Sentinel (Prohibited). This change cannot be overridden."
                },
                sentinel = new
                {
                    verdict = "Prohibited",
                    canOverride = false,
                    riskItems = prohibitedOnCreate.Select(i => new
                    {
                        code = i.Code,
                        description = i.Description,
                        tableName = i.TableName,
                        columnName = i.ColumnName
                    })
                }
            });
        }

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Get current schema (if exists) for diff
            var currentSchemaSql = @"SELECT json_schema, version, is_locked
                                      FROM vibe.collection_schemas
                                      WHERE client_id = @client_id AND collection = @collection AND is_active = true
                                      ORDER BY version DESC
                                      LIMIT 1";

            await using var currentCmd = new NpgsqlCommand(currentSchemaSql, connection);
            currentCmd.Parameters.Add(new NpgsqlParameter("@client_id", request.ClientId));
            currentCmd.Parameters.Add(new NpgsqlParameter("@collection", collection));

            string? currentSchemaJson = null;
            int currentVersion = 0;
            bool isLocked = false;

            await using var currentReader = await currentCmd.ExecuteReaderAsync(cancellationToken);
            if (await currentReader.ReadAsync(cancellationToken))
            {
                currentSchemaJson = currentReader["json_schema"] as string;
                currentVersion = currentReader["version"] as int? ?? 0;
                isLocked = currentReader["is_locked"] as bool? ?? false;
            }
            await currentReader.CloseAsync();

            // Check if schema is locked
            if (isLocked)
            {
                return BadRequest(ErrorResponse("SCHEMA_LOCKED",
                    $"Collection '{collection}' schema is locked and cannot be modified"));
            }

            // Run Sentinel pipeline on UPDATE (when current schema exists)
            if (currentSchemaJson != null && _sentinelOptions.Enabled)
            {
                using var currentDoc = JsonDocument.Parse(currentSchemaJson);
                var verdict = await RunSentinelPipeline(
                    currentDoc, proposedDoc, collection, request.ClientId, connection, cancellationToken);

                // Check for override
                var forceOverride = Request.Headers.TryGetValue("X-Vibe-Force-Schema-Update", out var forceHeader)
                    && forceHeader.ToString().Equals("true", StringComparison.OrdinalIgnoreCase)
                    && verdict.Verdict == SentinelVerdict.Destructive; // NEVER for Prohibited

                if (verdict.Blocked && !forceOverride)
                {
                    var statusCode = verdict.Verdict == SentinelVerdict.Prohibited ? 422 : 409;
                    var errorCode = verdict.Verdict == SentinelVerdict.Prohibited
                        ? "SCHEMA_PROHIBITED"
                        : "SCHEMA_DESTRUCTIVE";

                    return StatusCode(statusCode, new
                    {
                        success = false,
                        error = new
                        {
                            code = errorCode,
                            message = $"Schema change blocked by Sentinel ({verdict.Verdict}). " +
                                $"{verdict.RiskReport.Count} item(s) flagged."
                        },
                        sentinel = new
                        {
                            verdict = verdict.Verdict.ToString(),
                            canOverride = verdict.Verdict == SentinelVerdict.Destructive,
                            riskItems = verdict.RiskReport.Select(r => new
                            {
                                code = r.Code,
                                description = r.Description,
                                tableName = r.TableName,
                                columnName = r.ColumnName,
                                rowCount = r.RowCount,
                                dataAtRisk = r.DataAtRisk,
                                detail = r.Detail
                            })
                        }
                    });
                }

                if (forceOverride)
                {
                    _logger.LogWarning(
                        "SENTINEL_OVERRIDE: Destructive change forced. Collection={Collection}, ClientId={ClientId}, RiskItems={Count}",
                        collection, request.ClientId, verdict.RiskReport.Count);
                }
                else
                {
                    _logger.LogInformation(
                        "SENTINEL_PASSED: Collection={Collection}, Verdict={Verdict}, AutoApply={AutoApply}",
                        collection, verdict.Verdict, verdict.AutoApply);
                }
            }

            // Transaction: get max version, deactivate old, insert new
            var tx = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                // Get current max version
                var maxVersionSql = @"SELECT COALESCE(MAX(version), 0)
                                      FROM vibe.collection_schemas
                                      WHERE client_id = @client_id AND collection = @collection";

                await using var maxCmd = new NpgsqlCommand(maxVersionSql, connection, tx);
                maxCmd.Parameters.Add(new NpgsqlParameter("@client_id", request.ClientId));
                maxCmd.Parameters.Add(new NpgsqlParameter("@collection", collection));

                var maxVersion = Convert.ToInt32(await maxCmd.ExecuteScalarAsync(cancellationToken));
                var newVersion = maxVersion + 1;

                // Deactivate all existing versions
                var deactivateSql = @"UPDATE vibe.collection_schemas
                                      SET is_active = false, updated_at = now()
                                      WHERE client_id = @client_id AND collection = @collection AND is_active = true";

                await using var deactCmd = new NpgsqlCommand(deactivateSql, connection, tx);
                deactCmd.Parameters.Add(new NpgsqlParameter("@client_id", request.ClientId));
                deactCmd.Parameters.Add(new NpgsqlParameter("@collection", collection));
                await deactCmd.ExecuteNonQueryAsync(cancellationToken);

                // Insert new version
                var insertSql = @"INSERT INTO vibe.collection_schemas
                                    (client_id, collection, json_schema, version, is_active, created_at, created_by)
                                  VALUES
                                    (@client_id, @collection, @json_schema::jsonb, @version, true, now(), @created_by)
                                  RETURNING collection_schema_id, client_id, collection, json_schema,
                                            version, is_active, is_system, is_locked, created_at, created_by";

                await using var insertCmd = new NpgsqlCommand(insertSql, connection, tx);
                insertCmd.Parameters.Add(new NpgsqlParameter("@client_id", request.ClientId));
                insertCmd.Parameters.Add(new NpgsqlParameter("@collection", collection));
                insertCmd.Parameters.Add(new NpgsqlParameter("@json_schema", request.JsonSchema));
                insertCmd.Parameters.Add(new NpgsqlParameter("@version", newVersion));
                insertCmd.Parameters.Add(new NpgsqlParameter("@created_by",
                    request.CreatedBy.HasValue ? (object)request.CreatedBy.Value : DBNull.Value));

                var rows = await ReadRowsAsync(insertCmd, cancellationToken);
                await tx.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "VIBESQL_SCHEMAS: Created schema v{Version} for collection '{Collection}' (client_id={ClientId})",
                    newVersion, collection, request.ClientId);

                var statusCode = currentVersion == 0 ? 201 : 200;
                return StatusCode(statusCode, new
                {
                    success = true,
                    data = rows.FirstOrDefault(),
                    meta = new { version = newVersion, previousVersion = maxVersion }
                });
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (PostgresException pgEx)
        {
            _logger.LogError(pgEx, "VIBESQL_SCHEMAS: PostgreSQL error creating/updating schema");
            var error = SqlStateMapper.TranslatePostgresError(pgEx);
            return StatusCode(error.HttpStatusCode, error.ToResponse());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "VIBESQL_SCHEMAS: Unexpected error creating/updating schema");
            return StatusCode(500, ErrorResponse("INTERNAL_ERROR", "An internal error occurred"));
        }
        } // End of proposedDoc using block
    }

    /// <summary>
    /// Run the Schema Sentinel pipeline: Diff → Classify → Inspect → Verdict.
    /// </summary>
    private async Task<SentinelVerdictResult> RunSentinelPipeline(
        JsonDocument currentDoc,
        JsonDocument proposedDoc,
        string collection,
        int clientId,
        NpgsqlConnection connection,
        CancellationToken ct)
    {
        try
        {
            var pipeline = new SentinelPipeline(_diffEngine, _classifier, _inspector, null);

            // Get table count for P-401 detection
            var tableCount = await GetTableCountAsync(connection, clientId, collection, ct);
            var context = new ClassifierContext(
                ExistingTableCount: tableCount,
                IsSystemCollection: collection.StartsWith("vibe_") || collection.StartsWith("system_"));

            return await pipeline.AnalyzeAsync(currentDoc, proposedDoc, context, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SENTINEL_PIPELINE_ERROR: Collection={Collection}, ClientId={ClientId}", collection, clientId);
            // Sentinel failure = block (never auto-allow when uncertain)
            return new SentinelVerdictResult
            {
                Verdict = SentinelVerdict.Destructive,
                Diff = null,
                Classification = null,
                Inspection = null
            };
        }
    }

    /// <summary>
    /// Get the number of tables in the current schema for P-401 detection.
    /// </summary>
    private async Task<int> GetTableCountAsync(NpgsqlConnection connection, int clientId, string collection, CancellationToken ct)
    {
        try
        {
            var sql = @"SELECT json_schema
                        FROM vibe.collection_schemas
                        WHERE client_id = @client_id AND collection = @collection AND is_active = true
                        LIMIT 1";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.Add(new NpgsqlParameter("@client_id", clientId));
            cmd.Parameters.Add(new NpgsqlParameter("@collection", collection));

            var result = await cmd.ExecuteScalarAsync(ct);
            if (result == null || result == DBNull.Value)
                return 0;

            using var doc = JsonDocument.Parse(result.ToString()!);
            if (doc.RootElement.TryGetProperty("tables", out var tables) && tables.ValueKind == JsonValueKind.Object)
            {
                return tables.EnumerateObject().Count();
            }

            return doc.RootElement.TryGetProperty("properties", out _) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Q1: Structural validation on CREATE (no current schema to diff against).
    /// Checks for P-403 and other structural issues.
    /// </summary>
    private static List<SentinelItem> ValidateStructuralConstraints(JsonDocument schema, string collection)
    {
        var items = new List<SentinelItem>();

        // P-403: System collection guard
        if (collection.StartsWith("vibe_", StringComparison.OrdinalIgnoreCase) ||
            collection.StartsWith("system_", StringComparison.OrdinalIgnoreCase))
        {
            items.Add(new SentinelItem(
                SentinelTaxonomy.P403_ModifySystemSchema,
                SentinelVerdict.Prohibited,
                "Cannot create system collection schema via API"));
        }

        // Check if schema has tables
        var root = schema.RootElement;
        var hasTables = root.TryGetProperty("tables", out var tables) &&
                       tables.ValueKind == JsonValueKind.Object &&
                       tables.EnumerateObject().Any();
        var hasProperties = root.TryGetProperty("properties", out _);

        if (!hasTables && !hasProperties)
        {
            items.Add(new SentinelItem(
                "V-001",
                SentinelVerdict.Prohibited,
                "Schema must contain at least one table or properties definition"));
        }

        return items;
    }

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(
        NpgsqlCommand cmd, CancellationToken cancellationToken)
    {
        var results = new List<Dictionary<string, object?>>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var columnCount = reader.FieldCount;
        var columnNames = new string[columnCount];
        for (int i = 0; i < columnCount; i++)
            columnNames[i] = reader.GetName(i);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < columnCount; i++)
            {
                var value = reader.GetValue(i);
                row[columnNames[i]] = value == DBNull.Value ? null : ConvertValue(value);
            }
            results.Add(row);
        }

        return results;
    }

    private static object? ConvertValue(object value) => value switch
    {
        byte[] bytes => Convert.ToBase64String(bytes),
        DateTime dt => dt.ToString("O"),
        DateTimeOffset dto => dto.ToString("O"),
        TimeSpan ts => ts.ToString(),
        Guid guid => guid.ToString(),
        _ => value
    };

    private static object ErrorResponse(string code, string message) => new
    {
        success = false,
        error = new { code, message }
    };
}

public class SchemaUpdateRequest
{
    public int ClientId { get; set; }
    public string JsonSchema { get; set; } = string.Empty;
    public int? CreatedBy { get; set; }
}
