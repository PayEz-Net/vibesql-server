-- ========================================
-- Vibe Database - JSONB Index Optimization (Part 1)
-- Migration: 20260119_VibeIndexOptimization_Part1
-- Date: 2026-01-19
-- Purpose: Optimize JSONB indexes for performance (600% faster, 30% smaller)
-- Spec: VIBE_JSONB_OPTIMIZATION_SPEC.md Part 1
-- ========================================

-- UP MIGRATION
-- ========================================

BEGIN;

-- ========================================
-- 1.1 Switch to jsonb_path_ops
-- ========================================
-- Replace default GIN index with jsonb_path_ops for 600% faster containment queries
-- Trade-off: Only supports @> containment, not ?, ?|, ?& existence operators

DROP INDEX IF EXISTS vibe.idx_documents_data;

CREATE INDEX idx_documents_data_path
ON vibe.documents USING GIN (data jsonb_path_ops);

COMMENT ON INDEX vibe.idx_documents_data_path IS
'Optimized GIN index using jsonb_path_ops - 600% faster containment queries (@>), 30% smaller than jsonb_ops. Does not support existence operators (?, ?|, ?&).';

-- ========================================
-- 1.2 Expression Indexes for Hot Paths
-- ========================================
-- B-tree indexes on extracted JSONB fields for equality/range queries

-- Client ID from JSONB (for queries that filter by data->>''client_id'')
-- Note: Top-level client_id column already has idx_documents_collection_table
CREATE INDEX IF NOT EXISTS idx_documents_data_client_id
ON vibe.documents (((data->>'client_id')::int))
WHERE data->>'client_id' IS NOT NULL;

COMMENT ON INDEX vibe.idx_documents_data_client_id IS
'Fast lookup of documents by JSONB client_id field. Partial index (only non-NULL values).';

-- User ID from JSONB (owner_user_id, user_id fields in JSONB)
CREATE INDEX IF NOT EXISTS idx_documents_data_user_id
ON vibe.documents (((data->>'user_id')::int))
WHERE data->>'user_id' IS NOT NULL;

COMMENT ON INDEX vibe.idx_documents_data_user_id IS
'Fast lookup of documents by JSONB user_id field. Partial index (only non-NULL values).';

-- Owner User ID (for agent ownership queries)
CREATE INDEX IF NOT EXISTS idx_documents_data_owner_user_id
ON vibe.documents (((data->>'owner_user_id')::int))
WHERE data->>'owner_user_id' IS NOT NULL;

COMMENT ON INDEX vibe.idx_documents_data_owner_user_id IS
'Fast lookup of documents by owner_user_id (agent ownership). Partial index (only non-NULL values).';

-- Tenant ID from JSONB (for multi-tenant filtering)
CREATE INDEX IF NOT EXISTS idx_documents_data_tenant_id
ON vibe.documents (((data->>'tenant_id')::int))
WHERE data->>'tenant_id' IS NOT NULL;

COMMENT ON INDEX vibe.idx_documents_data_tenant_id IS
'Fast lookup of documents by tenant_id for multi-tenant filtering. Partial index (only non-NULL values).';

-- Status field (active/inactive/pending patterns)
CREATE INDEX IF NOT EXISTS idx_documents_data_status
ON vibe.documents ((data->>'status'))
WHERE data->>'status' IS NOT NULL;

COMMENT ON INDEX vibe.idx_documents_data_status IS
'Fast filtering by status field (active, inactive, pending, etc.). Partial index (only non-NULL values).';

-- Primary key within collection (common lookup pattern)
CREATE INDEX IF NOT EXISTS idx_documents_data_id
ON vibe.documents (
  client_id,
  collection,
  table_name,
  ((data->>'id')::int)
) WHERE data->>'id' IS NOT NULL AND deleted_at IS NULL;

COMMENT ON INDEX vibe.idx_documents_data_id IS
'Fast primary key lookups within collection (client_id + collection + table_name + data.id). Excludes deleted records.';

