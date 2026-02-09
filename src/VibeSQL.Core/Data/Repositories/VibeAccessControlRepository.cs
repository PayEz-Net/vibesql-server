using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for email access control operations.
/// </summary>
public class VibeAccessControlRepository : IVibeAccessControlRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<VibeAccessControlRepository> _logger;

    public VibeAccessControlRepository(VibeDbContext context, ILogger<VibeAccessControlRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Access Control Config

    public async Task<AccessControlConfig?> GetConfigAsync(int clientId)
    {
        return await _context.AccessControlConfigs.FindAsync(clientId);
    }

    public async Task<AccessControlConfig> UpsertConfigAsync(int clientId, string mode, int? updatedBy)
    {
        var config = await _context.AccessControlConfigs.FindAsync(clientId);

        if (config == null)
        {
            config = new AccessControlConfig
            {
                ClientId = clientId,
                Mode = mode,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = updatedBy
            };
            _context.AccessControlConfigs.Add(config);
        }
        else
        {
            config.Mode = mode;
            config.UpdatedAt = DateTimeOffset.UtcNow;
            config.UpdatedBy = updatedBy;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("VIBE_ACCESS_CONTROL: Updated config for ClientId={ClientId}, Mode={Mode}", clientId, mode);
        return config;
    }

    #endregion

    #region Email Access List

    public async Task<List<EmailAccessListEntry>> GetEmailListAsync(int clientId, string? listType = null)
    {
        var query = _context.EmailAccessListEntries
            .Where(e => e.ClientId == clientId);

        if (!string.IsNullOrEmpty(listType))
        {
            query = query.Where(e => e.ListType == listType);
        }

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<EmailAccessListEntry?> GetEmailEntryByIdAsync(int id)
    {
        return await _context.EmailAccessListEntries.FindAsync(id);
    }

    public async Task<EmailAccessListEntry?> FindEmailEntryAsync(int clientId, string listType, string email)
    {
        return await _context.EmailAccessListEntries
            .FirstOrDefaultAsync(e =>
                e.ClientId == clientId &&
                e.ListType == listType &&
                e.Email.ToLower() == email.ToLower());
    }

    public async Task<EmailAccessListEntry> AddEmailEntryAsync(EmailAccessListEntry entry)
    {
        entry.CreatedAt = DateTimeOffset.UtcNow;
        _context.EmailAccessListEntries.Add(entry);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_ACCESS_CONTROL: Added email {Email} to {ListType} list for ClientId={ClientId}",
            entry.Email, entry.ListType, entry.ClientId);
        return entry;
    }

    public async Task<int> AddEmailEntriesBulkAsync(int clientId, string listType, IEnumerable<string> emails, string? reason, int? createdBy)
    {
        var now = DateTimeOffset.UtcNow;
        var addedCount = 0;

        foreach (var email in emails)
        {
            var normalizedEmail = email.Trim().ToLower();

            // Check for duplicate
            var exists = await _context.EmailAccessListEntries
                .AnyAsync(e =>
                    e.ClientId == clientId &&
                    e.ListType == listType &&
                    e.Email.ToLower() == normalizedEmail);

            if (!exists)
            {
                _context.EmailAccessListEntries.Add(new EmailAccessListEntry
                {
                    ClientId = clientId,
                    ListType = listType,
                    Email = normalizedEmail,
                    Reason = reason,
                    CreatedAt = now,
                    CreatedBy = createdBy
                });
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("VIBE_ACCESS_CONTROL: Bulk added {Count} emails to {ListType} list for ClientId={ClientId}",
                addedCount, listType, clientId);
        }

        return addedCount;
    }

    public async Task<bool> DeleteEmailEntryAsync(int id)
    {
        var entry = await _context.EmailAccessListEntries.FindAsync(id);
        if (entry == null)
        {
            return false;
        }

        _context.EmailAccessListEntries.Remove(entry);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_ACCESS_CONTROL: Deleted email {Email} from {ListType} list for ClientId={ClientId}",
            entry.Email, entry.ListType, entry.ClientId);
        return true;
    }

    public async Task<bool> IsEmailAllowedAsync(int clientId, string email)
    {
        var config = await GetConfigAsync(clientId);

        // If no config or mode is off, allow all
        if (config == null || config.Mode == "off")
        {
            return true;
        }

        var normalizedEmail = email.Trim().ToLower();

        if (config.Mode == "allow_list")
        {
            // Only allow if email is in allow list
            return await _context.EmailAccessListEntries
                .AnyAsync(e =>
                    e.ClientId == clientId &&
                    e.ListType == "allow" &&
                    e.Email.ToLower() == normalizedEmail &&
                    (e.ExpiresAt == null || e.ExpiresAt > DateTimeOffset.UtcNow));
        }
        else if (config.Mode == "block_list")
        {
            // Block if email is in block list
            var isBlocked = await _context.EmailAccessListEntries
                .AnyAsync(e =>
                    e.ClientId == clientId &&
                    e.ListType == "block" &&
                    e.Email.ToLower() == normalizedEmail &&
                    (e.ExpiresAt == null || e.ExpiresAt > DateTimeOffset.UtcNow));
            return !isBlocked;
        }

        return true;
    }

    #endregion
}
