using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Graylog;
using Serilog.Sinks.Graylog.Core.Transport;
using VibeSQL.Core.Models;
using VibeSQL.Core.Query;
using VibeSQL.Core.Services;
using VibeSQL.Server.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// Optional: Azure App Configuration + Key Vault
// Set VibeSQL:UseKeyVault=true to enable
// ========================================
var useKeyVault = builder.Configuration.GetValue<bool>("VibeSQL:UseKeyVault", false);

if (useKeyVault)
{
    // Uncomment and configure for your key vault provider:
    // var azureAppConfigConnection = builder.Configuration["AzureAppConfig:ConnectionString"];
    // if (!string.IsNullOrEmpty(azureAppConfigConnection))
    // {
    //     builder.Configuration.AddAzureAppConfiguration(options =>
    //     {
    //         options.Connect(azureAppConfigConnection)
    //             .ConfigureKeyVault(kv =>
    //             {
    //                 kv.SetCredential(new Azure.Identity.DefaultAzureCredential());
    //             });
    //     });
    // }
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

// ========================================
// Auth Secret Configuration
// Loads HMAC secret from key vault or config/environment
// ========================================
VibeSecretConfiguration secretConfiguration;

if (useKeyVault)
{
    builder.Services.AddSingleton<IKeyVaultService, AzureKeyVaultService>();
    var keyVaultService = builder.Services.BuildServiceProvider().GetRequiredService<IKeyVaultService>();

    var hmacSecretName = builder.Configuration["VibeSQL:HmacSecretName"] ?? "VibeHmacSecret";
    var hmacSecret = await keyVaultService.GetSecretAsync(hmacSecretName);

    secretConfiguration = new VibeSecretConfiguration
    {
        HmacSecretName = hmacSecretName,
        HmacSecret = hmacSecret
    };
    Log.Information("VIBESQL_STARTUP: Loaded HMAC secret from key vault");
}
else
{
    // Load from appsettings or environment variable
    secretConfiguration = new VibeSecretConfiguration
    {
        HmacSecretName = "VibeHmacSecret",
        HmacSecret = builder.Configuration["VibeSQL:HmacSecret"]
            ?? Environment.GetEnvironmentVariable("VIBESQL_HMAC_SECRET")
            ?? throw new InvalidOperationException(
                "HMAC secret not configured. Set VibeSQL:HmacSecret in appsettings, " +
                "VIBESQL_HMAC_SECRET environment variable, or enable key vault with VibeSQL:UseKeyVault=true")
    };
    Log.Information("VIBESQL_STARTUP: Loaded HMAC secret from configuration");
}

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
- Built-in query limits and security",
        Contact = new OpenApiContact
        {
            Name = "VibeSQL",
            Url = new Uri("https://github.com/PayEz-Net/vibesql-server")
        },
        License = new OpenApiLicense
        {
            Name = "Apache 2.0",
            Url = new Uri("https://www.apache.org/licenses/LICENSE-2.0")
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

// HMAC authentication for service-to-service calls
app.UseMiddleware<HmacAuthMiddleware>();

app.MapControllers();

Log.Information("VibeSQL Server starting on {Environment}", app.Environment.EnvironmentName);

app.Run();
