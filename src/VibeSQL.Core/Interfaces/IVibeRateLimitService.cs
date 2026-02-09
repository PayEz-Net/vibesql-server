namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Rate limiting service interface for Vibe operations.
/// Provides distributed locking and request throttling capabilities.
/// </summary>
public interface IVibeRateLimitService
{
    /// <summary>
    /// Attempts to acquire a distributed lock. Returns true if lock acquired.
    /// </summary>
    Task<bool> TryAcquireLockAsync(string lockKey, TimeSpan expiry);

    /// <summary>
    /// Releases a distributed lock.
    /// </summary>
    Task ReleaseLockAsync(string lockKey);

    /// <summary>
    /// Checks if a client IP is blocked.
    /// </summary>
    Task<(bool IsBlocked, int RetryAfterSeconds)> IsBlockedAsync(string clientIp);

    /// <summary>
    /// Checks rate limit for an IP/endpoint combination.
    /// </summary>
    Task<(bool IsRateLimited, int RequestCount)> CheckRateLimitAsync(string clientIp, string endpoint);

    /// <summary>
    /// Increments request count for IP-based rate limiting.
    /// </summary>
    Task IncrementRequestCountAsync(string clientIp, string endpoint);

    /// <summary>
    /// Increments failed authentication count for IP blocking.
    /// </summary>
    Task IncrementFailedAuthAsync(string clientIp);

    /// <summary>
    /// Checks rate limit per client ID for sensitive endpoints.
    /// </summary>
    /// <param name="clientId">The IDP client ID</param>
    /// <param name="endpoint">Endpoint identifier</param>
    /// <param name="requestsPerMinute">Max requests per minute for this endpoint</param>
    /// <returns>Tuple of (IsRateLimited, CurrentRequestCount)</returns>
    Task<(bool IsRateLimited, int RequestCount)> CheckClientRateLimitAsync(int clientId, string endpoint, int requestsPerMinute);

    /// <summary>
    /// Increments request count for per-client rate limiting.
    /// </summary>
    Task IncrementClientRequestCountAsync(int clientId, string endpoint);

    /// <summary>
    /// Increments the concurrent query counter for a client. Call before query execution.
    /// Returns the new concurrent count.
    /// </summary>
    Task<int> IncrementConcurrentQueryAsync(int clientId);

    /// <summary>
    /// Decrements the concurrent query counter for a client. Call after query completes (in finally block).
    /// </summary>
    Task DecrementConcurrentQueryAsync(int clientId);

    /// <summary>
    /// Gets the current concurrent query count for a client.
    /// </summary>
    Task<int> GetConcurrentQueryCountAsync(int clientId);
}
