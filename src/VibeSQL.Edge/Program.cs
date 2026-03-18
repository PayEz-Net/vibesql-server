using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Graylog;
using Serilog.Sinks.Graylog.Core.Transport;
using VibeSQL.Edge.Admin;
using VibeSQL.Edge.Authentication;
using VibeSQL.Edge.Authorization;
using VibeSQL.Edge.Configuration;
using VibeSQL.Edge.Data;
using VibeSQL.Edge.Identity;
using VibeSQL.Edge.Middleware;
using VibeSQL.Edge.Proxy;

var builder = WebApplication.CreateBuilder(args);

// Serilog
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
        Facility = "VibeSQL.Edge"
    })
    .CreateLogger();

builder.Host.UseSerilog();

// Configuration
builder.Services.Configure<VibeEdgeOptions>(
    builder.Configuration.GetSection(VibeEdgeOptions.SectionName));

// EF Core - Edge's own tables in vibe_system schema (Devart dotConnect for PostgreSQL)
builder.Services.AddDbContext<EdgeDbContext>(options =>
    options.UsePostgreSql(builder.Configuration.GetConnectionString("EdgeDb")));

// HttpClient for proxying to VibeSQL.Server
builder.Services.AddHttpClient("VibeServer", client =>
{
    var serverUrl = builder.Configuration["VibeEdge:ServerUrl"] ?? "http://localhost:52411";
    client.BaseAddress = new Uri(serverUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpContextAccessor();

// Provider Registry + Dynamic Scheme Registrar
var providerRegistry = new ProviderRegistry();
builder.Services.AddSingleton<IProviderRegistry>(providerRegistry);
builder.Services.AddSingleton<IDynamicSchemeRegistrar, DynamicSchemeRegistrar>();
builder.Services.AddHostedService<EdgeAuthBackgroundService>();
builder.Services.AddSingleton<IProviderRefreshTrigger>(sp => (EdgeAuthBackgroundService)sp.GetServices<IHostedService>().First(s => s is EdgeAuthBackgroundService));

// Authentication — multi-provider OIDC via PolicyScheme
const string rejectScheme = "EdgeReject";
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = MultiProviderSelector.PolicyScheme;
    options.DefaultChallengeScheme = MultiProviderSelector.PolicyScheme;
})
.AddPolicyScheme(MultiProviderSelector.PolicyScheme, "Multi-Provider OIDC Selector", options =>
{
    options.ForwardDefault = rejectScheme;
    options.ForwardDefaultSelector = context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("VibeSQL.Edge.Authentication");
        return MultiProviderSelector.SelectScheme(context, providerRegistry, logger);
    };
})
.AddScheme<AuthenticationSchemeOptions, RejectAuthHandler>(rejectScheme, _ => { });

builder.Services.AddAuthorization();

// Identity resolution services
builder.Services.AddScoped<IClaimExtractor, ClaimExtractor>();
builder.Services.AddScoped<IUserProvisioningService, UserProvisioningService>();
builder.Services.AddScoped<IFederatedIdentityResolver, FederatedIdentityResolver>();

// Authorization services
builder.Services.AddScoped<IPermissionResolver, PermissionResolver>();

// Proxy services
var hmacSecret = builder.Configuration["VibeEdge:HmacSecret"];
if (string.IsNullOrEmpty(hmacSecret))
    Log.Warning("EDGE_CONFIG: VibeEdge:HmacSecret not configured. HMAC proxy will fail at runtime.");

builder.Services.AddSingleton<IHmacSigner>(_ => new HmacSigner(hmacSecret ?? string.Empty));
builder.Services.AddSingleton<IProxyRequestBuilder, ProxyRequestBuilder>();

// Admin filter
builder.Services.AddScoped<AdminPermissionFilter>();

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VibeSQL Edge API",
        Version = "v1.0.0",
        Description = @"Authentication gateway for VibeSQL Server Edition.

Supports multi-provider OIDC authentication, permission-level enforcement,
federated identity bridging, and HMAC-signed proxy to VibeSQL Server.

## Authentication

External requests use Bearer JWT tokens from any configured OIDC provider.
Admin endpoints require 'admin' permission level.",
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

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT from any configured OIDC provider"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            var origins = builder.Configuration.GetSection("VibeEdge:AllowedOrigins").Get<string[]>();
            if (origins is { Length: > 0 })
            {
                policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader();
            }
            else
            {
                Log.Warning("EDGE_CORS: No AllowedOrigins configured for production. CORS will reject all cross-origin requests. Set VibeEdge:AllowedOrigins in config.");
                policy.SetIsOriginAllowed(_ => false);
            }
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "VibeSQL Edge API v1"));
}

app.UseSerilogRequestLogging();
app.UseCors();

// Middleware pipeline order:
// 1. Authentication — JWT validation via multi-provider PolicyScheme
// 2. IdentityResolution — maps JWT claims ? federated identity ? vibe_user_id
// 3. PermissionEnforcement — resolves role ? permission level, classifies SQL, gates access
// 4. Routing + Authorization — ASP.NET Core endpoint routing and [Authorize] enforcement
app.UseAuthentication();
app.UseMiddleware<IdentityResolutionMiddleware>();
app.UseMiddleware<PermissionEnforcementMiddleware>();
app.UseRouting();
app.UseAuthorization();
app.UseMiddleware<AuditMiddleware>();
app.MapControllers();

Log.Information("VibeSQL Edge starting on {Environment}", app.Environment.EnvironmentName);

app.Run();

public partial class Program { }
