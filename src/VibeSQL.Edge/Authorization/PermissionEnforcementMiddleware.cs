using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeSQL.Edge;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Authorization;

public sealed class PermissionEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PermissionEnforcementMiddleware> _logger;

    public PermissionEnforcementMiddleware(RequestDelegate next, ILogger<PermissionEnforcementMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var providerKey = context.Items[EdgeContextKeys.ProviderKey] as string;
        var roles = context.Items[EdgeContextKeys.ExtractedRoles] as IReadOnlyList<string>;

        if (providerKey is null || roles is null)
        {
            await _next(context);
            return;
        }

        var vibeClientId = context.Items[EdgeContextKeys.ClientId] as string;

        var resolver = context.RequestServices.GetRequiredService<IPermissionResolver>();
        var permission = await resolver.ResolveAsync(providerKey, roles, vibeClientId, context.RequestAborted);

        context.Items[EdgeContextKeys.PermissionLevel] = permission.EffectiveLevel;
        context.Items[EdgeContextKeys.DeniedStatements] = permission.DeniedStatements;

        var (requiredLevel, statementType) = await DetermineRequiredPermission(context);

        if (requiredLevel is null)
        {
            await _next(context);
            return;
        }

        if (statementType == "ERROR")
        {
            context.Response.StatusCode = 400;
            await WriteJsonError(context, "INVALID_SQL", "Could not classify SQL statement");
            return;
        }

        if (permission.EffectiveLevel < requiredLevel.Value)
        {
            _logger.LogWarning(
                "EDGE_AUTHZ: Permission denied. Required={Required}, Effective={Effective}, Statement={Statement}, Provider={Provider}",
                requiredLevel.Value, permission.EffectiveLevel, statementType, providerKey);

            context.Response.StatusCode = 403;
            await WriteJsonError(context, "PERMISSION_DENIED",
                $"Operation requires '{requiredLevel.Value.ToDbString()}' permission, you have '{permission.EffectiveLevel.ToDbString()}'");
            return;
        }

        if (statementType is not null && IsStatementDenied(statementType, permission.DeniedStatements))
        {
            _logger.LogWarning(
                "EDGE_AUTHZ: Statement denied by role restriction. Statement={Statement}, Provider={Provider}",
                statementType, providerKey);

            context.Response.StatusCode = 403;
            await WriteJsonError(context, "STATEMENT_DENIED",
                $"Statement type '{statementType}' is explicitly denied by your role configuration");
            return;
        }

        await _next(context);
    }

    private async Task<(VibePermissionLevel? level, string? statementType)> DetermineRequiredPermission(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;

        if (path.StartsWith("/v1/query", StringComparison.OrdinalIgnoreCase)
            && method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            var sql = await ExtractSqlFromBody(context);
            if (sql is null)
                return (null, null);

            if (sql == MalformedBodySentinel)
                return (VibePermissionLevel.Admin, "ERROR");

            var classification = SqlStatementClassifier.Classify(sql);
            if (classification.IsError)
            {
                _logger.LogWarning("EDGE_AUTHZ: SQL classification error: {Error}", classification.ErrorMessage);
                return (VibePermissionLevel.Admin, "ERROR");
            }

            return (classification.RequiredLevel, classification.StatementType);
        }

        if (path.StartsWith("/v1/admin", StringComparison.OrdinalIgnoreCase))
            return (VibePermissionLevel.Admin, "ADMIN_API");

        if (path.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase))
        {
            var level = SqlStatementClassifier.ClassifyHttpRequest(method, path);
            return (level, $"HTTP_{method.ToUpperInvariant()}");
        }

        return (null, null);
    }

    internal static bool IsStatementDenied(string statementType, HashSet<string> deniedStatements)
    {
        if (deniedStatements.Count == 0)
            return false;

        if (deniedStatements.Contains("*"))
            return true;

        if (deniedStatements.Contains(statementType))
            return true;

        var baseType = statementType;
        if (statementType.StartsWith("WITH...", StringComparison.OrdinalIgnoreCase))
            baseType = statementType["WITH...".Length..];
        if (statementType.StartsWith("EXPLAIN ", StringComparison.OrdinalIgnoreCase))
            baseType = statementType["EXPLAIN ".Length..];

        return baseType != statementType && deniedStatements.Contains(baseType);
    }

    private const int MaxBodyBytes = 64 * 1024;
    internal const string MalformedBodySentinel = "__MALFORMED_BODY__";

    private static async Task<string?> ExtractSqlFromBody(HttpContext context)
    {
        try
        {
            context.Request.EnableBuffering();
        }
        catch
        {
        }

        if (context.Request.ContentLength > MaxBodyBytes)
            return MalformedBodySentinel;

        if (context.Request.Body.CanSeek)
            context.Request.Body.Position = 0;

        try
        {
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync(context.RequestAborted);

            if (context.Request.Body.CanSeek)
                context.Request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(body))
                return null;

            if (body.Length > MaxBodyBytes)
                return MalformedBodySentinel;

            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("sql", out var sqlProp))
                return sqlProp.GetString();

            if (doc.RootElement.TryGetProperty("SQL", out var sqlPropUpper))
                return sqlPropUpper.GetString();

            return null;
        }
        catch (JsonException)
        {
            if (context.Request.Body.CanSeek)
                context.Request.Body.Position = 0;
            return MalformedBodySentinel;
        }
        catch
        {
            if (context.Request.Body.CanSeek)
                context.Request.Body.Position = 0;
            return null;
        }
    }

    private static async Task WriteJsonError(HttpContext context, string code, string message)
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            success = false,
            error = new { code, message }
        }));
    }
}
