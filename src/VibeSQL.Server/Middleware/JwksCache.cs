using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace VibeSQL.Server.Middleware;

/// <summary>
/// Fetches and caches IDP JWKS for local JWT validation.
/// Refreshes on a 24-hour TTL. Thread-safe.
/// </summary>
public class JwksCache : BackgroundService
{
    private readonly ILogger<JwksCache> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _idpBaseUrl;
    private readonly TimeSpan _refreshInterval;

    private JsonWebKeySet? _cachedJwks;
    private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public JwksCache(
        ILogger<JwksCache> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _idpBaseUrl = configuration["Authentication:IdpBaseUrl"] ?? "https://idp.payez.net";
        _refreshInterval = TimeSpan.FromHours(24);
    }

    /// <summary>
    /// Get the current cached JWKS. Triggers refresh if stale or empty.
    /// </summary>
    public async Task<JsonWebKeySet?> GetJwksAsync(CancellationToken ct = default)
    {
        if (_cachedJwks != null && DateTimeOffset.UtcNow - _lastRefresh < _refreshInterval)
        {
            return _cachedJwks;
        }

        await _refreshLock.WaitAsync(ct);
        try
        {
            if (_cachedJwks != null && DateTimeOffset.UtcNow - _lastRefresh < _refreshInterval)
            {
                return _cachedJwks;
            }

            await RefreshAsync(ct);
            return _cachedJwks;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        var jwksUrl = _idpBaseUrl.TrimEnd('/') + "/.well-known/jwks.json";
        _logger.LogInformation("VSQL_JWKS_REFRESH: Fetching JWKS from {Url}", jwksUrl);

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var response = await client.GetAsync(jwksUrl, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            JsonWebKeySet? jwks = null;

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("data", out var dataValue) && dataValue.ValueKind == JsonValueKind.Object)
                {
                    jwks = new JsonWebKeySet(dataValue.GetRawText());
                }
                else if (root.TryGetProperty("keys", out var keysValue) && keysValue.ValueKind == JsonValueKind.Array)
                {
                    jwks = new JsonWebKeySet(json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VSQL_JWKS_PARSE: Failed to parse JWKS response, attempting raw parse");
                jwks = new JsonWebKeySet(json);
            }

            if (jwks == null || !jwks.GetSigningKeys().Any())
            {
                _logger.LogError("VSQL_JWKS_EMPTY: JWKS response contained no valid signing keys");
                return;
            }

            _cachedJwks = jwks;
            _lastRefresh = DateTimeOffset.UtcNow;
            _logger.LogInformation(
                "VSQL_JWKS_REFRESH_OK: Loaded {KeyCount} signing keys. Next refresh at {NextRefresh}",
                jwks.GetSigningKeys().Count(),
                _lastRefresh.Add(_refreshInterval));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "VSQL_JWKS_REFRESH_FAIL: Failed to fetch JWKS from {Url}. Using stale cache if available.",
                jwksUrl);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_refreshInterval, stoppingToken);
                await RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VSQL_JWKS_BACKGROUND: Background refresh failed");
            }
        }
    }
}
