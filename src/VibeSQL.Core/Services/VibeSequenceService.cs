using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Data;
using VibeSQL.Core.Interfaces;

namespace VibeSQL.Core.Services;

/// <summary>
/// Distributed sequence ID generation using block allocation.
///
/// Each node gets pre-allocated blocks of IDs from the database.
/// This allows multiple nodes to generate IDs without coordination,
/// supporting multi-master replication and high-availability setups.
///
/// Block allocation is atomic via PostgreSQL function.
/// In-memory caching minimizes DB round-trips for normal operation.
///
/// NOTE: Registered as Singleton for in-memory block cache.
/// Uses IServiceScopeFactory to create scoped DbContext when needed.
///
/// ARCHITECTURE NOTE: This could be split into:
/// - Singleton cache manager (in-memory only)
/// - Scoped repository for DB operations
/// But the current design is simpler and works correctly.
/// </summary>
public partial class VibeSequenceService : IVibeSequenceService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VibeSequenceService> _logger;
    private readonly string _nodeId;
    private readonly int _blockSize;
    private readonly int _preAllocateThreshold;

    // In-memory cache of active blocks per (client, collection, table)
    private readonly ConcurrentDictionary<string, SequenceBlock> _activeBlocks = new();

    // Lock objects per sequence to prevent concurrent block allocation
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _allocationLocks = new();

    // Track pending pre-allocations to avoid duplicate requests
    private readonly ConcurrentDictionary<string, Task> _pendingAllocations = new();

    // Node ID validation: alphanumeric, hyphens, underscores only (QAPert security review)
    [GeneratedRegex(@"^[a-zA-Z0-9_-]{1,100}$")]
    private static partial Regex NodeIdValidationRegex();

    public VibeSequenceService(
        IServiceScopeFactory scopeFactory,
        ILogger<VibeSequenceService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        // Node ID from config or environment (for multi-master identification)
        var nodeId = configuration["Vibe:NodeId"]
            ?? Environment.GetEnvironmentVariable("VIBE_NODE_ID")
            ?? "primary";

        // Validate node_id format (QAPert: prevent injection via malformed node IDs)
        if (!NodeIdValidationRegex().IsMatch(nodeId))
        {
            throw new ArgumentException(
                $"Invalid NodeId format: '{nodeId}'. Must be 1-100 alphanumeric characters, hyphens, or underscores.",
                nameof(configuration));
        }
        _nodeId = nodeId;

        // Block size - how many IDs per allocation (default 10,000)
        _blockSize = configuration.GetValue<int>("Vibe:SequenceBlockSize", 10000);

        // When to pre-allocate next block (% remaining, default 20%)
        _preAllocateThreshold = configuration.GetValue<int>("Vibe:SequencePreAllocateThreshold", 20);

        _logger.LogInformation("VibeSequenceService initialized: NodeId={NodeId}, BlockSize={BlockSize}, PreAllocateThreshold={Threshold}%",
            _nodeId, _blockSize, _preAllocateThreshold);
    }

    /// <summary>
    /// Gets the next sequence ID for a table.
    /// Fast path: increment in-memory counter.
    /// Slow path: allocate new block from DB.
    /// </summary>
    public async Task<long> GetNextIdAsync(int clientId, string collection, string tableName)
    {
        var key = GetCacheKey(clientId, collection, tableName);

        // Try to get from cached block
        if (_activeBlocks.TryGetValue(key, out var block))
        {
            var nextId = block.TryGetNextId();
            if (nextId.HasValue)
            {
                // Check if we should pre-allocate
                TriggerPreAllocationIfNeeded(clientId, collection, tableName, block);
                return nextId.Value;
            }
        }

        // No block or block exhausted - need to allocate
        return await AllocateAndGetNextIdAsync(clientId, collection, tableName);
    }

    /// <summary>
    /// Pre-allocates a new block of IDs.
    /// </summary>
    public async Task<(long Start, long End)> AllocateBlockAsync(int clientId, string collection, string tableName)
    {
        var key = GetCacheKey(clientId, collection, tableName);
        var lockObj = _allocationLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await lockObj.WaitAsync();
        try
        {
            // Double-check if another thread already allocated
            if (_activeBlocks.TryGetValue(key, out var existingBlock) && !existingBlock.IsExhausted)
            {
                return (existingBlock.BlockStart, existingBlock.BlockEnd);
            }

            // Create a scope for DbContext access
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VibeDbContext>();

            // Call the database function to allocate atomically
            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT block_start, block_end
                FROM vibe.allocate_sequence_block(@client_id, @collection, @table_name, @node_id, @block_size)";

            var clientIdParam = command.CreateParameter();
            clientIdParam.ParameterName = "@client_id";
            clientIdParam.Value = clientId;
            command.Parameters.Add(clientIdParam);

            var collectionParam = command.CreateParameter();
            collectionParam.ParameterName = "@collection";
            collectionParam.Value = collection;
            command.Parameters.Add(collectionParam);

            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@table_name";
            tableParam.Value = tableName;
            command.Parameters.Add(tableParam);

            var nodeParam = command.CreateParameter();
            nodeParam.ParameterName = "@node_id";
            nodeParam.Value = _nodeId;
            command.Parameters.Add(nodeParam);

            var blockSizeParam = command.CreateParameter();
            blockSizeParam.ParameterName = "@block_size";
            blockSizeParam.Value = _blockSize;
            command.Parameters.Add(blockSizeParam);

            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException($"Failed to allocate sequence block for {collection}.{tableName}");
            }

            var start = reader.GetInt64(0);
            var end = reader.GetInt64(1);

            // Update in-memory cache
            var newBlock = new SequenceBlock(start, end, _nodeId);
            _activeBlocks[key] = newBlock;

            _logger.LogInformation("VIBE_SEQ_BLOCK_ALLOCATED: Client={ClientId}, Collection={Collection}, Table={Table}, Node={Node}, Range={Start}-{End}",
                clientId, collection, tableName, _nodeId, start, end);

            return (start, end);
        }
        finally
        {
            lockObj.Release();
        }
    }

    /// <summary>
    /// Gets the current block status for monitoring.
    /// </summary>
    public async Task<SequenceBlockStatus?> GetBlockStatusAsync(int clientId, string collection, string tableName)
    {
        var key = GetCacheKey(clientId, collection, tableName);

        // Check in-memory first
        if (_activeBlocks.TryGetValue(key, out var block))
        {
            return new SequenceBlockStatus
            {
                BlockStart = block.BlockStart,
                BlockEnd = block.BlockEnd,
                CurrentValue = block.CurrentValue,
                NodeId = block.NodeId,
                AllocatedAt = block.AllocatedAt
            };
        }

        // Check database for this node's active block
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VibeDbContext>();

        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT block_start, block_end, current_value, node_id, allocated_at
            FROM vibe.sequence_blocks
            WHERE client_id = @client_id
              AND collection = @collection
              AND table_name = @table_name
              AND node_id = @node_id
              AND is_exhausted = FALSE
            LIMIT 1";

        var clientIdParam = command.CreateParameter();
        clientIdParam.ParameterName = "@client_id";
        clientIdParam.Value = clientId;
        command.Parameters.Add(clientIdParam);

        var collectionParam = command.CreateParameter();
        collectionParam.ParameterName = "@collection";
        collectionParam.Value = collection;
        command.Parameters.Add(collectionParam);

        var tableParam = command.CreateParameter();
        tableParam.ParameterName = "@table_name";
        tableParam.Value = tableName;
        command.Parameters.Add(tableParam);

        var nodeParam = command.CreateParameter();
        nodeParam.ParameterName = "@node_id";
        nodeParam.Value = _nodeId;
        command.Parameters.Add(nodeParam);

        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new SequenceBlockStatus
        {
            BlockStart = reader.GetInt64(0),
            BlockEnd = reader.GetInt64(1),
            CurrentValue = reader.GetInt64(2),
            NodeId = reader.GetString(3),
            AllocatedAt = reader.GetDateTime(4)
        };
    }

    /// <summary>
    /// Allocates a new block and returns the first ID.
    /// </summary>
    private async Task<long> AllocateAndGetNextIdAsync(int clientId, string collection, string tableName)
    {
        await AllocateBlockAsync(clientId, collection, tableName);

        var key = GetCacheKey(clientId, collection, tableName);
        if (_activeBlocks.TryGetValue(key, out var block))
        {
            var nextId = block.TryGetNextId();
            if (nextId.HasValue)
                return nextId.Value;
        }

        throw new InvalidOperationException($"Failed to get next ID after block allocation for {collection}.{tableName}");
    }

    /// <summary>
    /// Triggers async pre-allocation if block is running low.
    /// </summary>
    private void TriggerPreAllocationIfNeeded(int clientId, string collection, string tableName, SequenceBlock block)
    {
        var percentRemaining = block.PercentRemaining;
        if (percentRemaining > _preAllocateThreshold)
            return;

        var key = GetCacheKey(clientId, collection, tableName);

        // Check if pre-allocation already in progress
        if (_pendingAllocations.ContainsKey(key))
            return;

        // Start async pre-allocation
        var task = Task.Run(async () =>
        {
            try
            {
                _logger.LogDebug("VIBE_SEQ_PREALLOC: Starting pre-allocation for {Collection}.{Table} ({Remaining}% remaining)",
                    collection, tableName, percentRemaining);

                await AllocateBlockAsync(clientId, collection, tableName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VIBE_SEQ_PREALLOC_FAILED: Pre-allocation failed for {Collection}.{Table}",
                    collection, tableName);
            }
            finally
            {
                _pendingAllocations.TryRemove(key, out _);
            }
        });

        _pendingAllocations.TryAdd(key, task);
    }

    private static string GetCacheKey(int clientId, string collection, string tableName)
        => $"{clientId}:{collection}:{tableName}";

    /// <summary>
    /// Thread-safe in-memory sequence block.
    /// </summary>
    private class SequenceBlock
    {
        private long _currentValue;

        /// <summary>
        /// First ID in this block (inclusive).
        /// </summary>
        public long BlockStart { get; }

        /// <summary>
        /// Last ID in this block (exclusive).
        /// Block range is [BlockStart, BlockEnd), so valid IDs are BlockStart through BlockEnd-1.
        /// </summary>
        /// <remarks>
        /// QAPert note: The range math uses half-open interval [start, end).
        /// With BlockSize=10000:
        /// - Block 1: start=1, end=10001 -> IDs 1-10000 (10000 IDs)
        /// - Block 2: start=10001, end=20001 -> IDs 10001-20000 (10000 IDs)
        /// </remarks>
        public long BlockEnd { get; }

        public string NodeId { get; }
        public DateTime AllocatedAt { get; }

        public long CurrentValue => Interlocked.Read(ref _currentValue);
        public bool IsExhausted => CurrentValue >= BlockEnd;
        public double PercentRemaining => BlockEnd > BlockStart
            ? (double)(BlockEnd - CurrentValue) / (BlockEnd - BlockStart) * 100
            : 0;

        public SequenceBlock(long start, long end, string nodeId)
        {
            BlockStart = start;
            BlockEnd = end;
            _currentValue = start;
            NodeId = nodeId;
            AllocatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Atomically get next ID if available.
        /// Returns null if block is exhausted.
        /// </summary>
        public long? TryGetNextId()
        {
            // Atomically increment and check if we exceeded the block
            // We pre-increment, so if result is >= BlockEnd, the block is exhausted
            var next = Interlocked.Increment(ref _currentValue) - 1;
            if (next >= BlockEnd)
                return null;
            return next;
        }
    }
}
