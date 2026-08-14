using Microsoft.AspNetCore.Mvc;
using VibeSQL.Core.Models;
using VibeSQL.Core.Query;

namespace VibeSQL.Server.Controllers.V1;

/// <summary>
/// Raw SQL query execution endpoint - core VibeSQL functionality.
/// Ported from the hardened PayEz.VibeSql.Server.Api binary.
/// </summary>
[ApiController]
[Route("v1")]
[Produces("application/json")]
public class QueryController : ControllerBase
{
    private readonly IQueryExecutor _executor;
    private readonly ILogger<QueryController> _logger;

    public QueryController(
        IQueryExecutor executor,
        ILogger<QueryController> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    /// <summary>
    /// Execute a SQL query.
    /// </summary>
    /// <remarks>
    /// Executes a raw SQL query against the configured PostgreSQL database with:
    /// - Query validation (size limits, valid SQL keywords)
    /// - Safety checks (UPDATE/DELETE require WHERE clause)
    /// - Tenant isolation (RLS via SET LOCAL app.client_id)
    /// - Result limits (configurable, default 1000 rows)
    /// - Query timeout (tier-based)
    /// </remarks>
    [HttpPost("query")]
    [ProducesResponseType(typeof(QueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExecuteQuery([FromBody] QueryRequest request)
    {
        var tier = HttpContext.Items["ClientTier"] as string;
        var clientId = HttpContext.Items["ClientId"] as int?;

        try
        {
            _logger.LogDebug("VIBE_QUERY_ENDPOINT: Received query request");

            var result = await _executor.ExecuteAsync(request.Sql, tier, clientId,
                new QueryAuditContext(
                    HttpContext.Request.Path.ToString(),
                    HttpContext.Request.Method,
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    HttpContext.Request.Headers.UserAgent.ToString()),
                HttpContext.RequestAborted);

            return Ok(new QueryResponse
            {
                Success = true,
                Data = result.Rows,
                Meta = new QueryMetadata
                {
                    RowCount = result.RowCount,
                    ExecutionTimeMs = result.ExecutionTimeMs
                }
            });
        }
        catch (VibeQueryError ex)
        {
            _logger.LogWarning("VIBE_QUERY_ENDPOINT: Query error - {Code}: {Message}", ex.Code, ex.Message);
            return StatusCode(ex.HttpStatusCode, ex.ToResponse());
        }
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "vibe-server-api", version = "2.0.0" });
    }
}
