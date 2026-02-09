using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for agent_mail_attachments table operations.
/// Stores attachment metadata in VibeDocument JSON structure.
/// </summary>
public class AgentMailAttachmentRepository : IAgentMailAttachmentRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AgentMailAttachmentRepository> _logger;

    private const string CollectionName = "agent_mail";
    private const string AttachmentsTable = "agent_mail_attachments";

    public AgentMailAttachmentRepository(VibeDbContext context, ILogger<AgentMailAttachmentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument?> GetAttachmentAsync(int clientId, int attachmentId)
    {
        var attachments = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == AttachmentsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return attachments.FirstOrDefault(d =>
        {
            var data = TryDeserialize<AttachmentData>(d.Data);
            return data?.Id == attachmentId;
        });
    }

    public async Task<List<VibeDocument>> GetAttachmentsByMessageAsync(int clientId, int messageId)
    {
        var attachments = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == AttachmentsTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return attachments.Where(d =>
        {
            var data = TryDeserialize<AttachmentData>(d.Data);
            return data?.MessageId == messageId;
        }).OrderBy(d => d.CreatedAt).ToList();
    }

    public async Task<VibeDocument> CreateAttachmentAsync(
        int clientId,
        int userId,
        int messageId,
        string filename,
        string contentType,
        long sizeBytes,
        string storageKey,
        int? uploadedByAgentId,
        string? description)
    {
        var now = DateTimeOffset.UtcNow;

        // Get next ID from sequence
        var nextId = await GetNextAttachmentIdAsync(clientId);

        var attachmentData = new
        {
            id = nextId,
            message_id = messageId,
            filename = filename,
            content_type = contentType,
            size_bytes = sizeBytes,
            storage_key = storageKey,
            description = description,
            uploaded_by_agent_id = uploadedByAgentId,
            uploaded_by_user_id = userId,
            created_at = now.ToString("o")
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = userId,
            Collection = CollectionName,
            TableName = AttachmentsTable,
            Data = JsonSerializer.Serialize(attachmentData),
            CreatedAt = now,
            CreatedBy = userId
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created attachment: AttachmentId={AttachmentId}, MessageId={MessageId}, Filename={Filename}, ClientId={ClientId}",
            nextId, messageId, filename, clientId);

        return document;
    }

    public async Task<bool> DeleteAttachmentAsync(int clientId, int attachmentId, int userId)
    {
        var attachment = await GetAttachmentAsync(clientId, attachmentId);
        if (attachment == null)
            return false;

        attachment.DeletedAt = DateTimeOffset.UtcNow;
        attachment.UpdatedBy = userId;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted attachment: AttachmentId={AttachmentId}, ClientId={ClientId}, DeletedBy={UserId}",
            attachmentId, clientId, userId);

        return true;
    }

    public async Task<bool> MessageHasAttachmentsAsync(int clientId, int messageId)
    {
        var count = await GetAttachmentCountAsync(clientId, messageId);
        return count > 0;
    }

    public async Task<int> GetAttachmentCountAsync(int clientId, int messageId)
    {
        var attachments = await GetAttachmentsByMessageAsync(clientId, messageId);
        return attachments.Count;
    }

    private async Task<int> GetNextAttachmentIdAsync(int clientId)
    {
        var attachments = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == AttachmentsTable)
            .ToListAsync();

        if (!attachments.Any())
            return 1;

        var maxId = attachments
            .Select(d => TryDeserialize<AttachmentData>(d.Data)?.Id ?? 0)
            .Max();

        return maxId + 1;
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    // Internal class for deserialization
    private class AttachmentData
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public string? Filename { get; set; }
        public string? ContentType { get; set; }
        public long SizeBytes { get; set; }
        public string? StorageKey { get; set; }
        public string? Description { get; set; }
        public int? UploadedByAgentId { get; set; }
        public int UploadedByUserId { get; set; }
        public string? CreatedAt { get; set; }
    }
}
