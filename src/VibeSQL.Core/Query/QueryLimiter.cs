using Microsoft.Extensions.Configuration;

namespace VibeSQL.Core.Query;

/// <summary>
/// Enforces query result limits and timeouts
/// </summary>
public interface IQueryLimiter
{
    int MaxResultRows { get; }
    int DefaultTimeoutSeconds { get; }
    void CheckRowLimit(int currentRowCount);
    TimeSpan GetTimeout(string? tier = null);
}

public class QueryLimiter : IQueryLimiter
{
    private readonly IConfiguration _configuration;

    public QueryLimiter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public int MaxResultRows => _configuration.GetValue("VibeQueryLimits:MaxResultRows", 1000);
    public int DefaultTimeoutSeconds => _configuration.GetValue("VibeQueryTimeouts:DefaultSeconds", 5);

    public void CheckRowLimit(int currentRowCount)
    {
        if (currentRowCount >= MaxResultRows)
        {
            throw new VibeQueryError(
                VibeErrorCodes.ResultTooLarge,
                "Result set too large",
                $"Query returned more than the maximum allowed {MaxResultRows} rows");
        }
    }

    public TimeSpan GetTimeout(string? tier = null)
    {
        var seconds = tier?.ToLowerInvariant() switch
        {
            "free" => _configuration.GetValue("VibeQueryTimeouts:FreeSeconds", 2),
            "starter" => _configuration.GetValue("VibeQueryTimeouts:StarterSeconds", 5),
            "pro" => _configuration.GetValue("VibeQueryTimeouts:ProSeconds", 10),
            "enterprise" => _configuration.GetValue("VibeQueryTimeouts:EnterpriseSeconds", 30),
            _ => DefaultTimeoutSeconds
        };

        return TimeSpan.FromSeconds(seconds);
    }
}
