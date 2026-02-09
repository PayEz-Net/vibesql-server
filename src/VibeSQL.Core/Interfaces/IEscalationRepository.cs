using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for escalation_log table operations.
/// </summary>
public interface IEscalationRepository
{
    /// <summary>
    /// Creates a new escalation log entry.
    /// </summary>
    Task<EscalationLog> CreateAsync(EscalationLog escalation);

    /// <summary>
    /// Gets an escalation log entry by ID.
    /// </summary>
    Task<EscalationLog?> GetByIdAsync(int id);

    /// <summary>
    /// Gets the most recent unresolved escalation for a project.
    /// </summary>
    Task<EscalationLog?> GetActiveEscalationAsync(int projectId);

    /// <summary>
    /// Gets escalation history for a project.
    /// </summary>
    Task<IEnumerable<EscalationLog>> GetHistoryAsync(int projectId, DateTime? since = null, int limit = 20);

    /// <summary>
    /// Updates an escalation log entry (for resolution).
    /// </summary>
    Task<EscalationLog> UpdateAsync(EscalationLog escalation);

    /// <summary>
    /// Updates the notification sent timestamp.
    /// </summary>
    Task UpdateNotificationSentAsync(int escalationId, DateTime sentAt);
}
