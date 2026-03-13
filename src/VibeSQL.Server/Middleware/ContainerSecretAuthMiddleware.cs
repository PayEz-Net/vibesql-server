using System.Net;
using System.Text.Json;
using VibeSQL.Core.Models;

namespace VibeSQL.Server.Middleware;

/// <summary>
/// Shared-secret authentication middleware for internal service-to-service calls.
/// Validates Authorization: Secret {key} header.
///
/// This is the standard internal auth pattern for k8s/container deployments.
/// HMAC is reserved for edge/DMZ services — internal services behind the firewall
/// use simple shared-secret validation.
/// </summary>
public class ContainerSecretAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ContainerSecretAuthMiddleware> _logger;
    private readonly string _expectedSecret;

    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/v1/health",
        "/swagger",
        "/swagger/index.html",
        "/swagger/v1/swagger.json"
    };

    public ContainerSecretAuthMiddleware(
        RequestDelegate next,
        ILogger<ContainerSecretAuthMiddleware> logger,
        VibeContainerSecretConfig secretConfig)
    {
        _next = next;
        _logger = logger;
        _expectedSecret = secretConfig.Secret;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip auth for public paths (health, swagger)
        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader))
        {
            _logger.LogWarning("VSQL_AUTH: No Authorization header. Path: {Path}", path);
            await WriteUnauthorizedResponse(context, "AUTH_REQUIRED",
                "Authorization header required");
            return;
        }

        if (authHeader.StartsWith("Secret "))
        {
            var providedSecret = authHeader["Secret ".Length..];
            if (providedSecret != _expectedSecret)
            {
                _logger.LogWarning("VSQL_AUTH: Invalid container secret. Path: {Path}", path);
                await WriteUnauthorizedResponse(context, "INVALID_SECRET",
                    "Invalid container secret");
                return;
            }
        }
        else
        {
            _logger.LogWarning("VSQL_AUTH: Invalid auth scheme. Path: {Path}", path);
            await WriteUnauthorizedResponse(context, "INVALID_SCHEME",
                "Expected Authorization: Secret {key}");
            return;
        }

        // Pass through optional tier headers
        var clientTier = context.Request.Headers["X-Vibe-Client-Tier"].FirstOrDefault();
        if (!string.IsNullOrEmpty(clientTier))
        {
            context.Items["ClientTier"] = clientTier;
        }

        _logger.LogDebug("VSQL_AUTH_SUCCESS: Path: {Path}, Tier: {Tier}",
            path, clientTier ?? "default");

        await _next(context);
    }

    private static bool IsPublicPath(string path)
    {
        foreach (var publicPath in PublicPaths)
        {
            if (path.StartsWith(publicPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static async Task WriteUnauthorizedResponse(HttpContext context, string code, string message)
    {
        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            success = false,
            error = new { code, message }
        }));
    }
}
