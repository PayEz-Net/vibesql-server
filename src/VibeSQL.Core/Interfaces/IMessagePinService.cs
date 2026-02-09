using VibeSQL.Core.DTOs.AgentMail;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for message pinning operations in Agent Mail.
/// Handles business logic, validation, authorization, and limit enforcement.
/// </summary>
public interface IMessagePinService
{
    /// <summary>
    /// List pins for the authenticated agent.
    /// </summary>
    Task<PinsListResult> ListPinsAsync(
        string clientId,
        int userId,
        string agentName,
        PinsListQuery query);

    /// <summary>
    /// Pin a message. Idempotent - returns existing pin if already pinned.
    /// </summary>
    Task<PinResult> PinMessageAsync(
        string clientId,
        int userId,
        string agentName,
        int messageId,
        CreatePinRequest request);

    /// <summary>
    /// Unpin a message.
    /// </summary>
    Task<UnpinResult> UnpinMessageAsync(
        string clientId,
        int userId,
        string agentName,
        int messageId,
        string pinType,
        string? channelId);

    /// <summary>
    /// Update a pin's note and/or position.
    /// </summary>
    Task<PinResult> UpdatePinAsync(
        string clientId,
        int userId,
        string agentName,
        string pinId,
        UpdatePinRequest request);
}
