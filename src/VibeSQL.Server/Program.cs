using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Graylog;
using Serilog.Sinks.Graylog.Core.Transport;
using VibeSQL.Core.Models;
using VibeSQL.Core.Query;
using VibeSQL.Server.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// Serilog Logging
// ========================================
var graylogHost = builder.Configuration["Logging:Graylog:HostnameOrAddress"];
var graylogPort = builder.Configuration["Logging:Graylog:Port"];

// Card 394479. *** DO NOT RESTORE `graylogHost ?? "localhost"`. *** That fallback was STRICTLY
// WEAKER than no check at all. `??` only catches NULL, so an EMPTY host went to the sink
// verbatim, and a NULL host aimed a UDP sink at localhost:12201 - a port on this box with
// nothing listening. UDP has no delivery failure to observe, so a misconfigured sink and a
// healthy one look EXACTLY THE SAME from here while every log line is silently discarded.
//
// The scenario this card was filed about is a DROPPED OR MISTYPED ENV VAR KEY. The AKS
// deployment sets Logging__Graylog__HostnameOrAddress literally; if that key is lost, config
// falls through to appsettings.json. So the base appsettings.json host is now "" - an
// explicitly empty host means "no sink, deliberately", which takes the branch below, prints a
// WARNING, and registers nothing. A dropped env var is now LOUD instead of silent.
//
// appsettings.Development.json carried "localhost" too and is EMPTIED for the same reason. It
// OUTRANKS this base file, so fixing only the base file would have been defeated by it on any
// box running as Development - which is every container on the ROSA/93 dev rig.
//
// A validator (is this a usable IP/DNS name?) is deliberately NOT duplicated here. That rule
// lives in PayEz.Application.Logging.GraylogHostValidator in the PayEz-Core repository, and
// this is a separate repository with no reference to it - a rule COPIED is two rules that
// disagree by next month. The fail-open default is the defect this card names; refusing
// malformed hosts is a separate improvement and belongs with the shared validator, not a fork.
var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .WriteTo.Console();

if (string.IsNullOrWhiteSpace(graylogHost))
{
    Console.WriteLine(
        "WARNING: Logging:Graylog:HostnameOrAddress is not set. The Graylog sink will NOT be " +
        "registered and VibeSQL.Server will log to the console only. This is deliberate: the " +
        "previous behaviour silently defaulted to localhost:12201 and discarded every log line. " +
        "If you expected central logging, the setting that WINS is the " +
        "Logging__Graylog__HostnameOrAddress environment variable (highest precedence last: " +
        "appsettings.json < appsettings.{Environment}.json < environment variable), which is " +
        "what the AKS deployment sets - editing appsettings.json there is INERT.");
}
else
{
    loggerConfiguration.WriteTo.Graylog(new GraylogSinkOptions
    {
        HostnameOrAddress = graylogHost,
        Port = int.TryParse(graylogPort, out var port) ? port : 12201,
        TransportType = TransportType.Udp,
        Facility = "VibeSQL.Server"
    });
}

Log.Logger = loggerConfiguration.CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// ========================================
// Container Secret Authentication
// VibeSQL Server is an internal service. HMAC auth is handled
// by Vibe.Edge at the DMZ layer. This server uses a simple
// shared secret for service-to-service auth, with optional
// JWT Bearer validation against cached IDP JWKS.
// ========================================
var containerSecret = builder.Configuration["VibeSQL:ContainerSecret"]
    ?? Environment.GetEnvironmentVariable("VIBESQL_CONTAINER_SECRET")
    ?? throw new InvalidOperationException(
        "Container secret not configured. Set VibeSQL:ContainerSecret in appsettings " +
        "or VIBESQL_CONTAINER_SECRET environment variable.");

var secretConfig = new VibeContainerSecretConfig { Secret = containerSecret };
builder.Services.AddSingleton(secretConfig);
builder.Services.AddHostedService<JwksCache>();
builder.Services.AddSingleton(sp => sp.GetServices<IHostedService>().OfType<JwksCache>().First());
Log.Information("VIBESQL_STARTUP: Container secret auth and JWKS cache configured");

// ========================================
// VibeSQL Core Query Services
// ========================================
builder.Services.AddSingleton<IQueryValidator, QueryValidator>();
builder.Services.AddSingleton<IQuerySafetyChecker, QuerySafetyChecker>();
builder.Services.AddSingleton<IQueryLimiter, QueryLimiter>();
builder.Services.AddScoped<IQueryExecutor, QueryExecutor>();
builder.Services.AddScoped<IClientIdResolver, ClientIdResolver>();

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VibeSQL Server API",
        Version = "v2.0.0",
        Description = @"Production-ready PostgreSQL query server with HTTP API.

Features:
- Raw SQL execution with validation, safety checks, and tenant isolation (RLS)
- Container secret authentication for internal deployments
- Optional JWT Bearer validation against cached IDP JWKS
- Tier-based rate limiting and timeouts
- JSONB support for flexible schemas
- Built-in query limits and security

## Authentication

All endpoints (except /health) require one of:

- **Authorization**: `Secret {your-container-secret}`
- **Authorization**: `Bearer {jwt}` — validated locally against cached JWKS

Optional: **X-Vibe-Client-Tier** sets the tier for timeout/rate limits (Free, Starter, Pro, Enterprise).
Optional: **X-Vibe-Resolved-Client-Id** forwards the numeric tenant id for RLS-scoped queries.

HMAC authentication for external clients is handled by Vibe.Edge at the DMZ layer.",
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
        Description = "Container secret via `Secret {your-key}` or JWT via `Bearer {jwt}`"
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

// Container secret + JWT auth middleware
app.UseMiddleware<ContainerSecretAuthMiddleware>();
Log.Information("VibeSQL Server using container secret + JWT authentication");

app.MapHealthChecks("/health");
app.MapControllers();

Log.Information("VibeSQL Server starting on {Environment}", app.Environment.EnvironmentName);

app.Run();
