using Microsoft.AspNetCore.Mvc;
using Npgsql;
using VibeSQL.Core.Query;

namespace VibeSQL.Server.Controllers.V1;

[ApiController]
[Route("v1")]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private readonly string _connectionString;
    private readonly IClientIdResolver _clientIdResolver;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IConfiguration configuration,
        IClientIdResolver clientIdResolver,
        ILogger<DocumentsController> logger)
    {
        _connectionString = configuration.GetConnectionString("VibeDb")
            ?? throw new InvalidOperationException("VibeDb connection string not configured");
        _clientIdResolver = clientIdResolver;
        _logger = logger;
    }

    /// <summary>
    /// Insert a document into a collection table.
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
        if (string.IsNullOrWhiteSpace(request.ClientId))
            return BadRequest(ErrorResponse("MISSING_REQUIRED_FIELD", "client_id is required"));

        var resolvedClientId = await _clientIdResolver.ResolveAsync(request.ClientId);
        if (!resolvedClientId.HasValue)
            return BadRequest(ErrorResponse("CLIENT_NOT_FOUND", $"Client '{request.ClientId}' not found"));

        if (string.IsNullOrWhiteSpace(request.Data))
            return BadRequest(ErrorResponse("MISSING_REQUIRED_FIELD", "data is required"));

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Set the tenant context this connection operates under, BEFORE any statement
            // touches vibe.documents.
            //
            // Every tenant table carries RLS with FORCE and a tenant_isolation policy of
            // (client_id = current_setting('app.client_id')::int OR client_id = 0). Without
            // this, current_setting returns NULL, the policy evaluates false, and the INSERT
            // is rejected with "new row violates row-level security policy".
            //
            // It was invisible while the service connected as a superuser — superusers bypass
            // RLS entirely, so the policy never ran. It surfaced the moment the connection
            // used a least-privilege role. Anywhere this path currently works without setting
            // context, it is working because the role bypasses tenant isolation.
            //
            // is_local: false — the setting must outlive an implicit transaction and apply to
            // every command on this connection, including the audit write below.
            await using (var ctx = new NpgsqlCommand(
                "SELECT set_config('app.client_id', @client_id, false)", connection))
            {
                ctx.Parameters.Add(new NpgsqlParameter("client_id",
                    resolvedClientId.Value.ToString()));
                await ctx.ExecuteNonQueryAsync(cancellationToken);
            }

            var sql = @"INSERT INTO vibe.documents
                            (client_id, user_id, collection, table_name, data, collection_schema_id, created_at, created_by)
                        VALUES
                            (@client_id, @user_id, @collection, @table_name, @data::jsonb, @collection_schema_id, now(), @created_by)
                        RETURNING document_id, client_id, user_id, collection, table_name, data,
                                  collection_schema_id, created_at, created_by";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.Add(new NpgsqlParameter("client_id", resolvedClientId.Value));
            cmd.Parameters.Add(new NpgsqlParameter("user_id",
                request.UserId.HasValue ? (object)request.UserId.Value : DBNull.Value));
            cmd.Parameters.Add(new NpgsqlParameter("collection", collection));
            cmd.Parameters.Add(new NpgsqlParameter("table_name", table));
            cmd.Parameters.Add(new NpgsqlParameter("data", request.Data));
            cmd.Parameters.Add(new NpgsqlParameter("collection_schema_id",
                request.CollectionSchemaId.HasValue ? (object)request.CollectionSchemaId.Value : DBNull.Value));
            cmd.Parameters.Add(new NpgsqlParameter("created_by",
                request.CreatedBy.HasValue ? (object)request.CreatedBy.Value : DBNull.Value));

            Dictionary<string, object?>? row = null;

            // Reader is scoped tightly: Npgsql has no MARS, so the audit command below
            // cannot run on this connection while a reader is open.
            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
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
            }

            await WriteAuditAsync(connection, resolvedClientId.Value, request,
                row? ["document_id"], collection, table, cancellationToken);

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
        catch (PostgresException pgEx)
        {
            _logger.LogError(pgEx, "VIBESQL_DOCUMENTS: PostgreSQL error inserting document");
            var error = SqlStateMapper.TranslatePostgresError(pgEx);
            return StatusCode(error.HttpStatusCode, error.ToResponse());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "VIBESQL_DOCUMENTS: Unexpected error inserting document");
            return StatusCode(500, ErrorResponse("INTERNAL_ERROR", "An internal error occurred"));
        }
    }

    /// <summary>
    /// Writes the Req 10 audit row for a document insert.
    /// </summary>
    /// <remarks>
    /// This is the first producer for vibe.audit_logs. The table, its indexes, its RLS
    /// policy and its repository all existed for months with nothing writing to them —
    /// 99 rows, newest 2026-06-17, on a box under daily development. Registering the
    /// repository made it resolvable; it did not make audit rows appear. This does.
    ///
    /// It writes here, in the controller, rather than through IVibeAuditLogRepository,
    /// because THIS is the live write path: DocumentsController talks to Postgres
    /// directly with Npgsql and never touches the repository layer. Auditing from the
    /// repository would have produced a trail of schema migrations and nothing else —
    /// a call site that never fires, which is the exact defect this work exists to undo.
    ///
    /// admin_user_id falls back to the tenant's type='system' user, resolved IN the
    /// statement. Document inserts can legitimately carry no authenticated caller, and
    /// admin_user_id is NOT NULL and must reference a real user in the SAME tenant
    /// (user_id is unique per tenant, not globally).
    ///
    /// FAILS LOUDLY ON ZERO ROWS. If the tenant has no system user the SELECT yields
    /// nothing and the INSERT silently writes nothing — an audit gap that reports
    /// success. The row count is checked and throws instead.
    /// </remarks>
    private async Task WriteAuditAsync(
        NpgsqlConnection connection,
        int clientId,
        DocumentInsertRequest request,
        object? documentId,
        string collection,
        string table,
        CancellationToken cancellationToken)
    {
        const string auditSql = @"
            INSERT INTO vibe.audit_logs
                (client_id, admin_user_id, admin_email, category, action,
                 target_type, target_id, description, is_success,
                 ip_address, user_agent, request_path, http_method, created_at)
            SELECT @client_id,
                   COALESCE(@admin_user_id, sys.uid),
                   COALESCE(@admin_email,   sys.email),
                   'data', 'document.insert',
                   'document', @target_id, @description, true,
                   @ip_address, @user_agent, @request_path, @http_method, now()
            FROM (SELECT (data->>'user_id')::int AS uid, data->>'email' AS email
                    FROM vibe.documents
                   WHERE client_id = @client_id
                     AND collection = 'vibe_app' AND table_name = 'users'
                     AND deleted_at IS NULL
                     AND data->>'type' = 'system'
                   LIMIT 1) sys";

        await using var cmd = new NpgsqlCommand(auditSql, connection);
        cmd.Parameters.Add(new NpgsqlParameter("client_id", clientId));
        cmd.Parameters.Add(new NpgsqlParameter("admin_user_id",
            request.CreatedBy.HasValue ? request.CreatedBy.Value
            : request.UserId.HasValue ? request.UserId.Value
            : (object)DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("admin_email", DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("target_id",
            documentId?.ToString() ?? "unknown"));
        cmd.Parameters.Add(new NpgsqlParameter("description",
            $"Document inserted into {collection}/{table}"));
        cmd.Parameters.Add(new NpgsqlParameter("ip_address",
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"));
        cmd.Parameters.Add(new NpgsqlParameter("user_agent",
            HttpContext.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : "unknown"));
        cmd.Parameters.Add(new NpgsqlParameter("request_path", HttpContext.Request.Path.ToString()));
        cmd.Parameters.Add(new NpgsqlParameter("http_method", HttpContext.Request.Method));

        var written = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (written == 0)
        {
            throw new InvalidOperationException(
                $"Audit write produced no row for client {clientId}: the tenant has no " +
                "type='system' user to attribute an unauthenticated document insert to. " +
                "Seed one (VibeSchemaInitializer does this on boot) before accepting writes.");
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
    public string ClientId { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string Data { get; set; } = string.Empty;
    public int? CollectionSchemaId { get; set; }
    public int? CreatedBy { get; set; }
}
