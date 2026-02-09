using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Models.Standup;

namespace VibeSQL.Core.Services.Notifications;

/// <summary>
/// Service for sending notifications via Twilio SMS.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;
    private readonly string? _accountSid;
    private readonly string? _authToken;
    private readonly string? _fromNumber;

    public NotificationService(
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new ArgumentException("Phone number is required", nameof(phoneNumber));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message is required", nameof(message));
        }

        // Validate phone number format (basic E.164 check)
        if (!phoneNumber.StartsWith("+"))
        {
            throw new ArgumentException("Phone number must be in E.164 format (e.g., +1234567890)", nameof(phoneNumber));
        }

        if (!_enabled)
        {
            _logger.LogWarning(
                "Twilio not configured - would send SMS to {PhoneNumber}: {Message}",
                phoneNumber, message);
            return;
        }

        try
        {
            var messageResource = await MessageResource.CreateAsync(
                to: new PhoneNumber(phoneNumber),
                from: new PhoneNumber(_fromNumber),
                body: message);

            _logger.LogInformation(
                "SMS sent successfully to {PhoneNumber}. SID: {MessageSid}",
                phoneNumber, messageResource.Sid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send SMS to {PhoneNumber}",
                phoneNumber);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SendMilestoneCompleteAsync(
        int projectId,
        string milestone,
        StandupSummary summary,
        string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            _logger.LogWarning(
                "No phone number configured for project {ProjectId} - skipping milestone notification",
                projectId);
            return;
        }

        var message = $"[Vibe Agents] Milestone '{milestone}' complete!\n\n" +
                     $"Project {projectId}:\n" +
                     $"- {summary.TasksCompleted} tasks done\n" +
                     $"- {summary.TasksBlocked} blocked\n" +
                     $"- {summary.ReviewsPassed} reviews passed\n\n" +
                     $"Autonomy paused - review standup for details.";

        await SendSmsAsync(phoneNumber, message);
    }

    /// <inheritdoc />
    public async Task SendBlockerAlertAsync(int projectId, int blockedCount, string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            _logger.LogWarning(
                "No phone number configured for project {ProjectId} - skipping blocker alert",
                projectId);
            return;
        }

        var message = $"[Vibe Agents] Blocker alert!\n\n" +
                     $"Project {projectId} has {blockedCount} blocked tasks.\n" +
                     $"Autonomy paused - intervention needed.";

        await SendSmsAsync(phoneNumber, message);
    }

    /// <inheritdoc />
    public async Task SendAutonomyStoppedAsync(
        int projectId,
        string reason,
        StandupSummary summary,
        string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            _logger.LogWarning(
                "No phone number configured for project {ProjectId} - skipping stop notification",
                projectId);
            return;
        }

        var reasonText = reason switch
        {
            "milestone_complete" => "Milestone complete",
            "blocker_threshold" => "Too many blockers",
            "time_limit" => "Time limit reached",
            "manual" => "Manual stop",
            _ => reason
        };

        var message = $"[Vibe Agents] Autonomy stopped.\n\n" +
                     $"Project {projectId}: {reasonText}\n" +
                     $"- {summary.TasksCompleted} completed\n" +
                     $"- {summary.TasksBlocked} blocked\n" +
                     $"- {summary.TotalEntries} activity entries\n\n" +
                     $"Review standup for full details.";

        await SendSmsAsync(phoneNumber, message);
    }
}
