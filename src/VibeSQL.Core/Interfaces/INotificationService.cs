using VibeSQL.Core.Models.Standup;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for sending notifications (SMS, email) for autonomy events.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends an SMS message via Twilio.
    /// </summary>
    /// <param name="phoneNumber">Recipient phone number (E.164 format)</param>
    /// <param name="message">Message content</param>
    Task SendSmsAsync(string phoneNumber, string message);

    /// <summary>
    /// Sends a notification when a milestone is completed.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="milestone">Completed milestone name</param>
    /// <param name="summary">Activity summary</param>
    /// <param name="phoneNumber">Recipient phone number</param>
    Task SendMilestoneCompleteAsync(int projectId, string milestone, StandupSummary summary, string phoneNumber);

    /// <summary>
    /// Sends an alert when a blocker threshold is reached.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="blockedCount">Number of blocked tasks</param>
    /// <param name="phoneNumber">Recipient phone number</param>
    Task SendBlockerAlertAsync(int projectId, int blockedCount, string phoneNumber);

    /// <summary>
    /// Sends an alert when autonomy is stopped for any reason.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="reason">Stop reason</param>
    /// <param name="summary">Activity summary</param>
    /// <param name="phoneNumber">Recipient phone number</param>
    Task SendAutonomyStoppedAsync(int projectId, string reason, StandupSummary summary, string phoneNumber);
}
