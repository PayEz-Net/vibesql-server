using VibeSQL.Core.Entities;
using VibeSQL.Core.Models.Escalation;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for coordinator escalation and emergency shutdown.
/// Provides authority to shut down all development activity and notify human stakeholders.
/// </summary>
public interface IEscalationService
{
    /// <summary>
    /// Gets the current escalation settings for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>Current escalation settings</returns>
    Task<EscalationSettings?> GetSettingsAsync(int projectId);

    /// <summary>
    /// Updates escalation settings for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="update">Settings to update</param>
    /// <returns>Updated settings</returns>
    Task<EscalationSettings> UpdateSettingsAsync(int projectId, EscalationSettingsUpdate update);

    /// <summary>
    /// Checks escalation triggers based on current sensitivity level.
    /// Called by the coordinator loop to detect issues.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>Check result indicating if escalation should occur</returns>
    Task<EscalationCheck> CheckTriggersAsync(int projectId);

    /// <summary>
    /// Triggers an escalation - halts autonomy and notifies human.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="check">The escalation check that triggered this</param>
    /// <returns>Result of the escalation</returns>
    Task<EscalationResult> TriggerEscalationAsync(int projectId, EscalationCheck check);

    /// <summary>
    /// Triggers a manual escalation with custom reason.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="reason">Reason for manual escalation</param>
    /// <param name="shutdownModeOverride">Optional shutdown mode override</param>
    /// <returns>Result of the escalation</returns>
    Task<EscalationResult> TriggerManualEscalationAsync(int projectId, string reason, string? shutdownModeOverride = null);

    /// <summary>
    /// Resumes after an escalation.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="request">Resume request with resolution details</param>
    /// <returns>True if resumed successfully</returns>
    Task<bool> ResumeAsync(int projectId, ResumeRequest request);

    /// <summary>
    /// Gets escalation history for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="since">Optional start date filter</param>
    /// <param name="limit">Maximum number of entries to return</param>
    /// <returns>List of escalation log entries</returns>
    Task<IEnumerable<EscalationLog>> GetHistoryAsync(int projectId, DateTime? since = null, int limit = 20);

    /// <summary>
    /// Gets the most recent unresolved escalation for a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>Unresolved escalation if any</returns>
    Task<EscalationLog?> GetActiveEscalationAsync(int projectId);
}
