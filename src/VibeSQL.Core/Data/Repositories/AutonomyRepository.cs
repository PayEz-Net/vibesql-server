using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for autonomy_settings table operations.
/// </summary>
public class AutonomyRepository : IAutonomyRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AutonomyRepository> _logger;

    private const string CollectionName = "vibe_agents";
    private const string TableName = "autonomy_settings";

    public AutonomyRepository(VibeDbContext context, ILogger<AutonomyRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AutonomySettings?> GetSettingsAsync(int projectId)
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == 0
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents
            .Select(d => ParseSettings(d))
            .FirstOrDefault(s => s?.ProjectId == projectId);
    }

    public async Task<AutonomySettings> UpsertSettingsAsync(AutonomySettings settings)
    {
        var now = DateTimeOffset.UtcNow;

        // Check if settings exist
        var existing = await GetSettingsAsync(settings.ProjectId);

        if (existing != null)
        {
            // Update existing
            var documents = await _context.Documents
                .Where(d => d.ClientId == 0
                         && d.Collection == CollectionName
                         && d.TableName == TableName
                         && d.DeletedAt == null)
                .ToListAsync();

            var document = documents.FirstOrDefault(d =>
            {
                var s = ParseSettings(d);
                return s?.ProjectId == settings.ProjectId;
            });

            if (document != null)
            {
                settings.UpdatedAt = now.DateTime;
                document.Data = JsonSerializer.Serialize(ToDataObject(settings));
                document.UpdatedAt = now;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Updated autonomy settings for project {ProjectId}",
                    settings.ProjectId);

                return settings;
            }
        }

        // Create new
        var nextId = await GetNextSettingIdAsync();
        settings.SettingId = nextId;
        settings.CreatedAt = now.DateTime;
        settings.UpdatedAt = now.DateTime;

        var newDocument = new VibeDocument
        {
            ClientId = 0,
            Collection = CollectionName,
            TableName = TableName,
            Data = JsonSerializer.Serialize(ToDataObject(settings)),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Documents.Add(newDocument);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created autonomy settings {SettingId} for project {ProjectId}",
            nextId, settings.ProjectId);

        return settings;
    }

    public async Task UpdateEnabledAsync(int projectId, bool enabled)
    {
        var settings = await GetSettingsAsync(projectId);
        if (settings == null)
        {
            _logger.LogWarning(
                "Cannot update enabled flag - no settings found for project {ProjectId}",
                projectId);
            return;
        }

        settings.Enabled = enabled;
        await UpsertSettingsAsync(settings);
    }

    public async Task UpdateStartedAtAsync(int projectId, DateTime? startedAt)
    {
        var settings = await GetSettingsAsync(projectId);
        if (settings == null)
        {
            _logger.LogWarning(
                "Cannot update started_at - no settings found for project {ProjectId}",
                projectId);
            return;
        }

        settings.StartedAt = startedAt;
        await UpsertSettingsAsync(settings);
    }

    public async Task UpdateCoordinatorLoopEnabledAsync(int projectId, bool enabled)
    {
        var settings = await GetSettingsAsync(projectId);
        if (settings == null)
        {
            _logger.LogWarning(
                "Cannot update coordinator_loop_enabled - no settings found for project {ProjectId}",
                projectId);
            return;
        }

        settings.CoordinatorLoopEnabled = enabled;
        await UpsertSettingsAsync(settings);
    }

    public async Task UpdateCoordinatorLoopLastRunAtAsync(int projectId, DateTime lastRunAt)
    {
        var settings = await GetSettingsAsync(projectId);
        if (settings == null)
        {
            _logger.LogWarning(
                "Cannot update coordinator_loop_last_run_at - no settings found for project {ProjectId}",
                projectId);
            return;
        }

        settings.CoordinatorLoopLastRunAt = lastRunAt;
        await UpsertSettingsAsync(settings);
    }

    public async Task<IEnumerable<AutonomySettings>> GetActiveCoordinatorLoopsAsync()
    {
        var documents = await _context.Documents
            .Where(d => d.ClientId == 0
                     && d.Collection == CollectionName
                     && d.TableName == TableName
                     && d.DeletedAt == null)
            .ToListAsync();

        return documents
            .Select(d => ParseSettings(d))
            .Where(s => s != null && s.Enabled && s.CoordinatorLoopEnabled)
            .Cast<AutonomySettings>()
            .ToList();
    }

    private AutonomySettings? ParseSettings(VibeDocument document)
    {
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document.Data);
            if (data == null) return null;

            return new AutonomySettings
            {
                SettingId = data["setting_id"].GetInt32(),
                ProjectId = data["project_id"].GetInt32(),
                Enabled = data["enabled"].GetBoolean(),
                Mode = data["mode"].GetString() ?? "attended",
                StopCondition = data["stop_condition"].GetString() ?? "milestone",
                CurrentSpecId = data.TryGetValue("current_spec_id", out var specId) && specId.ValueKind != JsonValueKind.Null
                    ? specId.GetInt32()
                    : null,
                CurrentMilestone = data.TryGetValue("current_milestone", out var milestone) && milestone.ValueKind != JsonValueKind.Null
                    ? milestone.GetString()
                    : null,
                MaxRuntimeHours = data["max_runtime_hours"].GetInt32(),
                StartedAt = data.TryGetValue("started_at", out var startedAt) && startedAt.ValueKind != JsonValueKind.Null
                    ? startedAt.GetDateTimeOffset().DateTime
                    : null,
                NotifyPhone = data.TryGetValue("notify_phone", out var phone) && phone.ValueKind != JsonValueKind.Null
                    ? phone.GetString()
                    : null,
                NotifyEmail = data.TryGetValue("notify_email", out var email) && email.ValueKind != JsonValueKind.Null
                    ? email.GetString()
                    : null,
                SkipPermissions = data.TryGetValue("skip_permissions", out var skipPerms) && skipPerms.ValueKind != JsonValueKind.Null
                    && skipPerms.GetBoolean(),
                CoordinatorLoopEnabled = data.TryGetValue("coordinator_loop_enabled", out var loopEnabled) && loopEnabled.ValueKind != JsonValueKind.Null
                    && loopEnabled.GetBoolean(),
                CoordinatorLoopIntervalMinutes = data.TryGetValue("coordinator_loop_interval_minutes", out var interval) && interval.ValueKind != JsonValueKind.Null
                    ? interval.GetInt32()
                    : 5,
                CoordinatorLoopIdleThresholdMinutes = data.TryGetValue("coordinator_loop_idle_threshold_minutes", out var idleThreshold) && idleThreshold.ValueKind != JsonValueKind.Null
                    ? idleThreshold.GetInt32()
                    : 10,
                CoordinatorLoopReviewThresholdMinutes = data.TryGetValue("coordinator_loop_review_threshold_minutes", out var reviewThreshold) && reviewThreshold.ValueKind != JsonValueKind.Null
                    ? reviewThreshold.GetInt32()
                    : 15,
                CoordinatorLoopLastRunAt = data.TryGetValue("coordinator_loop_last_run_at", out var lastRun) && lastRun.ValueKind != JsonValueKind.Null
                    ? lastRun.GetDateTimeOffset().DateTime
                    : null,
                EscalationSensitivity = data.TryGetValue("escalation_sensitivity", out var escSensitivity) && escSensitivity.ValueKind != JsonValueKind.Null
                    ? escSensitivity.GetInt32()
                    : 2,
                EscalationShutdownMode = data.TryGetValue("escalation_shutdown_mode", out var escMode) && escMode.ValueKind != JsonValueKind.Null
                    ? escMode.GetString() ?? "soft"
                    : "soft",
                NotifyWebhookUrl = data.TryGetValue("notify_webhook_url", out var webhookUrl) && webhookUrl.ValueKind != JsonValueKind.Null
                    ? webhookUrl.GetString()
                    : null,
                EscalationCooldownMinutes = data.TryGetValue("escalation_cooldown_minutes", out var cooldown) && cooldown.ValueKind != JsonValueKind.Null
                    ? cooldown.GetInt32()
                    : 30,
                LastEscalationAt = data.TryGetValue("last_escalation_at", out var lastEsc) && lastEsc.ValueKind != JsonValueKind.Null
                    ? lastEsc.GetDateTimeOffset().DateTime
                    : null,
                EscalationCount = data.TryGetValue("escalation_count", out var escCount) && escCount.ValueKind != JsonValueKind.Null
                    ? escCount.GetInt32()
                    : 0,
                CreatedAt = data["created_at"].GetDateTimeOffset().DateTime,
                UpdatedAt = data["updated_at"].GetDateTimeOffset().DateTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse autonomy settings from document {DocumentId}", document.DocumentId);
            return null;
        }
    }

    private object ToDataObject(AutonomySettings settings)
    {
        return new
        {
            setting_id = settings.SettingId,
            project_id = settings.ProjectId,
            enabled = settings.Enabled,
            mode = settings.Mode,
            stop_condition = settings.StopCondition,
            current_spec_id = settings.CurrentSpecId,
            current_milestone = settings.CurrentMilestone,
            max_runtime_hours = settings.MaxRuntimeHours,
            started_at = settings.StartedAt,
            notify_phone = settings.NotifyPhone,
            notify_email = settings.NotifyEmail,
            skip_permissions = settings.SkipPermissions,
            coordinator_loop_enabled = settings.CoordinatorLoopEnabled,
            coordinator_loop_interval_minutes = settings.CoordinatorLoopIntervalMinutes,
            coordinator_loop_idle_threshold_minutes = settings.CoordinatorLoopIdleThresholdMinutes,
            coordinator_loop_review_threshold_minutes = settings.CoordinatorLoopReviewThresholdMinutes,
            coordinator_loop_last_run_at = settings.CoordinatorLoopLastRunAt,
            escalation_sensitivity = settings.EscalationSensitivity,
            escalation_shutdown_mode = settings.EscalationShutdownMode,
            notify_webhook_url = settings.NotifyWebhookUrl,
            escalation_cooldown_minutes = settings.EscalationCooldownMinutes,
            last_escalation_at = settings.LastEscalationAt,
            escalation_count = settings.EscalationCount,
            created_at = settings.CreatedAt,
            updated_at = settings.UpdatedAt
        };
    }

    private async Task<int> GetNextSettingIdAsync()
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
                    return data?["setting_id"].GetInt32() ?? 0;
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
