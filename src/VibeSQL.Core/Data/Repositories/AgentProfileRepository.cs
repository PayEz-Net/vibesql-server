using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Models;
using VibeSQL.Core.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for loading agent profile data from Vibe SQL.
/// Bridges IDP authentication with Vibe agent configuration data.
/// </summary>
public class AgentProfileRepository : IAgentProfileRepository
{
    private readonly VibeDbContext _vibeContext;
    private readonly ILogger<AgentProfileRepository> _logger;
    private readonly int _agentSystemClientId;
    private readonly string _collectionName;

    private const string ProfilesTable = "agent_profiles";
    private const string CapabilitiesTable = "agent_capabilities";
    private const string TeamsTable = "agent_teams";

    public AgentProfileRepository(
        VibeDbContext vibeContext,
        IConfiguration configuration,
        ILogger<AgentProfileRepository> logger)
    {
        _vibeContext = vibeContext;
        _logger = logger;

        // Load from configuration
        _agentSystemClientId = configuration.GetValue<int>("AgentAuth:VibeClientId", 1);
        _collectionName = configuration.GetValue<string>("AgentAuth:Collection", "agent_mail");
    }

    public async Task<AgentProfile?> GetAgentProfileAsync(int agentProfileId)
    {
        try
        {
            // Optimized: Use AsAsyncEnumerable to avoid loading all documents into memory
            var query = _vibeContext.Documents
                .Where(d => d.ClientId == _agentSystemClientId
                         && d.Collection == _collectionName
                         && d.TableName == ProfilesTable
                         && d.DeletedAt == null)
                .AsAsyncEnumerable();

            await foreach (var document in query)
            {
                var data = TryDeserialize<AgentProfileData>(document.Data);
                if (data?.Id == agentProfileId)
                {
                    return new AgentProfile
                    {
                        Id = data.Id,
                        TeamId = data.TeamId,
                        Name = data.Name,
                        DisplayName = data.DisplayName,
                        Username = data.Username,
                        RolePreset = data.RolePreset,
                        IsActive = data.IsActive
                    };
                }
            }

            _logger.LogWarning("Agent profile {AgentProfileId} not found in Vibe", agentProfileId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading agent profile {AgentProfileId} from Vibe", agentProfileId);
            return null;
        }
    }

    public async Task<IEnumerable<string>> GetAgentCapabilitiesAsync(int agentProfileId)
    {
        try
        {
            // Optimized: Stream results instead of loading all into memory
            var capabilities = new List<string>();
            var query = _vibeContext.Documents
                .Where(d => d.ClientId == _agentSystemClientId
                         && d.Collection == _collectionName
                         && d.TableName == CapabilitiesTable
                         && d.DeletedAt == null)
                .AsAsyncEnumerable();

            await foreach (var document in query)
            {
                var data = TryDeserialize<AgentCapabilityData>(document.Data);
                if (data != null && data.AgentId == agentProfileId && data.Enabled)
                {
                    capabilities.Add(data.Capability);
                }
            }

            return capabilities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading agent capabilities for profile {AgentProfileId}", agentProfileId);
            return Enumerable.Empty<string>();
        }
    }

    public async Task<AgentTeam?> GetAgentTeamAsync(int agentProfileId)
    {
        try
        {
            // First get the agent profile to get team_id
            var profile = await GetAgentProfileAsync(agentProfileId);
            if (profile == null || profile.TeamId == 0)
            {
                _logger.LogWarning("No team found for agent profile {AgentProfileId}", agentProfileId);
                return null;
            }

            // Optimized: Stream results to find team
            var query = _vibeContext.Documents
                .Where(d => d.ClientId == _agentSystemClientId
                         && d.Collection == _collectionName
                         && d.TableName == TeamsTable
                         && d.DeletedAt == null)
                .AsAsyncEnumerable();

            await foreach (var document in query)
            {
                var data = TryDeserialize<AgentTeamData>(document.Data);
                if (data?.Id == profile.TeamId)
                {
                    return new AgentTeam
                    {
                        Id = data.Id,
                        OwnerUserId = data.OwnerUserId,
                        TenantId = data.TenantId,
                        Name = data.Name,
                        TeamType = data.TeamType,
                        IsActive = data.IsActive
                    };
                }
            }

            _logger.LogWarning("Team {TeamId} not found for agent profile {AgentProfileId}", profile.TeamId, agentProfileId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading agent team for profile {AgentProfileId}", agentProfileId);
            return null;
        }
    }

    public async Task<bool> IsAgentActiveAsync(int agentProfileId)
    {
        var profile = await GetAgentProfileAsync(agentProfileId);
        return profile?.IsActive ?? false;
    }

    public async Task<AgentProfile?> GetCoordinatorAgentAsync(int projectId)
    {
        try
        {
            var query = _vibeContext.Documents
                .Where(d => d.ClientId == _agentSystemClientId
                         && d.Collection == _collectionName
                         && d.TableName == ProfilesTable
                         && d.DeletedAt == null)
                .AsAsyncEnumerable();

            await foreach (var document in query)
            {
                var data = TryDeserialize<AgentProfileData>(document.Data);
                if (data != null && data.ProjectId == projectId && data.IsCoordinator && data.IsActive)
                {
                    return MapToAgentProfile(data);
                }
            }

            _logger.LogDebug("No coordinator agent found for project {ProjectId}", projectId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding coordinator agent for project {ProjectId}", projectId);
            return null;
        }
    }

    public async Task<IEnumerable<AgentProfile>> GetProjectAgentsAsync(int projectId, bool activeOnly = true)
    {
        try
        {
            var agents = new List<AgentProfile>();
            var query = _vibeContext.Documents
                .Where(d => d.ClientId == _agentSystemClientId
                         && d.Collection == _collectionName
                         && d.TableName == ProfilesTable
                         && d.DeletedAt == null)
                .AsAsyncEnumerable();

            await foreach (var document in query)
            {
                var data = TryDeserialize<AgentProfileData>(document.Data);
                if (data != null && data.ProjectId == projectId)
                {
                    if (!activeOnly || data.IsActive)
                    {
                        agents.Add(MapToAgentProfile(data));
                    }
                }
            }

            return agents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading agents for project {ProjectId}", projectId);
            return Enumerable.Empty<AgentProfile>();
        }
    }

    public async Task<IEnumerable<AgentProfile>> GetIdleAgentsAsync(int projectId, TimeSpan idleThreshold)
    {
        try
        {
            var idleAgents = new List<AgentProfile>();
            var cutoffTime = DateTime.UtcNow - idleThreshold;

            var query = _vibeContext.Documents
                .Where(d => d.ClientId == _agentSystemClientId
                         && d.Collection == _collectionName
                         && d.TableName == ProfilesTable
                         && d.DeletedAt == null)
                .AsAsyncEnumerable();

            await foreach (var document in query)
            {
                var data = TryDeserialize<AgentProfileData>(document.Data);
                if (data != null && data.ProjectId == projectId && data.IsActive && data.Status == "active")
                {
                    // Agent is idle if last_activated_at is null or older than cutoff
                    if (!data.LastActivatedAt.HasValue || data.LastActivatedAt.Value < cutoffTime)
                    {
                        idleAgents.Add(MapToAgentProfile(data));
                    }
                }
            }

            return idleAgents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding idle agents for project {ProjectId}", projectId);
            return Enumerable.Empty<AgentProfile>();
        }
    }

    private static AgentProfile MapToAgentProfile(AgentProfileData data)
    {
        return new AgentProfile
        {
            Id = data.Id,
            TeamId = data.TeamId,
            Name = data.Name,
            DisplayName = data.DisplayName,
            Username = data.Username,
            RolePreset = data.RolePreset,
            IsActive = data.IsActive,
            IsCoordinator = data.IsCoordinator,
            ProjectId = data.ProjectId,
            Status = data.Status,
            Role = data.Role
        };
    }

    /// <summary>
    /// Safely deserializes JSON string to typed object.
    /// Returns null if deserialization fails.
    /// </summary>
    private static T? TryDeserialize<T>(string json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Internal data models for JSON deserialization

    private class AgentProfileData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("team_id")]
        public int TeamId { get; set; }

        [JsonPropertyName("project_id")]
        public int? ProjectId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("role_preset")]
        public string? RolePreset { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }

        [JsonPropertyName("is_coordinator")]
        public bool IsCoordinator { get; set; }

        [JsonPropertyName("last_activated_at")]
        public DateTime? LastActivatedAt { get; set; }
    }

    private class AgentCapabilityData
    {
        [JsonPropertyName("agent_id")]
        public int AgentId { get; set; }

        [JsonPropertyName("capability")]
        public string Capability { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }

    private class AgentTeamData
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("owner_user_id")]
        public int OwnerUserId { get; set; }

        [JsonPropertyName("tenant_id")]
        public string? TenantId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("team_type")]
        public string? TeamType { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }
    }
}
