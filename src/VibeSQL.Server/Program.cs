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
    var vaultUri = builder.Configuration["AzureKeyVault:VaultUri"];
    var useKeyVault = builder.Configuration.GetValue<bool>("AzureKeyVault:UseKeyVault");

    if (useKeyVault && !string.IsNullOrWhiteSpace(secretName) && !string.IsNullOrWhiteSpace(vaultUri))
    {
        // ONE SECRET, BY NAME. Deliberately a targeted SecretClient read and NOT
        // builder.Configuration.AddAzureKeyVault(...).
        //
        // I shipped the config-provider version to production on 2026-08-08 and it
        // took mail down for two minutes. AddAzureKeyVault APPENDS the vault to the
        // configuration chain, and in ASP.NET Core the LAST source wins — so every
        // secret in the vault silently outranked the environment. The vault holds a
        // stale ConnectionStrings--VibeDb, which then overrode the connection string
        // the manifest supplies from a k8s secret, and the pod dialled 127.0.0.1:5432
        // and got connection refused on every query.
        //
        // The lesson worth keeping: adding a configuration SOURCE changes PRECEDENCE
        // for everything, not just the key you came for. Fetching one named secret
        // cannot shadow anything.
        //
        // On AKS the credential is workload identity — the deployment sets
        // AZURE_CLIENT_ID / AZURE_TENANT_ID / AZURE_FEDERATED_TOKEN_FILE and binds a
        // service account, and DefaultAzureCredential picks the federated token up
        // with no code here. The pod reads the vault under its own authority; the
        // secret is never copied into container config, so rotation stays a single
        // write in one place.
        try
        {
            var kv = new Azure.Security.KeyVault.Secrets.SecretClient(
                new Uri(vaultUri), new Azure.Identity.DefaultAzureCredential());
            containerSecret = kv.GetSecret(secretName).Value.Value;
            Log.Information(
                "VIBESQL_STARTUP: container secret read from Key Vault {VaultUri} as {SecretName}",
                vaultUri, secretName);
        }
        catch (Exception ex)
        {
            // Fail loud and name the real cause. Falling through would hit the
            // empty-secret check below and report "container secret not configured",
            // which sends whoever debugs it to the manifest rather than to the
            // identity binding or vault access policy that actually failed.
            throw new InvalidOperationException(
                $"Could not read secret '{secretName}' from {vaultUri}. The pod's " +
                "workload identity is what authorises this read — check the service " +
                "account binding and the vault access policy for client " +
                $"{Environment.GetEnvironmentVariable("AZURE_CLIENT_ID")}.", ex);
        }
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
