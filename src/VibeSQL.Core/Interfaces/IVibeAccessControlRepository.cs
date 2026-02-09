using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository interface for email access control operations.
/// </summary>
public interface IVibeAccessControlRepository
{
    // Access Control Config
    Task<AccessControlConfig?> GetConfigAsync(int clientId);
    Task<AccessControlConfig> UpsertConfigAsync(int clientId, string mode, int? updatedBy);

    // Email Access List
    Task<List<EmailAccessListEntry>> GetEmailListAsync(int clientId, string? listType = null);
    Task<EmailAccessListEntry?> GetEmailEntryByIdAsync(int id);
    Task<EmailAccessListEntry?> FindEmailEntryAsync(int clientId, string listType, string email);
    Task<EmailAccessListEntry> AddEmailEntryAsync(EmailAccessListEntry entry);
    Task<int> AddEmailEntriesBulkAsync(int clientId, string listType, IEnumerable<string> emails, string? reason, int? createdBy);
    Task<bool> DeleteEmailEntryAsync(int id);
    Task<bool> IsEmailAllowedAsync(int clientId, string email);
}
