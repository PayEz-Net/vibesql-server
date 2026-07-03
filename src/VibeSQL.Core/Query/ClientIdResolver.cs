using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VibeSQL.Core.Query;

/// <summary>
/// Lightweight, generic client ID resolver.
/// Numeric strings are parsed directly; named slugs can be mapped via
/// configuration section <c>VibeSQL:ClientIdMappings:{slug}</c>.
/// </summary>
public class ClientIdResolver : IClientIdResolver
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClientIdResolver> _logger;

    public ClientIdResolver(IConfiguration configuration, ILogger<ClientIdResolver> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<int?> ResolveAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Task.FromResult<int?>(null);
        }

        var trimmed = clientId.Trim();

        // Direct numeric client id
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId) && numericId > 0)
        {
            _logger.LogDebug("CLIENT_RESOLVED: '{ClientId}' -> {NumericId} (numeric)", clientId, numericId);
            return Task.FromResult<int?>(numericId);
        }

        // Configured slug mapping
        var mappingKey = $"VibeSQL:ClientIdMappings:{trimmed}";
        var mappedValue = _configuration[mappingKey];
        if (!string.IsNullOrWhiteSpace(mappedValue) &&
            int.TryParse(mappedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mappedId) &&
            mappedId > 0)
        {
            _logger.LogDebug("CLIENT_RESOLVED: '{ClientId}' -> {MappedId} (via configuration)", clientId, mappedId);
            return Task.FromResult<int?>(mappedId);
        }

        _logger.LogWarning("CLIENT_RESOLVE_FAILED: No client found for '{ClientId}'", clientId);
        return Task.FromResult<int?>(null);
    }
}