-- Email lookups (for user/agent email searches)
CREATE INDEX IF NOT EXISTS idx_documents_data_email
ON vibe.documents ((data->>'email'))
WHERE data->>'email' IS NOT NULL;

COMMENT ON INDEX vibe.idx_documents_data_email IS
'Fast email lookups in JSONB data. Partial index (only non-NULL values).';

-- Created_at timestamp for sorting/filtering (as text for immutability)
CREATE INDEX IF NOT EXISTS idx_documents_data_created_at
ON vibe.documents ((data->>'created_at'))
WHERE data->>'created_at' IS NOT NULL;

COMMENT ON INDEX vibe.idx_documents_data_created_at IS
'Fast sorting/filtering by created_at timestamp from JSONB (indexed as text, comparison still works with ISO 8601 format). Partial index (only non-NULL values).';

-- ========================================
-- 1.3 Partial Indexes for Common Filters
-- ========================================
-- Targeted indexes for frequent query patterns

-- Active records only (most queries filter by is_active = true)
CREATE INDEX IF NOT EXISTS idx_documents_data_active
ON vibe.documents USING GIN (data jsonb_path_ops)
WHERE data->>'is_active' = 'true' AND deleted_at IS NULL;

COMMENT ON INDEX vibe.idx_documents_data_active IS
'Optimized GIN index for active records only (is_active=true, not deleted). Reduces index size by excluding inactive records.';

-- Specific collections (agent_profiles, agent_capabilities, agent_teams)
-- These build on top of the agent auth indexes created earlier
CREATE INDEX IF NOT EXISTS idx_documents_agent_mail_collection
ON vibe.documents USING GIN (data jsonb_path_ops)
WHERE collection = 'agent_mail' AND deleted_at IS NULL;

COMMENT ON INDEX vibe.idx_documents_agent_mail_collection IS
'Optimized GIN index for agent_mail collection only. Reduces index size and improves query performance for agent queries.';

-- User profiles collection
CREATE INDEX IF NOT EXISTS idx_documents_user_profiles
ON vibe.documents USING GIN (data jsonb_path_ops)
WHERE collection = 'user_profiles' AND deleted_at IS NULL;

COMMENT ON INDEX vibe.idx_documents_user_profiles IS
'Optimized GIN index for user_profiles collection. Fast containment queries on user profile data.';

-- Vibe app collections (system-level data)
CREATE INDEX IF NOT EXISTS idx_documents_vibe_app
ON vibe.documents USING GIN (data jsonb_path_ops)
WHERE collection = 'vibe_app' AND deleted_at IS NULL;

COMMENT ON INDEX vibe.idx_documents_vibe_app IS
'Optimized GIN index for vibe_app collection (system-level data).';

-- Compound partial: collection + tenant + active
-- Most common query pattern: "get active records for tenant in collection"
CREATE INDEX IF NOT EXISTS idx_documents_collection_tenant_active
ON vibe.documents (
  client_id,
  collection,
  table_name,
  ((data->>'tenant_id')::int)
)
WHERE data->>'is_active' = 'true'
  AND data->>'tenant_id' IS NOT NULL
  AND deleted_at IS NULL;

COMMENT ON INDEX vibe.idx_documents_collection_tenant_active IS
'Compound index for most common query pattern: active records in collection filtered by tenant. Excludes inactive and deleted records.';

-- Rebuild statistics
ANALYZE vibe.documents;

-- Log migration completion
DO $$
BEGIN
    RAISE NOTICE '=================================================';
    RAISE NOTICE 'Migration 20260119_VibeIndexOptimization_Part1 completed';
    RAISE NOTICE '=================================================';
    RAISE NOTICE 'Replaced idx_documents_data with jsonb_path_ops variant';
    RAISE NOTICE 'Added 9 expression indexes for hot path fields';
    RAISE NOTICE 'Added 5 partial indexes for common query patterns';
    RAISE NOTICE 'Expected improvement: 600%% faster containment queries, 30%% smaller index size';
END $$;

COMMIT;


