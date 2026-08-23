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
        // 237414: delegates to the real lookup — two implementations of "which partition is
        // this client on" is how the stub era produced a feature that never worked.
        return (await GetPartitionInfoAsync(clientId))?.PartitionName;
    }

    public async Task<PartitionInfo?> GetPartitionInfoAsync(int clientId)
    {
        // 237414: the REAL lookup (was a stub returning null, so every creation died
        // 'Client partition not found'). Source of truth: vibe.partition_assignments.
        // Raw command on the context connection — the entity model deliberately does not
        // grow for two read-only lookups (same trade ExecuteDDLAsync already makes).
        var row = await QuerySingleRowAsync(
            "SELECT partition_name, tier_level FROM vibe.partition_assignments " +
            "WHERE client_id = @client_id AND is_active = true LIMIT 1",
            ("client_id", clientId));
        if (row == null) return null;

        var partitionName = (string)row[0]!;
        return new PartitionInfo
        {
            PartitionName = partitionName,
            TierLevel = Convert.ToInt32(row[1]),
            // Shared unless the name marks a dedicated partition (house naming:
            // documents_shared_NNNN). Conservative: treating dedicated as shared merely adds
            // a redundant client_id predicate; the reverse would index every tenant's rows
            // into one client's index.
            IsShared = !partitionName.Contains("dedicated", StringComparison.OrdinalIgnoreCase),
        };
    }

    public async Task<int> GetTierLimitAsync(int clientId)
    {
        // 237414: the honest per-tier cap from DATA (vibe.tier_limits.max_virtual_indexes),
        // replacing the constant 5 that made the upgrade prompt a lie: the cap fired on real
        // usage while an upgrade delivered nothing. Resolution: client -> partition tier ->
        // tier_limits (tier_level maps to tier_id; level 0, the pre-assignment value, clamps
        // to Free — the conservative cap). FAIL LOUD on missing rows rather than defaulting:
        // an invented cap in either direction is the class this card retires.
        var partition = await GetPartitionInfoAsync(clientId)
            ?? throw new InvalidOperationException(
                $"Client {clientId} has no active partition assignment (vibe.partition_assignments) - cannot resolve a tier limit");

        var row = await QuerySingleRowAsync(
            "SELECT max_virtual_indexes FROM vibe.tier_limits WHERE tier_id = @tier_id",
            ("tier_id", Math.Max(partition.TierLevel, 1)));
        if (row == null)
        {
            throw new InvalidOperationException(
                $"No tier-limit row for tier {Math.Max(partition.TierLevel, 1)} (vibe.tier_limits) - refusing to guess a cap");
        }

        return Convert.ToInt32(row[0]);
    }

    /// <summary>
    /// 237414: single-row parameterized read on the context's connection — same
    /// connection-stewardship pattern as ExecuteDDLAsync (open only if closed, close only
    /// what we opened). Returns the first row's values, or null for no rows.
    /// </summary>
    private async Task<object?[]?> QuerySingleRowAsync(string sql, params (string Name, object Value)[] parameters)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = name;
                p.Value = value;
                cmd.Parameters.Add(p);
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            var values = new object?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            return values;
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
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
