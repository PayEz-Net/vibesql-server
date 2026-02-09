using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace VibeSQL.Server.Swagger;

/// <summary>
/// Swagger operation filter that adds HMAC authentication headers to all protected endpoints.
/// Documents the X-Vibe-Timestamp, X-Vibe-Signature, and X-Vibe-Service headers.
/// </summary>
public class HmacAuthOperationFilter : IOperationFilter
{
    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/v1/health",
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Skip health endpoints - they're public
        var relativePath = context.ApiDescription.RelativePath;
        if (relativePath != null && PublicPaths.Any(p => relativePath.StartsWith(p.TrimStart('/'), StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "X-Vibe-Signature"
                    }
                },
                Array.Empty<string>()
            },
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "X-Vibe-Timestamp"
                    }
                },
                Array.Empty<string>()
            },
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "X-Vibe-Service"
                    }
                },
                Array.Empty<string>()
            }
        });
    }
}
