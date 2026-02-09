using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for Vibe collection schema operations.
/// </summary>
public class VibeSchemaRepository : IVibeSchemaRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<VibeSchemaRepository> _logger;

    public VibeSchemaRepository(VibeDbContext context, ILogger<VibeSchemaRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<VibeCollectionSchema>> GetAllAsync(int clientId)
    {
        return await _context.CollectionSchemas
            .Where(s => s.ClientId == clientId && s.IsActive)
            .OrderBy(s => s.Collection)
            .ToListAsync();
    }

    public async Task<VibeCollectionSchema?> GetActiveSchemaAsync(int clientId, string collection)
    {
        return await _context.CollectionSchemas
            .Where(s => s.ClientId == clientId
                     && s.Collection == collection
                     && s.IsActive)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync();
    }

    public async Task<VibeCollectionSchema?> GetByIdAsync(int schemaId)
    {
        return await _context.CollectionSchemas.FindAsync(schemaId);
    }

    public async Task<List<VibeCollectionSchema>> GetVersionsAsync(int clientId, string collection)
    {
        return await _context.CollectionSchemas
            .Where(s => s.ClientId == clientId && s.Collection == collection)
            .OrderByDescending(s => s.Version)
            .ToListAsync();
    }

    public async Task<VibeCollectionSchema> CreateAsync(VibeCollectionSchema schema)
    {
        _context.CollectionSchemas.Add(schema);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_SCHEMA_CREATED: Collection={Collection}, Version={Version}, ClientId={ClientId}",
            schema.Collection, schema.Version, schema.ClientId);

        return schema;
    }

    public async Task<bool> UpdateAsync(VibeCollectionSchema schema)
    {
        schema.UpdatedAt = DateTimeOffset.UtcNow;
        _context.CollectionSchemas.Update(schema);
        var result = await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_SCHEMA_UPDATED: Collection={Collection}, Version={Version}", schema.Collection, schema.Version);

        return result > 0;
    }

    public async Task<bool> DeleteAsync(int schemaId)
    {
        var schema = await _context.CollectionSchemas.FindAsync(schemaId);
        if (schema == null) return false;

        _context.CollectionSchemas.Remove(schema);
        var result = await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_SCHEMA_DELETED: SchemaId={SchemaId}", schemaId);

        return result > 0;
    }

    public async Task<int> DeleteByCollectionAsync(int clientId, string collection)
    {
        var schemas = await _context.CollectionSchemas
            .Where(s => s.ClientId == clientId && s.Collection == collection)
            .ToListAsync();

        if (schemas.Count == 0) return 0;

        _context.CollectionSchemas.RemoveRange(schemas);
        await _context.SaveChangesAsync();

        _logger.LogInformation("VIBE_SCHEMA_DELETED: Collection={Collection}, ClientId={ClientId}, VersionsDeleted={Count}",
            collection, clientId, schemas.Count);

        return schemas.Count;
    }

    public async Task DeactivateVersionsAsync(int clientId, string collection)
    {
        var schemas = await _context.CollectionSchemas
            .Where(s => s.ClientId == clientId
                     && s.Collection == collection
                     && s.IsActive)
            .ToListAsync();

        foreach (var schema in schemas)
        {
            schema.IsActive = false;
            schema.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogDebug("VIBE_SCHEMA_DEACTIVATED: Collection={Collection}, Count={Count}", collection, schemas.Count);
    }

    public async Task<bool> CollectionExistsAsync(int clientId, string collection)
    {
        return await _context.CollectionSchemas
            .AnyAsync(s => s.ClientId == clientId && s.Collection == collection);
    }

    public async Task CreateSequenceAsync(int clientId, string collection, long startValue = 1)
    {
        var seqName = GetSequenceName(clientId, collection);
        var sql = $"CREATE SEQUENCE IF NOT EXISTS {seqName} START WITH {startValue} INCREMENT BY 1";

        await _context.Database.ExecuteSqlRawAsync(sql);

        _logger.LogInformation("VIBE_SEQUENCE_CREATED: {SeqName} at {StartValue}", seqName, startValue);
    }

    public async Task DropSequenceAsync(int clientId, string collection)
    {
        var seqName = GetSequenceName(clientId, collection);
        var sql = $"DROP SEQUENCE IF EXISTS {seqName}";

        await _context.Database.ExecuteSqlRawAsync(sql);

        _logger.LogInformation("VIBE_SEQUENCE_DROPPED: {SeqName}", seqName);
    }

    public string GetSequenceName(int clientId, string collection)
    {
        var safeCollection = collection.Replace("\"", "").Replace("'", "").Replace("-", "_");
        return $"vibe.seq_{clientId}_{safeCollection}";
    }

    public async Task<int> GetMaxVersionAsync(int clientId, string collection)
    {
        return await _context.CollectionSchemas
            .Where(s => s.ClientId == clientId && s.Collection == collection)
            .MaxAsync(s => (int?)s.Version) ?? 0;
    }
}
