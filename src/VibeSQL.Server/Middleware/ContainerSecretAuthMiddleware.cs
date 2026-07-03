using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using VibeSQL.Core.Models;

namespace VibeSQL.Server.Middleware;

/// <summary>
/// Shared-secret + JWT Bearer authentication middleware for VibeSQL Server.
///
/// Auth precedence:
/// 1. Authorization: Bearer {jwt} — validated locally against cached IDP JWKS
/// 2. Authorization: Secret {key} — shared secret for internal services
///
/// JWT validation is zero network round-trip (JWKS cached at startup, 24h TTL).
///
/// Public paths (health, swagger) bypass authentication. Optional tier and
/// resolved-client-id headers are passed through to the request pipeline.
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

    public async Task InvokeAsync(HttpContext context, JwksCache jwksCache)
    {
        var path = context.Request.Path.Value ?? "";

        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader))
        {
            _logger.LogWarning("VSQL_AUTH: No Authorization header. Path: {Path}", path);
            await WriteUnauthorizedResponse(context, "AUTH_REQUIRED", "Authorization header required");
            return;
        }

        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            if (LooksLikeJwt(token))
            {
                var principal = await ValidateJwtAsync(token, jwksCache);
                if (principal != null)
                {
                    var claim = principal.FindFirst("user_id")
                        ?? principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
                    if (claim != null && int.TryParse(claim.Value, out var userId))
                    {
                        context.Items["AuthenticatedUserId"] = userId;
                        _logger.LogDebug("VSQL_AUTH_JWT_OK: UserId={UserId}, Path={Path}", userId, path);
                    }
                    else
                    {
                        _logger.LogDebug("VSQL_AUTH_JWT_OK: No user_id claim. Path={Path}", path);
                    }

                    PassTierHeaders(context);
                    await _next(context);
                }
                else
                {
                    _logger.LogWarning("VSQL_AUTH_JWT_FAIL: JWT validation failed. Path: {Path}", path);
                    await WriteUnauthorizedResponse(context, "INVALID_TOKEN",
                        "JWT validation failed — token expired, revoked, or signature mismatch");
                }
                return;
            }

            _logger.LogDebug("VSQL_AUTH_BEARER_NOT_JWT: Bearer token does not look like JWT, falling through. Path: {Path}", path);
        }

        if (authHeader.StartsWith("Secret ", StringComparison.OrdinalIgnoreCase))
        {
            var providedSecret = authHeader["Secret ".Length..];
            if (providedSecret != _expectedSecret)
            {
                _logger.LogWarning("VSQL_AUTH: Invalid container secret. Path: {Path}", path);
                await WriteUnauthorizedResponse(context, "INVALID_SECRET", "Invalid container secret");
                return;
            }

            PassTierHeaders(context);
            _logger.LogDebug("VSQL_AUTH_SECRET_OK: Path: {Path}", path);
            await _next(context);
            return;
        }

        _logger.LogWarning("VSQL_AUTH: Invalid auth scheme. Path: {Path}", path);
        await WriteUnauthorizedResponse(context, "INVALID_SCHEME",
            "Expected Authorization: Bearer {jwt} or Authorization: Secret {key}");
    }

    private static bool LooksLikeJwt(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var parts = token.Split('.');
        if (parts.Length != 3)
            return false;

        return parts.All(p => p.Length > 0 && p.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
    }

    private async Task<ClaimsPrincipal?> ValidateJwtAsync(string token, JwksCache jwksCache)
    {
        try
        {
            var jwks = await jwksCache.GetJwksAsync();
            if (jwks == null)
            {
                _logger.LogWarning("VSQL_AUTH_JWT: JWKS not available");
                return null;
            }

            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
                return null;

            var jwtToken = handler.ReadJwtToken(token);
            var signingKey = jwks.GetSigningKeys().FirstOrDefault(k => k.KeyId == jwtToken.Header.Kid);
            if (signingKey == null)
            {
                _logger.LogWarning("VSQL_AUTH_JWT: No signing key found for kid={Kid}", jwtToken.Header.Kid);
                return null;
            }

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ValidateIssuer = true,
                ValidIssuer = jwtToken.Issuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5),
                RequireSignedTokens = true,
                RequireExpirationTime = true
            };

            return handler.ValidateToken(token, validationParameters, out _);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogDebug("VSQL_AUTH_JWT: Token expired");
            return null;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            _logger.LogDebug("VSQL_AUTH_JWT: Invalid signature");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "VSQL_AUTH_JWT: Validation error");
            return null;
        }
    }

    private static void PassTierHeaders(HttpContext context)
    {
        var clientTier = context.Request.Headers["X-Vibe-Client-Tier"].FirstOrDefault();
        if (!string.IsNullOrEmpty(clientTier))
        {
            context.Items["ClientTier"] = clientTier;
        }

        var resolvedClientId = context.Request.Headers["X-Vibe-Resolved-Client-Id"].FirstOrDefault();
        if (!string.IsNullOrEmpty(resolvedClientId) &&
            int.TryParse(resolvedClientId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var clientId))
        {
            context.Items["ClientId"] = clientId;
        }
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
}
