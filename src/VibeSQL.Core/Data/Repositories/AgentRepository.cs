using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for agent_mail_agents table operations.
/// Abstracts data access from business logic following Clean Architecture.
/// </summary>
public class AgentRepository : IAgentRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AgentRepository> _logger;

    private const string CollectionName = "agent_mail";
    private const string AgentsTable = "agent_mail_agents";

    public AgentRepository(VibeDbContext context, ILogger<AgentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument?> GetAgentByNameAsync(int clientId, string agentName)
    {
        var agents = await GetAllAgentsAsync(clientId);

        return agents.FirstOrDefault(d =>
        {
            var data = TryDeserialize<AgentData>(d.Data);
            return string.Equals(data?.Name, agentName, StringComparison.OrdinalIgnoreCase);
        });
    }

    public async Task<VibeDocument?> GetAgentByIdAsync(int clientId, int agentId)
    {
        var agents = await GetAllAgentsAsync(clientId);

        return agents.FirstOrDefault(d =>
        {
            var data = TryDeserialize<AgentData>(d.Data);
            return data?.Id == agentId;
        });
    }

    public async Task<Dictionary<string, VibeDocument>> GetAgentsByNamesAsync(int clientId, IEnumerable<string> agentNames)
    {
        var nameSet = agentNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var agents = await GetAllAgentsAsync(clientId);

        var result = new Dictionary<string, VibeDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in agents)
        {
            var data = TryDeserialize<AgentData>(doc.Data);
            if (data?.Name != null && nameSet.Contains(data.Name) && !result.ContainsKey(data.Name))
            {
                result[data.Name] = doc;
            }
        }
        return result;
    }

    public async Task<List<VibeDocument>> GetAllAgentsAsync(int clientId)
    {
        return await _context.Documents
            .AsNoTracking()
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == AgentsTable
                     && d.DeletedAt == null)
            .ToListAsync();
    }

    public async Task<List<VibeDocument>> GetAgentsByOwnerAsync(int clientId, int ownerUserId)
    {
        var allAgents = await GetAllAgentsAsync(clientId);

        return allAgents.Where(d =>
        {
            var data = TryDeserialize<AgentData>(d.Data);
            return data?.OwnerUserId == ownerUserId || data?.IsShared == true;
        }).ToList();
    }

    public async Task<VibeDocument> CreateAgentAsync(
        int clientId,
        int ownerUserId,
        string name,
        string displayName,
        string role,
        string program,
        string model,
        bool isShared = false)
    {
        var now = DateTimeOffset.UtcNow;

        // Get next ID from sequence
        var nextId = await GetNextAgentIdAsync(clientId);

        var agentData = new
        {
            id = nextId,
            owner_user_id = ownerUserId,
            name = name,
            display_name = displayName,
            role = role,
            program = program,
            model = model,
            is_shared = isShared,
            is_active = true,
            created_at = now.ToString("o")
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = ownerUserId,
            Collection = CollectionName,
            TableName = AgentsTable,
            Data = JsonSerializer.Serialize(agentData),
            CreatedAt = now,
            CreatedBy = ownerUserId
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("AGENT_CREATED: AgentId={AgentId}, Name={Name}, ClientId={ClientId}",
            nextId, name, clientId);

        return document;
    }

    public async Task<int> GetAgentCountByOwnerAsync(int clientId, int ownerUserId)
    {
        var allAgents = await GetAllAgentsAsync(clientId);

        return allAgents.Count(d =>
        {
            var data = TryDeserialize<AgentData>(d.Data);
            return data?.OwnerUserId == ownerUserId;
        });
    }

    private async Task<int> GetNextAgentIdAsync(int clientId)
    {
        var seqName = $"vibe.seq_{clientId}_{CollectionName}_agents";

        try
        {
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            try
            {
                // Ensure sequence exists
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = $"CREATE SEQUENCE IF NOT EXISTS {seqName} START WITH 1 INCREMENT BY 1";
                await createCmd.ExecuteNonQueryAsync();

                // Get next value
                using var nextCmd = connection.CreateCommand();
                nextCmd.CommandText = $"SELECT nextval('{seqName}')";
                var result = await nextCmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get sequence value, falling back to max+1");
            var allAgents = await GetAllAgentsAsync(clientId);
            var maxId = allAgents
                .Select(d => TryDeserialize<AgentData>(d.Data)?.Id ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            return maxId + 1;
        }
    }

    private static T? TryDeserialize<T>(string? json) where T : class
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

    private class AgentData
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("owner_user_id")]
        public int OwnerUserId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("is_shared")]
        public bool? IsShared { get; set; }
    }
}
