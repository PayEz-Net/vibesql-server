using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for agent_presence table operations.
/// Abstracts data access from business logic following Clean Architecture.
/// </summary>
public class AgentPresenceRepository : IAgentPresenceRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AgentPresenceRepository> _logger;

    private const string CollectionName = "agent_mail";
    private const string PresenceTable = "agent_presence";

    public AgentPresenceRepository(VibeDbContext context, ILogger<AgentPresenceRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument?> GetPresenceAsync(int clientId, int agentId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == PresenceTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents.FirstOrDefault(d =>
        {
            var data = TryDeserialize<PresenceData>(d.Data);
            return data?.AgentId == agentId;
        });
    }

    public async Task<List<VibeDocument>> GetPresenceByAgentIdsAsync(int clientId, IEnumerable<int> agentIds)
    {
        var agentIdSet = agentIds.ToHashSet();

        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == PresenceTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents.Where(d =>
        {
            var data = TryDeserialize<PresenceData>(d.Data);
            return data != null && agentIdSet.Contains(data.AgentId);
        }).ToList();
    }

    public async Task<List<VibeDocument>> GetAllPresenceAsync(int clientId, string? status = null, int limit = 100, int offset = 0)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == PresenceTable
                     && d.DeletedAt == null)
            .ToListAsync();

        var filtered = documents.AsEnumerable();

        if (!string.IsNullOrEmpty(status))
        {
            filtered = filtered.Where(d =>
            {
                var data = TryDeserialize<PresenceData>(d.Data);
                return data != null && string.Equals(data.Status, status, StringComparison.OrdinalIgnoreCase);
            });
        }

        return filtered
            .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    public async Task<VibeDocument> UpsertPresenceAsync(int clientId, int agentId, string status, string? statusMessage, string? clientInfoJson)
    {
        var now = DateTimeOffset.UtcNow;
        var nowStr = now.ToString("o");

        // Check for existing presence
        var existing = await GetPresenceAsync(clientId, agentId);

        if (existing != null)
        {
            // Update existing
            var existingData = TryDeserialize<PresenceData>(existing.Data);
            if (existingData != null)
            {
                existingData.Status = status;
                existingData.StatusMessage = statusMessage;
                existingData.LastSeen = nowStr;
                existingData.LastHeartbeatAt = nowStr;
                existingData.UpdatedAt = nowStr;

                if (status == "online" && string.IsNullOrEmpty(existingData.ConnectedAt))
                {
                    existingData.ConnectedAt = nowStr;
                }
                else if (status == "offline")
                {
                    existingData.ConnectedAt = null;
                }

                if (!string.IsNullOrEmpty(clientInfoJson))
                {
                    existingData.ClientInfo = JsonSerializer.Deserialize<JsonElement>(clientInfoJson);
                }

                existing.Data = JsonSerializer.Serialize(existingData);
                existing.UpdatedAt = now;
                await _context.SaveChangesAsync();

                _logger.LogDebug("AGENT_PRESENCE_UPDATED: AgentId={AgentId}, Status={Status}",
                    agentId, status);

                return existing;
            }
        }

        // Create new
        var nextId = await GetNextPresenceIdAsync(clientId);

        var presenceData = new PresenceData
        {
            Id = nextId,
            AgentId = agentId,
            Status = status,
            StatusMessage = statusMessage,
            LastSeen = nowStr,
            LastHeartbeatAt = nowStr,
            ConnectedAt = status == "online" ? nowStr : null,
            ClientInfo = !string.IsNullOrEmpty(clientInfoJson)
                ? JsonSerializer.Deserialize<JsonElement>(clientInfoJson)
                : null,
            CreatedAt = nowStr,
            UpdatedAt = nowStr
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = 0,
            Collection = CollectionName,
            TableName = PresenceTable,
            Data = JsonSerializer.Serialize(presenceData),
            CreatedAt = now,
            CreatedBy = 0
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogDebug("AGENT_PRESENCE_CREATED: AgentId={AgentId}, Status={Status}",
            agentId, status);

        return document;
    }

    public async Task<bool> UpdateHeartbeatAsync(int clientId, int agentId)
    {
        var now = DateTimeOffset.UtcNow;
        var nowStr = now.ToString("o");

        var existing = await GetPresenceAsync(clientId, agentId);
        if (existing == null)
            return false;

        var existingData = TryDeserialize<PresenceData>(existing.Data);
        if (existingData == null)
            return false;

        existingData.LastSeen = nowStr;
        existingData.LastHeartbeatAt = nowStr;
        existingData.UpdatedAt = nowStr;

        // If offline, set to online
        if (existingData.Status == "offline")
        {
            existingData.Status = "online";
            existingData.ConnectedAt = nowStr;
        }

        existing.Data = JsonSerializer.Serialize(existingData);
        existing.UpdatedAt = now;
        await _context.SaveChangesAsync();

        _logger.LogDebug("AGENT_PRESENCE_HEARTBEAT: AgentId={AgentId}", agentId);

        return true;
    }

    public async Task<bool> MarkOfflineAsync(int clientId, int agentId)
    {
        var now = DateTimeOffset.UtcNow;
        var nowStr = now.ToString("o");

        var existing = await GetPresenceAsync(clientId, agentId);
        if (existing == null)
            return false;

        var existingData = TryDeserialize<PresenceData>(existing.Data);
        if (existingData == null)
            return false;

        existingData.Status = "offline";
        existingData.LastSeen = nowStr;
        existingData.ConnectedAt = null;
        existingData.UpdatedAt = nowStr;

        existing.Data = JsonSerializer.Serialize(existingData);
        existing.UpdatedAt = now;
        await _context.SaveChangesAsync();

        _logger.LogInformation("AGENT_PRESENCE_OFFLINE: AgentId={AgentId}", agentId);

        return true;
    }

    public async Task<List<VibeDocument>> GetStalePresenceAsync(int clientId, TimeSpan heartbeatThreshold)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(heartbeatThreshold);

        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == PresenceTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents.Where(d =>
        {
            var data = TryDeserialize<PresenceData>(d.Data);
            if (data == null || data.Status == "offline")
                return false;

            if (string.IsNullOrEmpty(data.LastHeartbeatAt))
                return true; // No heartbeat ever, consider stale

            if (DateTimeOffset.TryParse(data.LastHeartbeatAt, out var lastHb))
            {
                return lastHb < cutoff;
            }

            return false;
        }).ToList();
    }

    public async Task<int> CountOnlineAgentsAsync(int clientId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == PresenceTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents.Count(d =>
        {
            var data = TryDeserialize<PresenceData>(d.Data);
            return data?.Status == "online";
        });
    }

    public async Task<int> CountAwayAgentsAsync(int clientId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == PresenceTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents.Count(d =>
        {
            var data = TryDeserialize<PresenceData>(d.Data);
            return data?.Status == "away";
        });
    }

    private async Task<int> GetNextPresenceIdAsync(int clientId)
    {
        var seqName = $"vibe.seq_{clientId}_{CollectionName}_presence";

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
            var allPresence = await _context.Documents
                .Where(d => d.ClientId == clientId
                         && d.Collection == CollectionName
                         && d.TableName == PresenceTable
                         && d.DeletedAt == null)
                .ToListAsync();

            var maxId = allPresence
                .Select(d => TryDeserialize<PresenceData>(d.Data)?.Id ?? 0)
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

    private class PresenceData
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("agent_id")]
        public int AgentId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = "offline";

        [System.Text.Json.Serialization.JsonPropertyName("status_message")]
        public string? StatusMessage { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("last_seen")]
        public string? LastSeen { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("last_heartbeat_at")]
        public string? LastHeartbeatAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("connected_at")]
        public string? ConnectedAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("client_info")]
        public JsonElement? ClientInfo { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("updated_at")]
        public string? UpdatedAt { get; set; }
    }
}
