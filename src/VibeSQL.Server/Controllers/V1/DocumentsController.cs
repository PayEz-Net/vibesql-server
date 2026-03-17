using Microsoft.AspNetCore.Mvc;
using Devart.Data.PostgreSql;
using VibeSQL.Core.Query;

namespace VibeSQL.Server.Controllers.V1;

[ApiController]
[Route("v1")]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private readonly string _connectionString;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IConfiguration configuration,
        ILogger<DocumentsController> logger)
    {
        _connectionString = configuration.GetConnectionString("VibeDb")
            ?? throw new InvalidOperationException("VibeDb connection string not configured");
        _logger = logger;
    }

    /// <summary>
    /// Insert a document into a collection table
    /// </summary>
    [HttpPost("collections/{collection}/tables/{table}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InsertDocument(
        string collection,
        string table,
        [FromBody] DocumentInsertRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(collection))
            return BadRequest(ErrorResponse("MISSING_REQUIRED_FIELD", "Collection name is required"));
        if (string.IsNullOrWhiteSpace(table))
            return BadRequest(ErrorResponse("MISSING_REQUIRED_FIELD", "Table name is required"));
        if (request.ClientId <= 0)
            return BadRequest(ErrorResponse("MISSING_REQUIRED_FIELD", "client_id is required and must be positive"));
        if (string.IsNullOrWhiteSpace(request.Data))
            return BadRequest(ErrorResponse("MISSING_REQUIRED_FIELD", "data is required"));

        try
        {
            await using var connection = new PgSqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"INSERT INTO vibe.documents
                            (client_id, user_id, collection, table_name, data, collection_schema_id, created_at, created_by)
                        VALUES
                            (@client_id, @user_id, @collection, @table_name, @data::jsonb, @collection_schema_id, now(), @created_by)
                        RETURNING document_id, client_id, user_id, collection, table_name, data,
                                  collection_schema_id, created_at, created_by";

            await using var cmd = new PgSqlCommand(sql, connection);
            cmd.Parameters.Add(new PgSqlParameter("@client_id", request.ClientId));
            cmd.Parameters.Add(new PgSqlParameter("@user_id",
                request.UserId.HasValue ? (object)request.UserId.Value : DBNull.Value));
            cmd.Parameters.Add(new PgSqlParameter("@collection", collection));
            cmd.Parameters.Add(new PgSqlParameter("@table_name", table));
            cmd.Parameters.Add(new PgSqlParameter("@data", request.Data));
            cmd.Parameters.Add(new PgSqlParameter("@collection_schema_id",
                request.CollectionSchemaId.HasValue ? (object)request.CollectionSchemaId.Value : DBNull.Value));
            cmd.Parameters.Add(new PgSqlParameter("@created_by",
                request.CreatedBy.HasValue ? (object)request.CreatedBy.Value : DBNull.Value));

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            Dictionary<string, object?>? row = null;

            if (await reader.ReadAsync(cancellationToken))
            {
                row = new Dictionary<string, object?>();
                var columnCount = reader.FieldCount;
                for (int i = 0; i < columnCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[reader.GetName(i)] = value == DBNull.Value ? null : ConvertValue(value);
                }
            }

            _logger.LogInformation(
                "VIBESQL_DOCUMENTS: Inserted document into {Collection}/{Table} (client_id={ClientId})",
                collection, table, request.ClientId);

            return StatusCode(201, new
            {
                success = true,
                data = row,
                meta = new { collection, table }
            });
        }
        catch (PgSqlException pgEx)
        {
            _logger.LogError(pgEx, "VIBESQL_DOCUMENTS: PostgreSQL error inserting document");
            var error = SqlStateMapper.TranslateDevartError(pgEx);
            return StatusCode(error.HttpStatusCode, error.ToResponse());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "VIBESQL_DOCUMENTS: Unexpected error inserting document");
            return StatusCode(500, ErrorResponse("INTERNAL_ERROR", "An internal error occurred"));
        }
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

public class DocumentInsertRequest
{
    public int ClientId { get; set; }
    public int? UserId { get; set; }
    public string Data { get; set; } = string.Empty;
    public int? CollectionSchemaId { get; set; }
    public int? CreatedBy { get; set; }
}
