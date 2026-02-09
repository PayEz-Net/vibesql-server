using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Data.Common;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for virtual index operations.
/// </summary>
public class VirtualIndexRepository : IVirtualIndexRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<VirtualIndexRepository> _logger;

    public VirtualIndexRepository(VibeDbContext context, ILogger<VirtualIndexRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<VirtualIndex>> GetActiveIndexesAsync(int clientId, string collection)
    {
        return await _context.VirtualIndexes
            .Where(v => v.ClientId == clientId && v.Collection == collection && v.DroppedAt == null)
            .OrderBy(v => v.TableName)
            .ThenBy(v => v.IndexName)
            .ToListAsync();
    }

    public async Task<VirtualIndex?> GetByNameAsync(int clientId, string collection, string indexName)
    {
        return await _context.VirtualIndexes
            .FirstOrDefaultAsync(v =>
                v.ClientId == clientId &&
                v.Collection == collection &&
                v.IndexName == indexName &&
                v.DroppedAt == null);
    }

    public async Task<VirtualIndex?> GetByIdAsync(int virtualIndexId)
    {
        return await _context.VirtualIndexes.FindAsync(virtualIndexId);
    }

    public async Task<VirtualIndex> CreateAsync(VirtualIndex virtualIndex)
    {
        _context.VirtualIndexes.Add(virtualIndex);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_INDEX_CREATED: Index={IndexName}, Client={ClientId}, Partition={Partition}",
            virtualIndex.IndexName, virtualIndex.ClientId, virtualIndex.PartitionName);

        return virtualIndex;
    }

    public async Task<bool> UpdateAsync(VirtualIndex virtualIndex)
    {
        _context.VirtualIndexes.Update(virtualIndex);
        var result = await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_INDEX_UPDATED: Index={IndexName}, Client={ClientId}",
            virtualIndex.IndexName, virtualIndex.ClientId);

        return result > 0;
    }

    public async Task<int> GetActiveIndexCountAsync(int clientId)
    {
        return await _context.VirtualIndexes
            .CountAsync(v => v.ClientId == clientId && v.DroppedAt == null);
    }

    public async Task<string?> GetPartitionNameAsync(int clientId)
    {
        // Simplified - partition lookup not critical for self-healing feature
        return null;
    }

    public async Task<PartitionInfo?> GetPartitionInfoAsync(int clientId)
    {
        // Simplified - partition info not critical for self-healing feature
        return await Task.FromResult<PartitionInfo?>(null);
    }

    public async Task<int> GetTierLimitAsync(int clientId)
    {
        // Default tier limit - can be enhanced later with proper tier configuration lookup
        return await Task.FromResult(5);
    }

    public async Task ExecuteDDLAsync(string ddl, int timeoutSeconds = 300)
    {
        // Must use separate connection - CREATE INDEX CONCURRENTLY can't run in transaction
        // Use DbConnection abstraction which works with Devart PostgreSQL provider
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        
        try
        {
            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = ddl;
            cmd.CommandTimeout = timeoutSeconds;
            await cmd.ExecuteNonQueryAsync();

            _logger.LogInformation("VIBE_INDEX_DDL: Executed DDL command");
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
