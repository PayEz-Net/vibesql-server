namespace VibeSQL.Core.Interfaces;

/// <summary>
/// Service for distributed sequence ID generation using block allocation.
/// Supports multi-master replication by pre-allocating ID blocks per node.
///
/// Each node gets non-overlapping ranges of IDs:
/// - Node A: 1-10000, 20001-30000, ...
/// - Node B: 10001-20000, 30001-40000, ...
///
/// This enables active-active replication without ID collisions.
/// </summary>
public interface IVibeSequenceService
{
    /// <summary>
    /// Gets the next sequence ID for a table.
    /// Fast path: returns from in-memory block cache.
    /// Slow path: allocates new block from database if current block exhausted.
    /// </summary>
    /// <param name="clientId">The client ID</param>
    /// <param name="collection">The collection name</param>
    /// <param name="tableName">The table name</param>
    /// <returns>Next available ID (guaranteed unique across all nodes)</returns>
    Task<long> GetNextIdAsync(int clientId, string collection, string tableName);

    /// <summary>
    /// Pre-allocates a new block of IDs for this node.
    /// Call this proactively when current block is running low.
    /// </summary>
    /// <param name="clientId">The client ID</param>
    /// <param name="collection">The collection name</param>
    /// <param name="tableName">The table name</param>
    /// <returns>The allocated block range (start inclusive, end exclusive)</returns>
    Task<(long Start, long End)> AllocateBlockAsync(int clientId, string collection, string tableName);

    /// <summary>
    /// Gets the current block status for monitoring/diagnostics.
    /// </summary>
    Task<SequenceBlockStatus?> GetBlockStatusAsync(int clientId, string collection, string tableName);
}

/// <summary>
/// Status of a sequence block for monitoring
/// </summary>
public class SequenceBlockStatus
{
    public long BlockStart { get; set; }
    public long BlockEnd { get; set; }
    public long CurrentValue { get; set; }

    /// <summary>
    /// IDs remaining in this block before exhaustion
    /// </summary>
    public int Remaining => (int)(BlockEnd - CurrentValue);

    /// <summary>
    /// Percentage of block consumed (0-100)
    /// </summary>
    public double PercentUsed => BlockEnd > BlockStart
        ? (double)(CurrentValue - BlockStart) / (BlockEnd - BlockStart) * 100
        : 0;

    /// <summary>
    /// Node identifier that owns this block
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// When this block was allocated from the database
    /// </summary>
    public DateTime AllocatedAt { get; set; }
}
