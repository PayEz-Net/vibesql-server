// File: VibeSQL.Core/Interfaces/IReadReceiptService.cs
// Interface for Agent Mail Read Receipt Service

using VibeSQL.Core.DTOs.AgentMail;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for managing read receipts on agent mail messages.
/// </summary>
public interface IReadReceiptService
{
    /// <summary>
    /// Get list of readers for a message.
    /// Only accessible by the message sender.
    /// </summary>
    /// <param name="clientId">Client/tenant ID</param>
    /// <param name="messageId">Message ID to check</param>
    /// <param name="requestingAgentId">Agent making the request (must be sender)</param>
    /// <returns>Readers response with read/pending lists</returns>
    Task<MessageReadersResponse> GetMessageReadersAsync(int clientId, long messageId, int requestingAgentId);
    
    /// <summary>
    /// Mark a message as read by the current agent.
    /// Idempotent - won't update if already read.
    /// </summary>
    /// <param name="clientId">Client/tenant ID</param>
    /// <param name="messageId">Message ID to mark as read</param>
    /// <param name="agentId">Agent marking the message as read</param>
    /// <returns>Mark read response with receipt status</returns>
    Task<MarkReadResponse> MarkMessageAsReadAsync(int clientId, long messageId, int agentId);
    
    /// <summary>
    /// Get read status for multiple messages at once.
    /// Only returns data for messages where requester is sender.
    /// </summary>
    /// <param name="clientId">Client/tenant ID</param>
    /// <param name="messageIds">Message IDs to check</param>
    /// <param name="requestingAgentId">Agent making the request</param>
    /// <returns>List of read statuses</returns>
    Task<BulkReadStatusResponse> GetBulkReadStatusAsync(int clientId, List<long> messageIds, int requestingAgentId);
    
    /// <summary>
    /// Get current read receipt settings for an agent.
    /// </summary>
    /// <param name="clientId">Client/tenant ID</param>
    /// <param name="agentId">Agent ID</param>
    /// <returns>Current settings</returns>
    Task<ReadReceiptSettingsResponse> GetReadReceiptSettingsAsync(int clientId, int agentId);
    
    /// <summary>
    /// Update read receipt settings for an agent.
    /// </summary>
    /// <param name="clientId">Client/tenant ID</param>
    /// <param name="agentId">Agent ID</param>
    /// <param name="sendReadReceipts">Whether to send read receipts</param>
    /// <returns>Updated settings</returns>
    Task<UpdateReadReceiptSettingsResponse> UpdateReadReceiptSettingsAsync(int clientId, int agentId, bool sendReadReceipts);
}
