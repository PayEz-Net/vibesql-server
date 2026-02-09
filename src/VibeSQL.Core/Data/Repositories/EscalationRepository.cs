using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for escalation_log table operations.
/// </summary>
public class EscalationRepository : IEscalationRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<EscalationRepository> _logger;

    private const string CollectionName = "vibe_agents";
    private const string TableName = "escalation_log";

    public EscalationRepository(VibeDbContext context, ILogger<EscalationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EscalationLog> CreateAsync(EscalationLog escalation)
    {
        var now = DateTimeOffset.UtcNow;

        // Get next ID
        var nextId = await GetNextIdAsync();
        escalation.Id = nextId;
        escalation.TriggeredAt = now.DateTime;

        var document = new VibeDocument
        {
            ClientId = escalation.ClientId,
            Collection = CollectionName,
            TableName = TableName,
            Data = JsonSerializer.Serialize(ToDataObject(escalation)),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created escalation log entry {Id} for project {ProjectId}: {TriggerType}",
            nextId, escalation.ProjectId, escalation.TriggerType);

        return escalation;
    }

    public async Task<EscalationLog?> GetByIdAsync(int id)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == 0
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents
            .Select(d => ParseEscalation(d))
            .FirstOrDefault(e => e?.Id == id);
    }

    public async Task<EscalationLog?> GetActiveEscalationAsync(int projectId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == 0
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents
            .Select(d => ParseEscalation(d))
            .Where(e => e != null && e.ProjectId == projectId && e.ResolvedAt == null)
            .OrderByDescending(e => e!.TriggeredAt)
            .FirstOrDefault();
    }

    public async Task<IEnumerable<EscalationLog>> GetHistoryAsync(int projectId, DateTime? since = null, int limit = 20)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == 0
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        var escalations = documents
            .Select(d => ParseEscalation(d))
            .Where(e => e != null && e.ProjectId == projectId)
            .Cast<EscalationLog>();

        if (since.HasValue)
        {
            escalations = escalations.Where(e => e.TriggeredAt >= since.Value);
        }

        return escalations
            .OrderByDescending(e => e.TriggeredAt)
            .Take(limit)
            .ToList();
    }

    public async Task<EscalationLog> UpdateAsync(EscalationLog escalation)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == 0
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        var document = documents.FirstOrDefault(d =>
        {
            var e = ParseEscalation(d);
            return e?.Id == escalation.Id;
        });

        if (document == null)
        {
            throw new InvalidOperationException($"Escalation log entry {escalation.Id} not found");
        }

        document.Data = JsonSerializer.Serialize(ToDataObject(escalation));
        document.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Updated escalation log entry {Id} for project {ProjectId}",
            escalation.Id, escalation.ProjectId);

        return escalation;
    }

    public async Task UpdateNotificationSentAsync(int escalationId, DateTime sentAt)
    {
        var escalation = await GetByIdAsync(escalationId);
        if (escalation == null)
        {
            _logger.LogWarning("Cannot update notification sent - escalation {Id} not found", escalationId);
            return;
        }

        escalation.NotificationSentAt = sentAt;
        await UpdateAsync(escalation);
    }

    private EscalationLog? ParseEscalation(VibeDocument document)
    {
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document.Data);
            if (data == null) return null;

            return new EscalationLog
            {
                Id = data["id"].GetInt32(),
                ClientId = data["client_id"].GetInt32(),
                ProjectId = data["project_id"].GetInt32(),
                TriggeredAt = data.TryGetValue("triggered_at", out var triggeredAt) && triggeredAt.ValueKind != JsonValueKind.Null
                    ? triggeredAt.GetDateTimeOffset().DateTime
                    : DateTime.UtcNow,
                SensitivityLevel = data["sensitivity_level"].GetInt32(),
                TriggerType = data["trigger_type"].GetString() ?? string.Empty,
                TriggerDetails = data.TryGetValue("trigger_details", out var details) && details.ValueKind != JsonValueKind.Null
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(details.GetRawText())
                    : null,
                ShutdownMode = data.TryGetValue("shutdown_mode", out var mode) && mode.ValueKind != JsonValueKind.Null
                    ? mode.GetString()
                    : null,
                NotificationChannels = data.TryGetValue("notification_channels", out var channels) && channels.ValueKind == JsonValueKind.Array
                    ? channels.EnumerateArray().Select(e => e.GetString()!).Where(s => s != null).ToList()
                    : null,
                NotificationSentAt = data.TryGetValue("notification_sent_at", out var sentAt) && sentAt.ValueKind != JsonValueKind.Null
                    ? sentAt.GetDateTimeOffset().DateTime
                    : null,
                ResolvedAt = data.TryGetValue("resolved_at", out var resolvedAt) && resolvedAt.ValueKind != JsonValueKind.Null
                    ? resolvedAt.GetDateTimeOffset().DateTime
                    : null,
                ResolvedBy = data.TryGetValue("resolved_by", out var resolvedBy) && resolvedBy.ValueKind != JsonValueKind.Null
                    ? resolvedBy.GetString()
                    : null,
                ResolutionAction = data.TryGetValue("resolution_action", out var action) && action.ValueKind != JsonValueKind.Null
                    ? action.GetString()
                    : null,
                ResolutionNotes = data.TryGetValue("resolution_notes", out var notes) && notes.ValueKind != JsonValueKind.Null
                    ? notes.GetString()
                    : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse escalation log from document {DocumentId}", document.DocumentId);
            return null;
        }
    }

    private object ToDataObject(EscalationLog escalation)
    {
        return new
        {
            id = escalation.Id,
            client_id = escalation.ClientId,
            project_id = escalation.ProjectId,
            triggered_at = escalation.TriggeredAt,
            sensitivity_level = escalation.SensitivityLevel,
            trigger_type = escalation.TriggerType,
            trigger_details = escalation.TriggerDetails,
            shutdown_mode = escalation.ShutdownMode,
            notification_channels = escalation.NotificationChannels,
            notification_sent_at = escalation.NotificationSentAt,
            resolved_at = escalation.ResolvedAt,
            resolved_by = escalation.ResolvedBy,
            resolution_action = escalation.ResolutionAction,
            resolution_notes = escalation.ResolutionNotes
        };
    }

    private async Task<int> GetNextIdAsync()
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == 0
                     && d.Collection == CollectionName
                     && d.TableName == TableName)
            .ToListAsync();

        var maxId = documents
            .Select(d =>
            {
                try
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(d.Data);
                    return data?["id"].GetInt32() ?? 0;
                }
                catch
                {
                    return 0;
                }
            })
            .DefaultIfEmpty(0)
            .Max();

        return maxId + 1;
    }
}
