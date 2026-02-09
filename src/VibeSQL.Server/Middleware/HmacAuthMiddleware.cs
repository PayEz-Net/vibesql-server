using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VibeSQL.Core.Models;

namespace VibeSQL.Server.Middleware;

/// <summary>
/// HMAC authentication middleware for service-to-service calls.
/// Uses a shared secret (loaded from key vault or config) for signature verification.
///
/// Required headers:
/// - X-Vibe-Timestamp: Unix epoch timestamp
/// - X-Vibe-Signature: HMAC-SHA256 signature of "{timestamp}|{method}|{path}"
/// - X-Vibe-Service: Service identifier (for logging)
///
/// Optional headers:
/// - X-Vibe-Client-Tier: Client tier for timeout configuration
/// </summary>
public class HmacAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HmacAuthMiddleware> _logger;
    private readonly string _hmacSecret;
    private readonly bool _devBypass;

    private const string TIMESTAMP_HEADER = "X-Vibe-Timestamp";
    private const string SIGNATURE_HEADER = "X-Vibe-Signature";
    private const string SERVICE_HEADER = "X-Vibe-Service";
    private const string CLIENT_TIER_HEADER = "X-Vibe-Client-Tier";

    private static readonly TimeSpan MaxTimestampAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(1);

    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/v1/health",
        "/swagger",
        "/swagger/index.html",
        "/swagger/v1/swagger.json"
    };

    public HmacAuthMiddleware(
        RequestDelegate next,
        ILogger<HmacAuthMiddleware> logger,
        IHostEnvironment environment,
        IConfiguration configuration,
        VibeSecretConfiguration secretConfig)
    {
        _next = next;
        _logger = logger;
        _hmacSecret = secretConfig.HmacSecret;
        _devBypass = environment.IsDevelopment()
            && configuration.GetValue<bool>("VibeSQL:DevBypassHmac", false);

        if (_devBypass)
        {
            logger.LogWarning("VIBESQL_AUTH: Development HMAC bypass is ENABLED - do NOT use in production");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        // Development bypass - skip HMAC validation for Swagger/local testing
        if (_devBypass)
        {
            var devTier = context.Request.Headers[CLIENT_TIER_HEADER].FirstOrDefault();
            if (!string.IsNullOrEmpty(devTier))
            {
                context.Items["ClientTier"] = devTier;
            }

            _logger.LogDebug("VIBESQL_AUTH: Dev bypass - skipping HMAC for {Path}", path);
            await _next(context);
            return;
        }

        var timestampStr = context.Request.Headers[TIMESTAMP_HEADER].FirstOrDefault();
        var signature = context.Request.Headers[SIGNATURE_HEADER].FirstOrDefault();
        var service = context.Request.Headers[SERVICE_HEADER].FirstOrDefault() ?? "unknown";

        if (string.IsNullOrEmpty(timestampStr) || string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("VIBESQL_AUTH: Missing HMAC headers. Service: {Service}, Path: {Path}", service, path);
            await WriteUnauthorizedResponse(context, "HMAC_REQUIRED",
                "Endpoints require X-Vibe-Timestamp and X-Vibe-Signature headers");
            return;
        }

        if (!long.TryParse(timestampStr, out var timestamp))
        {
            _logger.LogWarning("VIBESQL_AUTH: Invalid timestamp format. Service: {Service}", service);
            await WriteUnauthorizedResponse(context, "INVALID_TIMESTAMP", "X-Vibe-Timestamp must be a valid Unix epoch");
            return;
        }

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        var now = DateTimeOffset.UtcNow;

        if (requestTime < now - MaxTimestampAge)
        {
            _logger.LogWarning("VIBESQL_AUTH: Timestamp too old. Service: {Service}, RequestTime: {RequestTime}, Now: {Now}",
                service, requestTime, now);
            await WriteUnauthorizedResponse(context, "TIMESTAMP_EXPIRED",
                "Request timestamp too old (replay protection)");
            return;
        }

        if (requestTime > now + MaxClockSkew)
        {
            _logger.LogWarning("VIBESQL_AUTH: Timestamp in future. Service: {Service}, RequestTime: {RequestTime}", service, requestTime);
            await WriteUnauthorizedResponse(context, "TIMESTAMP_FUTURE", "Request timestamp too far in future");
            return;
        }

        var method = context.Request.Method.ToUpperInvariant();
        var stringToSign = $"{timestamp}|{method}|{path}";
        var expectedSignature = ComputeHmacSignature(stringToSign, _hmacSecret);

        if (!ConstantTimeEquals(signature, expectedSignature))
        {
            _logger.LogWarning("VIBESQL_AUTH: Signature mismatch. Service: {Service}, Path: {Path}", service, path);
            await WriteUnauthorizedResponse(context, "SIGNATURE_MISMATCH", "Request signature verification failed");
            return;
        }

        var clientTier = context.Request.Headers[CLIENT_TIER_HEADER].FirstOrDefault();
        if (!string.IsNullOrEmpty(clientTier))
        {
            context.Items["ClientTier"] = clientTier;
        }

        _logger.LogDebug("VIBESQL_AUTH: Authenticated. Service: {Service}, Path: {Path}, Tier: {Tier}",
            service, path, clientTier ?? "default");

        await _next(context);
    }

    private static bool IsPublicPath(string path)
    {
        foreach (var publicPath in PublicPaths)
        {
            if (path.StartsWith(publicPath, StringComparison.OrdinalIgnoreCase))
                return true;
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

    private static string ComputeHmacSignature(string data, string key)
    {
        var keyBytes = Convert.FromBase64String(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToBase64String(hash);
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }
}
