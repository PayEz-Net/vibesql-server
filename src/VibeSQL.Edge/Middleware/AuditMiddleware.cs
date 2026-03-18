using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Middleware;

public sealed class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditMiddleware> _logger;

    public AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var start = DateTimeOffset.UtcNow;

        await _next(context);

        var providerKey = context.Items[EdgeContextKeys.ProviderKey] as string;
        var userId = context.Items[EdgeContextKeys.UserId];
        var permissionLevel = context.Items[EdgeContextKeys.PermissionLevel] as VibePermissionLevel?;
        var externalSubject = context.Items[EdgeContextKeys.ExternalSubject] as string;

        var statusCode = context.Response.StatusCode;
        var method = context.Request.Method;
        var path = context.Request.Path.Value;
        var elapsed = DateTimeOffset.UtcNow - start;

        var allowed = statusCode < 400;

        _logger.LogInformation(
            "EDGE_AUDIT: {Result} | {Method} {Path} | Status={StatusCode} | Provider={Provider} | User={UserId} | Subject={Subject} | Permission={Permission} | Elapsed={ElapsedMs}ms",
            allowed ? "ALLOW" : "DENY",
            method,
            path,
            statusCode,
            providerKey ?? "none",
            userId?.ToString() ?? "anonymous",
            externalSubject ?? "none",
            permissionLevel?.ToDbString() ?? "none",
            elapsed.TotalMilliseconds.ToString("F1"));
    }
}
