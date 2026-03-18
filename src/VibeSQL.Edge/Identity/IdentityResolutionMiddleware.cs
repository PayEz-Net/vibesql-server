using System.Text.Json;
using VibeSQL.Edge;
using VibeSQL.Edge.Authentication;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Identity;

public sealed class IdentityResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdentityResolutionMiddleware> _logger;

    public IdentityResolutionMiddleware(RequestDelegate next, ILogger<IdentityResolutionMiddleware> logger)
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
        if (providerKey is null)
        {
            _logger.LogWarning("EDGE_IDENTITY: Authenticated request but no EdgeProviderKey in context");
            await _next(context);
            return;
        }

        var registry = context.RequestServices.GetRequiredService<IProviderRegistry>();
        var providerRecord = registry.GetByKey(providerKey);

        if (providerRecord is null)
        {
            _logger.LogError("EDGE_IDENTITY: Provider {ProviderKey} not found in registry", providerKey);
            context.Response.StatusCode = 500;
            await WriteJsonError(context, "PROVIDER_NOT_FOUND", "Authenticated provider configuration missing");
            return;
        }

        var provider = new OidcProvider
        {
            ProviderKey = providerRecord.ProviderKey,
            SubjectClaimPath = providerRecord.SubjectClaimPath,
            RoleClaimPath = providerRecord.RoleClaimPath,
            EmailClaimPath = providerRecord.EmailClaimPath,
            AutoProvision = providerRecord.AutoProvision,
            ProvisionDefaultRole = providerRecord.ProvisionDefaultRole
        };

        var extractor = context.RequestServices.GetRequiredService<IClaimExtractor>();
        ExtractedClaims claims;
        try
        {
            claims = extractor.Extract(context.User, provider);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "EDGE_IDENTITY: Claim extraction failed for provider {ProviderKey}", providerKey);
            context.Response.StatusCode = 401;
            await WriteJsonError(context, "CLAIM_EXTRACTION_FAILED", ex.Message);
            return;
        }

        var resolver = context.RequestServices.GetRequiredService<IFederatedIdentityResolver>();
        var resolved = await resolver.ResolveAsync(providerKey, claims, provider, context.RequestAborted);

        if (resolved is null)
        {
            context.Response.StatusCode = 403;
            await WriteJsonError(context, "IDENTITY_NOT_RESOLVED",
                "User identity could not be resolved. Contact your administrator.");
            return;
        }

        context.Items[EdgeContextKeys.UserId] = resolved.VibeUserId;
        context.Items[EdgeContextKeys.ProviderKey] = resolved.ProviderKey;
        context.Items[EdgeContextKeys.ExternalSubject] = resolved.ExternalSubject;
        context.Items[EdgeContextKeys.ExtractedRoles] = claims.Roles;

        var audClaim = context.User.FindFirst("aud")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Uri)?.Value;
        if (audClaim is not null)
            context.Items[EdgeContextKeys.ClientId] = audClaim;

        await _next(context);
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