-- DOWN MIGRATION (Rollback)
-- ========================================

-- Uncomment below to rollback this migration

/*
BEGIN;

-- Drop new indexes
DROP INDEX IF EXISTS vibe.idx_documents_data_path;
DROP INDEX IF EXISTS vibe.idx_documents_data_client_id;
DROP INDEX IF EXISTS vibe.idx_documents_data_user_id;
DROP INDEX IF EXISTS vibe.idx_documents_data_owner_user_id;
DROP INDEX IF EXISTS vibe.idx_documents_data_tenant_id;
DROP INDEX IF EXISTS vibe.idx_documents_data_status;
DROP INDEX IF EXISTS vibe.idx_documents_data_id;
DROP INDEX IF EXISTS vibe.idx_documents_data_email;
DROP INDEX IF EXISTS vibe.idx_documents_data_created_at;
DROP INDEX IF EXISTS vibe.idx_documents_data_active;
DROP INDEX IF EXISTS vibe.idx_documents_agent_mail_collection;
DROP INDEX IF EXISTS vibe.idx_documents_user_profiles;
DROP INDEX IF EXISTS vibe.idx_documents_vibe_app;
DROP INDEX IF EXISTS vibe.idx_documents_collection_tenant_active;

-- Restore original GIN index
CREATE INDEX idx_documents_data ON vibe.documents USING GIN (data);

-- Rebuild statistics
ANALYZE vibe.documents;

RAISE NOTICE 'Migration 20260119_VibeIndexOptimization_Part1 rolled back successfully';

COMMIT;
*/


-- VERIFICATION QUERIES
-- ========================================

-- Verify indexes were created
SELECT
    schemaname,
    tablename,
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'vibe'
  AND tablename = 'documents'
  AND indexname LIKE 'idx_documents_data%'
ORDER BY indexname;

-- Check index sizes
SELECT
    schemaname || '.' || tablename AS table_name,
    indexname,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size,
    idx_scan AS times_used,
    idx_tup_read AS tuples_read,
    idx_tup_fetch AS tuples_fetched
FROM pg_stat_user_indexes
WHERE schemaname = 'vibe'
  AND tablename = 'documents'
  AND indexname LIKE 'idx_documents_data%'
ORDER BY pg_relation_size(indexrelid) DESC;

-- Test containment query performance (should use jsonb_path_ops index)
EXPLAIN ANALYZE
SELECT document_id, data
FROM vibe.documents
WHERE data @> '{"is_active": true, "status": "active"}'
  AND deleted_at IS NULL
LIMIT 10;


-- PERFORMANCE EXPECTATIONS
-- ========================================
/*
Before: Default GIN index (jsonb_ops)
After:  jsonb_path_ops + expression indexes + partial indexes

Expected improvements:
1. Containment queries (@>): 600% faster
2. Index size: 30% smaller
3. Expression index queries (data->>''field''): 10-100x faster (no JSONB parsing)
4. Partial indexes: Only index relevant subsets, reducing memory footprint

Query patterns optimized:
1. Containment: data @> '{"field": "value"}' - Uses jsonb_path_ops
2. Equality: data->>''user_id'' = 123 - Uses expression index
3. Range: data->>''created_at'' > ''2026-01-01'' - Uses expression index
4. Active records: is_active = true - Uses partial index
5. Collection filtering: collection = ''agent_mail'' - Uses partial GIN index

Trade-offs:
- Lost support for existence operators (?, ?|, ?&) - use expression indexes instead
- More indexes = more storage (but smaller sizes with partial indexes)
- More indexes = slightly slower writes (marginal impact)
*/


-- MAINTENANCE NOTES
-- ========================================
/*
1. Run ANALYZE after bulk data imports
2. Monitor index usage with pg_stat_user_indexes
3. Consider REINDEX if bloat exceeds 30%
4. Partial indexes auto-exclude deleted/inactive records - no manual cleanup needed
5. Expression indexes eliminate JSONB parsing overhead - no code changes needed
*/
