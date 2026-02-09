using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Graylog;
using Serilog.Sinks.Graylog.Core.Transport;
using VibeSQL.Core.Models;
using VibeSQL.Core.Query;
using VibeSQL.Server.Middleware;
using VibeSQL.Server.Swagger;

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
// Auth Secret Configuration
// Loads HMAC secret from config or environment variable.
// Key vault integration (CryptAply) will provide key governance
// and compliance - see IKeyVaultService for the integration point.
// ========================================
var secretConfiguration = new VibeSecretConfiguration
{
    HmacSecretName = builder.Configuration["VibeSQL:HmacSecretName"] ?? "VibeHmacSecret",
    HmacSecret = builder.Configuration["VibeSQL:HmacSecret"]
        ?? Environment.GetEnvironmentVariable("VIBESQL_HMAC_SECRET")
        ?? throw new InvalidOperationException(
            "HMAC secret not configured. Set VibeSQL:HmacSecret in appsettings " +
            "or VIBESQL_HMAC_SECRET environment variable.")
};
Log.Information("VIBESQL_STARTUP: Loaded HMAC secret from configuration");

builder.Services.AddSingleton(secretConfiguration);

// ========================================
// VibeSQL Core Query Services
// ========================================
builder.Services.AddSingleton<IQueryValidator, QueryValidator>();
builder.Services.AddSingleton<IQuerySafetyChecker, QuerySafetyChecker>();
builder.Services.AddSingleton<IQueryLimiter, QueryLimiter>();
builder.Services.AddScoped<IQueryExecutor, QueryExecutor>();

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
- KV/secret managed HMAC authentication
- Tier-based rate limiting and timeouts
- JSONB support for flexible schemas
- Built-in query limits and security

## Authentication

All endpoints (except /health) require HMAC authentication via three headers:

- **X-Vibe-Timestamp**: Unix epoch seconds (must be within 5 minutes)
- **X-Vibe-Signature**: HMAC-SHA256 of `{timestamp}|{METHOD}|{path}` using the shared secret
- **X-Vibe-Service**: Identifier of the calling service (for logging)

Optional: **X-Vibe-Client-Tier** sets the tier for timeout/rate limits (Free, Starter, Pro, Enterprise).

In development, set `VibeSQL:DevBypassHmac=true` to skip HMAC validation for local testing.",
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

    // HMAC security scheme definitions
    c.AddSecurityDefinition("X-Vibe-Signature", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Vibe-Signature",
        Description = "HMAC-SHA256 signature of \"{timestamp}|{METHOD}|{path}\" using the shared secret (base64)"
    });

    c.AddSecurityDefinition("X-Vibe-Timestamp", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Vibe-Timestamp",
        Description = "Unix epoch timestamp (seconds). Must be within 5 minutes of server time."
    });

    c.AddSecurityDefinition("X-Vibe-Service", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Vibe-Service",
        Description = "Calling service identifier (e.g. 'vibe-app', 'admin-portal')"
    });

    c.OperationFilter<HmacAuthOperationFilter>();
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

// HMAC authentication for service-to-service calls
app.UseMiddleware<HmacAuthMiddleware>();

app.MapControllers();

Log.Information("VibeSQL Server starting on {Environment}", app.Environment.EnvironmentName);

app.Run();
