using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
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

// Dedicated constraint-violation log (Postgres-style flatfile by default).
// Toggled by Logging:VibeSQL:ConstraintLog:Enabled in appsettings.
// The executor emits events with scope property VibeEventType=CONSTRAINT_VIOLATION;
// this sub-logger filter routes only those events to the file, leaving the main
// Console/Graylog pipeline untouched. Adding syslog/ES/etc. later = one more
// WriteTo.* line inside the same sub-logger block.
var constraintLogEnabled = builder.Configuration.GetValue<bool>("Logging:VibeSQL:ConstraintLog:Enabled", true);
var constraintLogPath = builder.Configuration["Logging:VibeSQL:ConstraintLog:FilePath"]
    ?? "logs/vibesql-constraints.log";
var constraintLogRetention = builder.Configuration.GetValue<int>("Logging:VibeSQL:ConstraintLog:RetainedFileCountLimit", 14);
var constraintRollingIntervalStr = builder.Configuration["Logging:VibeSQL:ConstraintLog:RollingInterval"] ?? "Day";
var constraintRollingInterval = Enum.TryParse<Serilog.RollingInterval>(constraintRollingIntervalStr, ignoreCase: true, out var parsedInterval)
    ? parsedInterval
    : Serilog.RollingInterval.Day;

// Postgres-style multi-line output template. One event renders as:
//   2026-04-13 22:47:01.234 UTC [tid] ERROR:  duplicate key value violates unique constraint "idx_users_email_unique"
//           DETAIL:  Key (email)=(foo@example.com) already exists.
//           STATEMENT:  INSERT INTO vibe.documents (...) VALUES (...);
// The tab-indented continuation matches PostgreSQL's own log_line_prefix feel.
const string constraintOutputTemplate =
    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{ThreadId}] {Level:u5}:  {Message:lj}{NewLine}" +
    "\tDETAIL:  {Detail}{NewLine}" +
    "\tSTATEMENT:  {Statement}{NewLine}";

var loggerConfig = new LoggerConfiguration()
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
    });

if (constraintLogEnabled)
{
    loggerConfig = loggerConfig.WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(Matching.WithProperty<string>(
            "VibeEventType",
            v => string.Equals(v, "CONSTRAINT_VIOLATION", StringComparison.Ordinal)))
        .WriteTo.File(
            path: constraintLogPath,
            outputTemplate: constraintOutputTemplate,
            rollingInterval: constraintRollingInterval,
            retainedFileCountLimit: constraintLogRetention,
            shared: true));
}

Log.Logger = loggerConfig.CreateLogger();

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
