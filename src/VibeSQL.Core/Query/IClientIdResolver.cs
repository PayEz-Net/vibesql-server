namespace VibeSQL.Core.Query;

/// <summary>
/// Resolves a client identifier slug or string to the numeric client id used for
/// row-level security and document insertion.
/// </summary>
public interface IClientIdResolver
{
    /// <summary>
    /// Resolves a client identifier to a numeric client id.
    /// Accepts numeric strings and configured slug mappings.
    /// </summary>
    Task<int?> ResolveAsync(string clientId);
}
