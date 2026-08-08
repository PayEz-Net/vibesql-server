using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Graylog;
using Serilog.Sinks.Graylog.Core.Transport;
using VibeSQL.Core.Models;
using VibeSQL.Core;
using VibeSQL.Core.Query;
using VibeSQL.Server.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// Azure Key Vault (optional — off unless configured)
// ========================================
// The deployed service fetches its own secrets AT STARTUP under its OWN identity.
// They are deliberately NOT copied into container config or a k8s secret: the vault
// holds one copy, every service reads it, and rotation is a single write. Duplicating
// a secret into a manifest forks it and silently breaks rotation — the copy keeps
// working after the original is rotated, which is worse than failing.
//
// On AKS the credential comes from workload identity: the deployment sets
// AZURE_CLIENT_ID / AZURE_TENANT_ID / AZURE_FEDERATED_TOKEN_FILE and binds a service
// account, and DefaultAzureCredential picks the federated token up with no code.
// The pod has authority to read the vault; nothing else needs it.
//
// ENTIRELY OPTIONAL. With AzureKeyVault:UseKeyVault unset or false this block does
// nothing and the server runs on plain configuration — which is how it runs on 93 and
// how any non-Azure deployment runs it. That conditionality is why this can live in
// the OSS product without making it Azure-only.
//
// Secrets land as ordinary configuration keys, so KV secret "ContainersKey" is read
// as Configuration["ContainersKey"] below.
var useKeyVault = builder.Configuration.GetValue<bool>("AzureKeyVault:UseKeyVault");
var vaultUri = builder.Configuration["AzureKeyVault:VaultUri"];
if (useKeyVault && !string.IsNullOrWhiteSpace(vaultUri))
{
    // FAIL LOUD. If the vault is configured but unreachable — wrong identity, missing
    // federated token, network policy — starting anyway would produce a service with
    // no container secret, and the secret check below would then report "not
    // configured", sending whoever debugs it to the manifest instead of to the
    // identity binding that actually failed.
    builder.Configuration.AddAzureKeyVault(
        new Uri(vaultUri),
        new Azure.Identity.DefaultAzureCredential());
    Console.WriteLine($"VIBESQL_STARTUP: Key Vault configured ({vaultUri})");
}

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
builder.Services.AddHttpClient();

// ========================================
// Container Secret Authentication
// VibeSQL Server is an internal service. HMAC auth is handled
// by Vibe.Edge at the DMZ layer. This server uses a simple
// shared secret for service-to-service auth, with optional
// JWT Bearer validation against cached IDP JWKS.
// ========================================
// EMPTY IS UNSET. `??` only falls through on null, and both appsettings.json and
// appsettings.Development.json ship "ContainerSecret": "" — a non-null empty string.
// The consequence was that the env-var fallback was UNREACHABLE and the throw never
// fired: the service booted with an empty secret and logged "Container secret auth
// configured". It is not an auth bypass — ASP.NET trims the header value, so
// "Authorization: Secret " arrives as "Secret" and never matches the "Secret "
// prefix — it is an auth DEADLOCK: nothing can authenticate via the Secret scheme,
// and the service reports itself healthy. Measured 2026-08-08 against a local F5 run.
// Three sources, in order of specificity. All three treat empty as absent.
//   1. VIBESQL_CONTAINER_SECRET  — env, for local/dev and non-Azure hosts
//   2. VibeSQL:ContainerSecret   — plain configuration
//   3. VibeSQL:ContainerSecretName -> the secret of that NAME in Key Vault
// (3) is how the deployed service gets it: the manifest names "ContainersKey" and the
// pod reads it under its own workload identity. The value is never written into
// container config, so rotation happens once in the vault and every service picks it
// up on next start.
var containerSecret = Environment.GetEnvironmentVariable("VIBESQL_CONTAINER_SECRET") is { Length: > 0 } envSecret
    ? envSecret
    : builder.Configuration["VibeSQL:ContainerSecret"];

if (string.IsNullOrWhiteSpace(containerSecret))
{
    var secretName = builder.Configuration["VibeSQL:ContainerSecretName"];
    if (!string.IsNullOrWhiteSpace(secretName))
    {
        // Key Vault secrets are flattened into configuration by the provider above,
        // so this is a plain lookup — not a second vault round-trip.
        containerSecret = builder.Configuration[secretName];
        if (string.IsNullOrWhiteSpace(containerSecret))
        {
            throw new InvalidOperationException(
                $"VibeSQL:ContainerSecretName is '{secretName}' but no such secret was " +
                "loaded. The Key Vault provider either is not enabled " +
                "(AzureKeyVault:UseKeyVault) or the pod's identity cannot read that " +
                "secret. Failing here rather than reporting 'secret not configured', " +
                "which would point at the manifest instead of the identity binding.");
        }
        Log.Information("VIBESQL_STARTUP: container secret loaded from Key Vault as {SecretName}", secretName);
    }
}

if (string.IsNullOrWhiteSpace(containerSecret))
{
    throw new InvalidOperationException(
        "Container secret not configured. Set VibeSQL:ContainerSecret in appsettings " +
        "or VIBESQL_CONTAINER_SECRET environment variable. An empty or whitespace value " +
        "is treated as unset: it would start a service on which Secret-scheme auth can " +
        "never succeed.");
}

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

// VibeSQL Schema Sentinel (VS-SS) — recovered from origin/npgsql-migration 2026-07-30.
// Classifies schema changes and BLOCKS destructive ones (409, overridable via
// X-Vibe-Force-Schema-Update) and Prohibited ones (422, never overridable).
builder.Services.AddVibeSentinelServices(builder.Configuration);
Log.Information("VIBESQL_STARTUP: Schema Sentinel enabled");
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
