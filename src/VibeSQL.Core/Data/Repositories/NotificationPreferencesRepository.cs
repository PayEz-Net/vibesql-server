using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for notification preferences per agent.
/// Stores preferences in Vibe documents for multi-tenant isolation.
/// </summary>
public class NotificationPreferencesRepository : INotificationPreferencesRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<NotificationPreferencesRepository> _logger;

    private const string CollectionName = "agent_mail";
    private const string TableName = "notification_preferences";

    public NotificationPreferencesRepository(VibeDbContext context, ILogger<NotificationPreferencesRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument?> GetByAgentIdAsync(int clientId, int agentId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents.FirstOrDefault(d =>
        {
            var data = TryDeserialize<PreferencesData>(d.Data);
            return data?.AgentId == agentId;
        });
    }

    public async Task<List<VibeDocument>> GetByUserAsync(int clientId, int userId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.OwnerUserId == userId
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents;
    }

    public async Task<VibeDocument> UpsertAsync(
        int clientId,
        int agentId,
        bool enabled,
        List<string> eventTypes,
        string minImportance,
        string? quietHoursJson,
        bool includePreview,
        bool soundEnabled,
        bool badgeEnabled,
        List<string> mutedAgents,
        List<string> mutedThreads)
    {
        var now = DateTimeOffset.UtcNow;
        var nowStr = now.ToString("o");

        var existing = await GetByAgentIdAsync(clientId, agentId);

        if (existing != null)
        {
            // Update existing
            var existingData = TryDeserialize<PreferencesData>(existing.Data);
            if (existingData != null)
            {
                existingData.Enabled = enabled;
                existingData.EventTypes = eventTypes;
                existingData.MinImportance = minImportance;
                existingData.QuietHoursJson = quietHoursJson;
                existingData.IncludePreview = includePreview;
                existingData.SoundEnabled = soundEnabled;
                existingData.BadgeEnabled = badgeEnabled;
                existingData.MutedAgents = mutedAgents;
                existingData.MutedThreads = mutedThreads;
                existingData.UpdatedAt = nowStr;

                existing.Data = JsonSerializer.Serialize(existingData);
                existing.UpdatedAt = now;
                await _context.SaveChangesAsync();

                _logger.LogDebug(
                    "NOTIFICATION_PREFS_UPDATED: ClientId={ClientId}, AgentId={AgentId}, Enabled={Enabled}",
                    clientId, agentId, enabled);

                return existing;
            }
        }

        // Create new
        var nextId = await GetNextIdAsync(clientId);

        var prefsData = new PreferencesData
        {
            Id = nextId,
            AgentId = agentId,
            Enabled = enabled,
            EventTypes = eventTypes,
            MinImportance = minImportance,
            QuietHoursJson = quietHoursJson,
            IncludePreview = includePreview,
            SoundEnabled = soundEnabled,
            BadgeEnabled = badgeEnabled,
            MutedAgents = mutedAgents,
            MutedThreads = mutedThreads,
            CreatedAt = nowStr,
            UpdatedAt = nowStr
        };

        // Get the agent owner to set OwnerUserId
        int? ownerUserId = await GetAgentOwnerAsync(clientId, agentId);

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = ownerUserId,
            Collection = CollectionName,
            TableName = TableName,
            Data = JsonSerializer.Serialize(prefsData),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "NOTIFICATION_PREFS_CREATED: ClientId={ClientId}, AgentId={AgentId}, Enabled={Enabled}",
            clientId, agentId, enabled);

        return document;
    }

    public async Task<bool> DeleteAsync(int clientId, int agentId)
    {
        var doc = await GetByAgentIdAsync(clientId, agentId);
        if (doc == null) return false;

        doc.DeletedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> IsAgentMutedAsync(int clientId, int recipientAgentId, string fromAgentName)
    {
        var doc = await GetByAgentIdAsync(clientId, recipientAgentId);
        if (doc == null) return false;

        var data = TryDeserialize<PreferencesData>(doc.Data);
        if (data?.MutedAgents == null) return false;

        return data.MutedAgents.Any(a => a.Equals(fromAgentName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsThreadMutedAsync(int clientId, int agentId, string threadId)
    {
        var doc = await GetByAgentIdAsync(clientId, agentId);
        if (doc == null) return false;

        var data = TryDeserialize<PreferencesData>(doc.Data);
        if (data?.MutedThreads == null) return false;

        return data.MutedThreads.Any(t => t.Equals(threadId, StringComparison.OrdinalIgnoreCase));
    }

    #region Helpers

    private async Task<int> GetNextIdAsync(int clientId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == TableName)
            .ToListAsync();

        if (!documents.Any()) return 1;

        var maxId = documents
            .Select(d => TryDeserialize<PreferencesData>(d.Data)?.Id ?? 0)
            .DefaultIfEmpty(0)
            .Max();

        return maxId + 1;
    }

    private async Task<int?> GetAgentOwnerAsync(int clientId, int agentId)
    {
        // Look up agent to get owner
        var agents = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == "agent_mail_agents"
                     && d.DeletedAt == null)
            .ToListAsync();

        var agent = agents.FirstOrDefault(d =>
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(d.Data ?? "{}");
                if (jsonDoc.RootElement.TryGetProperty("id", out var idProp))
                {
                    return idProp.GetInt32() == agentId;
                }
            }
            catch { }
            return false;
        });

        if (agent != null)
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(agent.Data ?? "{}");
                if (jsonDoc.RootElement.TryGetProperty("owner_user_id", out var ownerProp))
                {
                    return ownerProp.GetInt32();
                }
            }
            catch { }
        }

        return null;
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Internal Data Model

    private class PreferencesData
    {
        public int Id { get; set; }
        public int AgentId { get; set; }
        public bool Enabled { get; set; } = true;
        public List<string> EventTypes { get; set; } = new();
        public string MinImportance { get; set; } = "low";
        public string? QuietHoursJson { get; set; }
        public bool IncludePreview { get; set; } = true;
        public bool SoundEnabled { get; set; } = true;
        public bool BadgeEnabled { get; set; } = true;
        public List<string> MutedAgents { get; set; } = new();
        public List<string> MutedThreads { get; set; } = new();
        public string? CreatedAt { get; set; }
        public string? UpdatedAt { get; set; }
    }

    #endregion
}
