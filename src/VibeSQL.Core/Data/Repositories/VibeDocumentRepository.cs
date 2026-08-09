using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Exceptions;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for Vibe document data access operations.
/// </summary>
public class VibeDocumentRepository : IVibeDocumentRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<VibeDocumentRepository> _logger;
    private readonly IClientReferenceValidator _clientReferenceValidator;

    /// <summary>
    /// TEMPORARY carve-out for the pre-existing client_id=0 sentinel (card 186212 /
    /// M-201). client_id 0 was used, undocumented, as a code-only "global" marker before
    /// this validator existed -- 128 live rows at client_id=0 in vibe.documents_default on
    /// 93 as of 2026-08-09. docs/cross-schema-reference-constraints.md's target end-state is
    /// client_id IS NULL for genuinely-global rows, with 0 retired entirely (4-step
    /// migration: move mis-parked rows to their real client, move legitimately-global rows
    /// to NULL, make the column nullable, THEN enforce). That migration has NOT run yet --
    /// it touches live production data and needs its own reviewed card, not a 3am solo edit.
    /// Until it does, rejecting client_id=0 here would break every caller still relying on
    /// the current sentinel. Remove this carve-out in the same change that retires the
    /// client_id=0 sentinel; do not let it become permanent by default.
    /// </summary>
    private const int LegacyGlobalSentinelClientId = 0;

    public VibeDocumentRepository(
        VibeDbContext context,
        ILogger<VibeDocumentRepository> logger,
        IClientReferenceValidator clientReferenceValidator)
    {
        _context = context;
        _logger = logger;
        _clientReferenceValidator = clientReferenceValidator;
    }

    public async Task<VibeDocument?> GetByIdAsync(int clientId, string collection, string tableName, int documentId)
    {
        return await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == collection
                     && d.TableName == tableName
                     && d.DocumentId == documentId
                     && d.DeletedAt == null)
            .FirstOrDefaultAsync();
    }

    public async Task<(List<VibeDocument> Documents, int TotalCount)> ListAsync(
        int clientId, string collection, string tableName, int page, int pageSize)
    {
        var query = _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == collection
                     && d.TableName == tableName
                     && d.DeletedAt == null);

        var totalCount = await query.CountAsync();

        var documents = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (documents, totalCount);
    }

    public async Task<(VibeDocument Document, Dictionary<string, object>? GeneratedKeys)> CreateAsync(
        int clientId, int userId, string collection, string tableName, string data, int? createdBy)
    {
        // Mode B cross-schema reference constraint (docs/cross-schema-reference-constraints.md,
        // card 186212 / Sentinel M-201): refuse a write naming a client_id that does not exist,
        // instead of silently orphaning tenant data. See LegacyGlobalSentinelClientId for the
        // one deliberate, temporary exception.
        if (clientId != LegacyGlobalSentinelClientId)
        {
            var clientExists = await _clientReferenceValidator.ClientExistsAsync(clientId);
            if (!clientExists)
            {
                _logger.LogWarning(
                    "VIBE_DOC_UNKNOWN_CLIENT: Refused document write for non-existent client. ClientId: {ClientId}, Collection: {Collection}, Table: {TableName}",
                    clientId, collection, tableName);
                throw new UnknownClientReferenceException(clientId);
            }
        }

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = userId,
            Collection = collection,
            TableName = tableName,
            Data = data,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy ?? userId
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_DOC_CREATED: DocumentId: {DocumentId}, Client: {ClientId}, Collection: {Collection}",
            document.DocumentId, clientId, collection);

        return (document, null);
    }

    public async Task<VibeDocument?> UpdateAsync(int documentId, string data, int? updatedBy)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document == null || document.DeletedAt != null)
        {
            return null;
        }

        document.Data = data;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        document.UpdatedBy = updatedBy;

        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_DOC_UPDATED: DocumentId: {DocumentId}", documentId);

        return document;
    }

    public async Task<VibeDocument?> PatchAsync(int documentId, Dictionary<string, object?> patchData, int? updatedBy)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document == null || document.DeletedAt != null)
        {
            return null;
        }

        // Parse existing data, merge with patch, serialize back
        var existingData = string.IsNullOrEmpty(document.Data)
            ? new Dictionary<string, object?>()
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(document.Data)
              ?? new Dictionary<string, object?>();

        foreach (var kvp in patchData)
        {
            existingData[kvp.Key] = kvp.Value;
        }

        document.Data = System.Text.Json.JsonSerializer.Serialize(existingData);
        document.UpdatedAt = DateTimeOffset.UtcNow;
        document.UpdatedBy = updatedBy;

        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_DOC_PATCHED: DocumentId: {DocumentId}", documentId);

        return document;
    }

    public async Task<bool> DeleteAsync(int documentId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document == null || document.DeletedAt != null)
        {
            return false;
        }

        document.DeletedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_DOC_DELETED: DocumentId: {DocumentId}", documentId);

        return true;
    }

    public async Task<long> GetNextSequenceValueAsync(string sequenceName)
    {
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT nextval('{sequenceName}')";
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    public async Task<int> GetDocumentCountAsync(int clientId, string collection, string? tableName = null)
    {
        var query = _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == collection
                     && d.DeletedAt == null);

        if (!string.IsNullOrEmpty(tableName))
        {
            query = query.Where(d => d.TableName == tableName);
        }

        return await query.CountAsync();
    }

    public async Task<List<VibeDocument>> FindByFieldAsync(int clientId, string collection, string tableName, string fieldName, object value)
    {
        // Use raw SQL with JSONB containment operator (@>) for field search
        var jsonValue = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object> { { fieldName, value } });

        return await _context.Documents
            .FromSqlRaw(
                @"SELECT * FROM vibe.documents
                  WHERE client_id = {0}
                    AND collection = {1}
                    AND table_name = {2}
                    AND deleted_at IS NULL
                    AND data @> {3}::jsonb",
                clientId, collection, tableName, jsonValue)
            .ToListAsync();
    }

    public async Task TrackEncryptedValueOwnershipAsync(string ciphertextHash, int clientId, int keyId)
    {
        // Check if already recorded (idempotent)
        var exists = await _context.EncryptedValueOwnerships
            .AnyAsync(e => e.CiphertextHash == ciphertextHash);

        if (!exists)
        {
            var ownership = new VibeEncryptedValueOwnership
            {
                CiphertextHash = ciphertextHash,
                ClientId = clientId,
                KeyId = keyId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.EncryptedValueOwnerships.Add(ownership);
            await _context.SaveChangesAsync();
        }
    }
}
