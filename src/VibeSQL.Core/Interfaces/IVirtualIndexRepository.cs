using VibeSQL.Core.Entities;

namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Repository for virtual index operations.
/// Handles index metadata CRUD and partition queries.
/// </summary>
public interface IVirtualIndexRepository
{
    /// <summary>
    /// Get all active indexes for a client's collection
    /// </summary>
    Task<List<VirtualIndex>> GetActiveIndexesAsync(int clientId, string collection);

    /// <summary>
    /// Get a specific index by name
    /// </summary>
    Task<VirtualIndex?> GetByNameAsync(int clientId, string collection, string indexName);

    /// <summary>
    /// Get index by ID
    /// </summary>
    Task<VirtualIndex?> GetByIdAsync(int virtualIndexId);

    /// <summary>
    /// Create a new virtual index
    /// </summary>
    Task<VirtualIndex> CreateAsync(VirtualIndex virtualIndex);

    /// <summary>
    /// Update an existing virtual index (for soft delete)
    /// </summary>
    Task<bool> UpdateAsync(VirtualIndex virtualIndex);

    /// <summary>
    /// Get count of active indexes for a client
    /// </summary>
    Task<int> GetActiveIndexCountAsync(int clientId);

    /// <summary>
    /// Get partition name for a client
    /// </summary>
    Task<string?> GetPartitionNameAsync(int clientId);

    /// <summary>
    /// Get partition info (name, tier level, is shared)
    /// </summary>
    Task<PartitionInfo?> GetPartitionInfoAsync(int clientId);

    /// <summary>
    /// Get tier limit for virtual indexes
    /// </summary>
    Task<int> GetTierLimitAsync(int clientId);

    /// <summary>
    /// Execute DDL command (CREATE/DROP INDEX)
    /// </summary>
    Task ExecuteDDLAsync(string ddl, int timeoutSeconds = 300);
}

/// <summary>
/// Partition information for index creation
/// </summary>
public class PartitionInfo
{
    public string PartitionName { get; set; } = string.Empty;
    public int TierLevel { get; set; }
    public bool IsShared { get; set; }
}
