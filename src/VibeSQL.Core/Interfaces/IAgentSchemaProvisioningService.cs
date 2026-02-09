namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for provisioning vibe_agents schema for clients.
/// Called when admin enables Vibe Agents for a client.
/// </summary>
public interface IAgentSchemaProvisioningService
{
    /// <summary>
    /// Provision vibe_agents schema for a client.
    /// Idempotent - only provisions if schema doesn't exist for client.
    /// </summary>
    /// <param name="clientId">The client ID to provision for</param>
    /// <param name="adminUserId">Admin user performing the action (for audit)</param>
    /// <returns>Provisioning result</returns>
    Task<AgentSchemaProvisionResult> ProvisionAsync(int clientId, int? adminUserId = null);

    /// <summary>
    /// Check if vibe_agents schema is provisioned for a client.
    /// </summary>
    Task<bool> IsProvisionedAsync(int clientId);

    /// <summary>
    /// Get provisioning status and stats for a client.
    /// </summary>
    Task<AgentSchemaStatus> GetStatusAsync(int clientId);

    /// <summary>
    /// Seed default data (project, team, agents) for a client.
    /// Copies template documents from client_id=0 to the target client.
    /// </summary>
    Task<AgentSeedResult> SeedDefaultDataAsync(int clientId, int? adminUserId = null);
}

/// <summary>
/// Result of schema provisioning operation.
/// </summary>
public class AgentSchemaProvisionResult
{
    public bool Success { get; set; }
    public bool AlreadyProvisioned { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? ProvisionedAt { get; set; }
    public int TablesCreated { get; set; }

    public static AgentSchemaProvisionResult Succeeded(DateTimeOffset provisionedAt, int tablesCreated) => new()
    {
        Success = true,
        ProvisionedAt = provisionedAt,
        TablesCreated = tablesCreated
    };

    public static AgentSchemaProvisionResult AlreadyExists() => new()
    {
        Success = true,
        AlreadyProvisioned = true
    };

    public static AgentSchemaProvisionResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}

/// <summary>
/// Status of vibe_agents schema for a client.
/// </summary>
public class AgentSchemaStatus
{
    public bool IsProvisioned { get; set; }
    public DateTimeOffset? ProvisionedAt { get; set; }
    public int TableCount { get; set; }
    public int AgentCount { get; set; }
    public int TeamCount { get; set; }
    public int MessageCount { get; set; }
    public int ProjectCount { get; set; }
}

/// <summary>
/// Result of seeding default data for a client.
/// </summary>
public class AgentSeedResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int ProjectsCreated { get; set; }
    public int TeamsCreated { get; set; }
    public int AgentsCreated { get; set; }
    public int BoardsCreated { get; set; }
    public int Skipped { get; set; }

    public static AgentSeedResult Succeeded(int projects, int teams, int agents, int boards, int skipped = 0) => new()
    {
        Success = true,
        ProjectsCreated = projects,
        TeamsCreated = teams,
        AgentsCreated = agents,
        BoardsCreated = boards,
        Skipped = skipped
    };

    public static AgentSeedResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}
