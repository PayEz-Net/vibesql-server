using VibeSQL.Core.DTOs.AgentMail;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Application service interface for agent mail attachment operations.
/// Handles business logic for file uploads, downloads, and attachment management.
/// </summary>
public interface IAgentMailAttachmentService
{
    /// <summary>
    /// Upload an attachment to a message.
    /// Validates permissions, stores file to disk, and creates metadata record.
    /// </summary>
    Task<AgentMailAttachmentUploadResult> UploadAsync(
        string clientId,
        int userId,
        AgentMailAttachmentUploadRequest request,
        Stream fileStream);

    /// <summary>
    /// Download an attachment.
    /// Validates permissions and returns file stream.
    /// </summary>
    Task<AgentMailAttachmentDownloadResult> DownloadAsync(
        string clientId,
        int userId,
        int attachmentId);

    /// <summary>
    /// List all attachments for a message.
    /// </summary>
    Task<AgentMailAttachmentListResult> ListByMessageAsync(
        string clientId,
        int userId,
        int messageId);

    /// <summary>
    /// Delete an attachment.
    /// Validates permissions, deletes file from disk, and soft-deletes metadata.
    /// </summary>
    Task<AgentMailAttachmentDeleteResult> DeleteAsync(
        string clientId,
        int userId,
        int attachmentId);
}
