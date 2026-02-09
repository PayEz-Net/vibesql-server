using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for agent_mail_attachments table operations.
/// Handles database operations for message attachments metadata.
/// </summary>
public interface IAgentMailAttachmentRepository
{
    /// <summary>
    /// Get attachment by ID.
    /// </summary>
    Task<VibeDocument?> GetAttachmentAsync(int clientId, int attachmentId);

    /// <summary>
    /// Get all attachments for a message.
    /// </summary>
    Task<List<VibeDocument>> GetAttachmentsByMessageAsync(int clientId, int messageId);

    /// <summary>
    /// Create a new attachment record.
    /// </summary>
    Task<VibeDocument> CreateAttachmentAsync(
        int clientId,
        int userId,
        int messageId,
        string filename,
        string contentType,
        long sizeBytes,
        string storageKey,
        int? uploadedByAgentId,
        string? description);

    /// <summary>
    /// Delete an attachment (soft delete).
    /// </summary>
    Task<bool> DeleteAttachmentAsync(int clientId, int attachmentId, int userId);

    /// <summary>
    /// Check if a message has any attachments.
    /// </summary>
    Task<bool> MessageHasAttachmentsAsync(int clientId, int messageId);

    /// <summary>
    /// Get total attachment count for a message.
    /// </summary>
    Task<int> GetAttachmentCountAsync(int clientId, int messageId);
}
