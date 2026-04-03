using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Graylog;
using Serilog.Sinks.Graylog.Core.Transport;
using VibeSQL.Core;
using VibeSQL.Core.Models;
using VibeSQL.Core.Options;
using VibeSQL.Core.Query;
using VibeSQL.Server.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// Serilog Logging
// ========================================
var graylogHost = builder.Configuration["Logging:Graylog:HostnameOrAddress"];
var graylogPort = builder.Configuration["Logging:Graylog:Port"];

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .WriteTo.Graylog(new GraylogSinkOptions
    {
        HostnameOrAddress = graylogHost ?? "localhost",
        Port = int.TryParse(graylogPort, out var port) ? port : 12201,
        TransportType = TransportType.Udp,
        Facility = "VibeSQL.Server"
    })
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddHttpContextAccessor();

// ========================================
// Container Secret Authentication
// VibeSQL Server is an internal service. HMAC auth is handled
// by Vibe.Edge at the DMZ layer. This server uses a simple
// shared secret for service-to-service auth.
// ========================================
var containerSecret = builder.Configuration["VibeSQL:ContainerSecret"]
    ?? Environment.GetEnvironmentVariable("VIBESQL_CONTAINER_SECRET");

if (string.IsNullOrWhiteSpace(containerSecret))
{
    throw new InvalidOperationException(
        "Container secret not configured. Set VibeSQL:ContainerSecret in appsettings " +
        "or VIBESQL_CONTAINER_SECRET environment variable.");
}

var secretConfig = new VibeContainerSecretConfig { Secret = containerSecret };
builder.Services.AddSingleton(secretConfig);
Log.Information("VIBESQL_STARTUP: Container secret auth configured");

// ========================================
// VibeSQL Core Query Services
// ========================================
builder.Services.AddSingleton<IQueryValidator, QueryValidator>();
builder.Services.AddSingleton<IQuerySafetyChecker, QuerySafetyChecker>();
builder.Services.AddSingleton<IQueryLimiter, QueryLimiter>();
builder.Services.AddScoped<IQueryExecutor, QueryExecutor>();

// ========================================
// VibeSQL Schema Sentinel (VS-SS)
// ========================================
builder.Services.AddVibeSentinelServices(builder.Configuration);
Log.Information("VIBESQL_STARTUP: Schema Sentinel enabled");

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VibeSQL Server API",
        Version = "v2.0.0",
        Description = @"Production-ready PostgreSQL query server with HTTP API.

Features:
- Raw SQL execution with validation and safety checks
- Container secret authentication for internal deployments
- Tier-based rate limiting and timeouts
- JSONB support for flexible schemas
- Built-in query limits and security
- **Schema Sentinel (VS-SS)**: Automatic protection against destructive schema changes

## Authentication

All endpoints (except /health) require container secret authentication:

- **Authorization**: `Secret {your-container-secret}`

Optional: **X-Vibe-Client-Tier** sets the tier for timeout/rate limits (Free, Starter, Pro, Enterprise).

HMAC authentication for external clients is handled by Vibe.Edge at the DMZ layer.

## Schema Sentinel (VS-SS)

Schema changes are automatically classified using the Sentinel Taxonomy:
- **S-100** (Safe): Add tables, nullable columns, indexes — auto-applied
- **M-200** (Migration): Add non-null columns with defaults — auto-applied with DDL
- **D-300** (Destructive): Drop tables/columns, narrow types — blocked, can override
- **P-400** (Prohibited): Drop entire schema, major regressions — blocked, never allowed

To override a Destructive (409) change, include header:
- **X-Vibe-Force-Schema-Update**: `true`

Prohibited (422) changes cannot be overridden.",
        Contact = new OpenApiContact
        {
            Name = "VibeSQL",
            Url = new Uri("https://github.com/vibesql/vibesql-server")
        },
        License = new OpenApiLicense
        {
            Name = "Apache 2.0",
            Url = new Uri("https://www.apache.org/licenses/LICENSE-2.0")
        }
    });

    // Container secret security scheme
    c.AddSecurityDefinition("Authorization", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Container secret via `Secret {your-key}`"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Authorization"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "VibeSQL Server API v2"));
}

app.UseSerilogRequestLogging();
app.UseCors();

// Container secret auth — internal service-to-service only
app.UseMiddleware<ContainerSecretAuthMiddleware>();
Log.Information("VibeSQL Server using container secret authentication");

app.MapControllers();

Log.Information("VibeSQL Server starting on {Environment}", app.Environment.EnvironmentName);

app.Run();
