using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Authentication;

public interface IDynamicSchemeRegistrar
{
    Task RegisterProviderAsync(OidcProvider provider);
    Task UnregisterProviderAsync(string schemeId);
    bool IsRegistered(string schemeId);
}

public sealed class DynamicSchemeRegistrar : IDynamicSchemeRegistrar, IDisposable
{
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly IOptionsMonitorCache<JwtBearerOptions> _optionsCache;
    private readonly ILogger<DynamicSchemeRegistrar> _logger;
    private readonly IHostEnvironment _environment;
    private readonly Dictionary<string, IDisposable?> _managedConfigs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public DynamicSchemeRegistrar(
        IAuthenticationSchemeProvider schemeProvider,
        IOptionsMonitorCache<JwtBearerOptions> optionsCache,
        ILogger<DynamicSchemeRegistrar> logger,
        IHostEnvironment environment)
    {
        _schemeProvider = schemeProvider;
        _optionsCache = optionsCache;
        _logger = logger;
        _environment = environment;
    }

    public async Task RegisterProviderAsync(OidcProvider provider)
    {
        var schemeId = ToSchemeId(provider.ProviderKey);

        var existing = await _schemeProvider.GetSchemeAsync(schemeId);
        if (existing is not null)
        {
            _logger.LogDebug("EDGE_AUTH: Scheme {SchemeId} already registered, updating options", schemeId);
            _optionsCache.TryRemove(schemeId);
            RemoveManagedConfig(schemeId);
        }

        var requireHttps = !_environment.IsDevelopment();
        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            provider.DiscoveryUrl,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = requireHttps })
        {
            AutomaticRefreshInterval = TimeSpan.FromHours(24),
            RefreshInterval = TimeSpan.FromMinutes(5)
        };

        var options = new JwtBearerOptions
        {
            Authority = provider.Issuer,
            Audience = provider.Audience,
            RequireHttpsMetadata = requireHttps,
            ConfigurationManager = configManager,
            TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = provider.Issuer,
                ValidateAudience = true,
                ValidAudience = provider.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(provider.ClockSkewSeconds),
                ValidateIssuerSigningKey = true
            }
        };

        _optionsCache.TryAdd(schemeId, options);

        if (existing is null)
        {
            _schemeProvider.AddScheme(new AuthenticationScheme(schemeId, provider.DisplayName, typeof(JwtBearerHandler)));
        }

        lock (_lock)
        {
            _managedConfigs[schemeId] = configManager as IDisposable;
        }

        _logger.LogInformation("EDGE_AUTH: Registered auth scheme {SchemeId} for issuer {Issuer}",
            schemeId, provider.Issuer);
    }

    public async Task UnregisterProviderAsync(string schemeId)
    {
        var existing = await _schemeProvider.GetSchemeAsync(schemeId);
        if (existing is null)
        {
            _logger.LogDebug("EDGE_AUTH: Scheme {SchemeId} not found, nothing to unregister", schemeId);
            return;
        }

        _schemeProvider.RemoveScheme(schemeId);
        _optionsCache.TryRemove(schemeId);
        RemoveManagedConfig(schemeId);

        _logger.LogInformation("EDGE_AUTH: Unregistered auth scheme {SchemeId}", schemeId);
    }

    public bool IsRegistered(string schemeId)
    {
        lock (_lock)
        {
            return _managedConfigs.ContainsKey(schemeId);
        }
    }

    private void RemoveManagedConfig(string schemeId)
    {
        lock (_lock)
        {
            if (_managedConfigs.Remove(schemeId, out var disposable))
                disposable?.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var kvp in _managedConfigs)
                kvp.Value?.Dispose();
            _managedConfigs.Clear();
        }
    }

    public static string ToSchemeId(string providerKey) => $"oidc-{providerKey}";
}
