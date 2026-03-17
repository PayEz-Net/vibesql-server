using Microsoft.AspNetCore.Mvc;
using Devart.Data.PostgreSql;
using VibeSQL.Core.Query;

namespace VibeSQL.Server.Controllers.V1;

[ApiController]
[Route("v1")]
[Produces("application/json")]
public class SchemasController : ControllerBase
{
    private readonly string _connectionString;
    private readonly ILogger<SchemasController> _logger;

    public SchemasController(
        IConfiguration configuration,
        ILogger<SchemasController> logger)
    {
        _connectionString = configuration.GetConnectionString("VibeDb")
            ?? throw new InvalidOperationException("VibeDb connection string not configured");
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
            await using var connection = new PgSqlConnection(_connectionString);
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

            await using var cmd = new PgSqlCommand(sql, connection);
            cmd.Parameters.Add(new PgSqlParameter("@collection", collection));
            if (clientId.HasValue)
                cmd.Parameters.Add(new PgSqlParameter("@client_id", clientId.Value));

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
        catch (PgSqlException pgEx)
        {
            _logger.LogError(pgEx, "VIBESQL_SCHEMAS: PostgreSQL error listing versions");
            var error = SqlStateMapper.TranslateDevartError(pgEx);
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
            await using var connection = new PgSqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Transaction: get max version, deactivate old, insert new
            var tx = connection.BeginTransaction();

            // Get current max version
            var maxVersionSql = @"SELECT COALESCE(MAX(version), 0)
                                  FROM vibe.collection_schemas
                                  WHERE client_id = @client_id AND collection = @collection";

            await using var maxCmd = new PgSqlCommand(maxVersionSql, connection, tx);
            maxCmd.Parameters.Add(new PgSqlParameter("@client_id", request.ClientId));
            maxCmd.Parameters.Add(new PgSqlParameter("@collection", collection));

            var maxVersion = Convert.ToInt32(await maxCmd.ExecuteScalarAsync(cancellationToken));
            var newVersion = maxVersion + 1;

            // Deactivate all existing versions
            var deactivateSql = @"UPDATE vibe.collection_schemas
                                  SET is_active = false, updated_at = now()
                                  WHERE client_id = @client_id AND collection = @collection AND is_active = true";

            await using var deactCmd = new PgSqlCommand(deactivateSql, connection, tx);
            deactCmd.Parameters.Add(new PgSqlParameter("@client_id", request.ClientId));
            deactCmd.Parameters.Add(new PgSqlParameter("@collection", collection));
            await deactCmd.ExecuteNonQueryAsync(cancellationToken);

            // Insert new version
            var insertSql = @"INSERT INTO vibe.collection_schemas
                                (client_id, collection, json_schema, version, is_active, created_at, created_by)
                              VALUES
                                (@client_id, @collection, @json_schema::jsonb, @version, true, now(), @created_by)
                              RETURNING collection_schema_id, client_id, collection, json_schema,
                                        version, is_active, is_system, is_locked, created_at, created_by";

            await using var insertCmd = new PgSqlCommand(insertSql, connection, tx);
            insertCmd.Parameters.Add(new PgSqlParameter("@client_id", request.ClientId));
            insertCmd.Parameters.Add(new PgSqlParameter("@collection", collection));
            insertCmd.Parameters.Add(new PgSqlParameter("@json_schema", request.JsonSchema));
            insertCmd.Parameters.Add(new PgSqlParameter("@version", newVersion));
            insertCmd.Parameters.Add(new PgSqlParameter("@created_by",
                request.CreatedBy.HasValue ? (object)request.CreatedBy.Value : DBNull.Value));

            var rows = await ReadRowsAsync(insertCmd, cancellationToken);
            tx.Commit();

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
        catch (PgSqlException pgEx)
        {
            _logger.LogError(pgEx, "VIBESQL_SCHEMAS: PostgreSQL error creating/updating schema");
            var error = SqlStateMapper.TranslateDevartError(pgEx);
            return StatusCode(error.HttpStatusCode, error.ToResponse());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "VIBESQL_SCHEMAS: Unexpected error creating/updating schema");
            return StatusCode(500, ErrorResponse("INTERNAL_ERROR", "An internal error occurred"));
        }
    }

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(
        PgSqlCommand cmd, CancellationToken cancellationToken)
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
