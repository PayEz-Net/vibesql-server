using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Query;

namespace VibeSQL.Server.Controllers.V1;

[ApiController]
[Route("v1")]
[Produces("application/json")]
public class SchemasController : ControllerBase
{
    private readonly string _connectionString;
    private readonly ILogger<SchemasController> _logger;
    private readonly IVibeIndexManagementService _indexManagement;

    public SchemasController(
        IConfiguration configuration,
        ILogger<SchemasController> logger,
        IVibeIndexManagementService indexManagement)
    {
        _connectionString = configuration.GetConnectionString("VibeDb")
            ?? throw new InvalidOperationException("VibeDb connection string not configured");
        _logger = logger;
        _indexManagement = indexManagement;
    }

    /// <summary>
    /// List all schema versions for a collection.
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
            cmd.Parameters.Add(new NpgsqlParameter("collection", collection));
            if (clientId.HasValue)
                cmd.Parameters.Add(new NpgsqlParameter("client_id", clientId.Value));

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
    /// </summary>
    [HttpPut("schemas/{collection}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrUpdateSchema(
        string collection,
        [FromBody] SchemaUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(collection))
            return BadRequest(ErrorResponse("MISSING_REQUIRED_FIELD", "Collection name is required"));
        if (request.ClientId <= 0)
            return BadRequest(ErrorResponse("MISSING_REQUIRED_FIELD", "client_id is required and must be positive"));
        if (string.IsNullOrWhiteSpace(request.JsonSchema))
            return BadRequest(ErrorResponse("MISSING_REQUIRED_FIELD", "json_schema is required"));

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Transaction: get max version, deactivate old, insert new
            await using var tx = await connection.BeginTransactionAsync(cancellationToken);

            // Get current max version
            var maxVersionSql = @"SELECT COALESCE(MAX(version), 0)
                                  FROM vibe.collection_schemas
                                  WHERE client_id = @client_id AND collection = @collection";

            await using var maxCmd = new NpgsqlCommand(maxVersionSql, connection, tx);
            maxCmd.Parameters.Add(new NpgsqlParameter("client_id", request.ClientId));
            maxCmd.Parameters.Add(new NpgsqlParameter("collection", collection));

            var maxVersion = Convert.ToInt32(await maxCmd.ExecuteScalarAsync(cancellationToken));
            var newVersion = maxVersion + 1;

            // Deactivate all existing versions
            var deactivateSql = @"UPDATE vibe.collection_schemas
                                  SET is_active = false, updated_at = now()
                                  WHERE client_id = @client_id AND collection = @collection AND is_active = true";

            await using var deactCmd = new NpgsqlCommand(deactivateSql, connection, tx);
            deactCmd.Parameters.Add(new NpgsqlParameter("client_id", request.ClientId));
            deactCmd.Parameters.Add(new NpgsqlParameter("collection", collection));
            await deactCmd.ExecuteNonQueryAsync(cancellationToken);

            // Insert new version
            var insertSql = @"INSERT INTO vibe.collection_schemas
                                (client_id, collection, json_schema, version, is_active, created_at, created_by)
                              VALUES
                                (@client_id, @collection, @json_schema::jsonb, @version, true, now(), @created_by)
                              RETURNING collection_schema_id, client_id, collection, json_schema,
                                        version, is_active, is_system, is_locked, created_at, created_by";

            await using var insertCmd = new NpgsqlCommand(insertSql, connection, tx);
            insertCmd.Parameters.Add(new NpgsqlParameter("client_id", request.ClientId));
            insertCmd.Parameters.Add(new NpgsqlParameter("collection", collection));
            insertCmd.Parameters.Add(new NpgsqlParameter("json_schema", request.JsonSchema));
            insertCmd.Parameters.Add(new NpgsqlParameter("version", newVersion));
            insertCmd.Parameters.Add(new NpgsqlParameter("created_by",
                request.CreatedBy.HasValue ? (object)request.CreatedBy.Value : DBNull.Value));

            var rows = await ReadRowsAsync(insertCmd, cancellationToken);
            await tx.CommitAsync(cancellationToken);

            // Virtual-index sync (card 209106 / 186214 lane 3): the service
            // interface's own doc names schema create/update as its trigger.
            // Runs POST-COMMIT by design - the version row is already durable,
            // so a sync failure CANNOT fail the schema write. Failure mode,
            // stated explicitly: a failed or partial sync leaves virtual_indexes
            // stale relative to the new schema version; it is logged loud and is
            // recoverable by re-PUTting the schema (sync is idempotent - existing
            // indexes are skipped, orphans dropped).
            try
            {
                using var schemaDoc = JsonDocument.Parse(request.JsonSchema);
                var syncResults = await _indexManagement.SyncIndexesForSchemaAsync(
                    request.ClientId, collection, schemaDoc);
                var failed = syncResults.Count(r => !r.Success);
                if (failed > 0)
                {
                    _logger.LogWarning(
                        "VIBESQL_SCHEMAS: index sync incomplete for collection '{Collection}' (client_id={ClientId}): {Failed}/{Total} index operations failed - schema v{Version} stands; re-PUT to retry",
                        collection, request.ClientId, failed, syncResults.Count, newVersion);
                }
            }
            catch (Exception syncEx) when (syncEx is not OperationCanceledException)
            {
                _logger.LogError(syncEx,
                    "VIBESQL_SCHEMAS: index sync failed post-commit for collection '{Collection}' (client_id={ClientId}) - schema v{Version} stands; re-PUT to retry sync",
                    collection, request.ClientId, newVersion);
            }

            _logger.LogInformation(
                "VIBESQL_SCHEMAS: Created schema v{Version} for collection '{Collection}' (client_id={ClientId})",
                newVersion, collection, request.ClientId);

            return Ok(new
            {
                success = true,
                data = rows.FirstOrDefault(),
                meta = new { version = newVersion, previousVersion = maxVersion }
            });
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
