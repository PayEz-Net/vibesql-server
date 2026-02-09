-- ========================================
-- Vibe Database - Agent Auth Performance Indexes
-- Migration: 20260119_AgentAuthIndexes
-- Date: 2026-01-19
-- Purpose: Add specialized indexes for agent authentication queries
-- ========================================

-- UP MIGRATION
-- ========================================

-- 1. Agent Profiles by ID (Primary lookup during token generation)
-- Direct index on JSONB field eliminates in-memory filtering
CREATE INDEX IF NOT EXISTS idx_agent_profiles_id
ON vibe.documents USING btree (((data->>'id')::integer))
WHERE client_id = 1
  AND collection = 'agent_mail'
  AND table_name = 'agent_profiles'
  AND deleted_at IS NULL;

COMMENT ON INDEX vibe.idx_agent_profiles_id IS
'Fast lookup of agent profiles by ID during token generation. Partial index filtered to agent_mail collection only.';

-- 2. Agent Capabilities by Agent ID (Scope validation)
-- Used to load capabilities for a specific agent
CREATE INDEX IF NOT EXISTS idx_agent_capabilities_agent_id
ON vibe.documents USING btree (((data->>'agent_id')::integer))
WHERE client_id = 1
  AND collection = 'agent_mail'
  AND table_name = 'agent_capabilities'
  AND deleted_at IS NULL;

COMMENT ON INDEX vibe.idx_agent_capabilities_agent_id IS
'Fast lookup of agent capabilities by agent_id for scope validation during authentication.';

-- 3. Agent Teams by ID (Team metadata lookup)
-- Used to load team information during token generation
CREATE INDEX IF NOT EXISTS idx_agent_teams_id
ON vibe.documents USING btree (((data->>'id')::integer))
WHERE client_id = 1
  AND collection = 'agent_mail'
  AND table_name = 'agent_teams'
  AND deleted_at IS NULL;

COMMENT ON INDEX vibe.idx_agent_teams_id IS
'Fast lookup of agent teams by ID during token generation.';

-- 4. Agent Profiles by Username (OAuth authentication)
-- Critical for password grant flow where username is provided
CREATE INDEX IF NOT EXISTS idx_agent_profiles_username
ON vibe.documents USING btree ((data->>'username'))
WHERE client_id = 1
  AND collection = 'agent_mail'
  AND table_name = 'agent_profiles'
  AND deleted_at IS NULL;

COMMENT ON INDEX vibe.idx_agent_profiles_username IS
'Fast lookup of agent profiles by username during OAuth password grant authentication.';

-- 5. Agent Active Status (IsActive filtering)
-- Enables fast filtering on active agents without reading full JSON
CREATE INDEX IF NOT EXISTS idx_agent_profiles_active
ON vibe.documents USING btree (client_id, collection, table_name, ((data->>'is_active')::boolean))
WHERE deleted_at IS NULL
  AND collection = 'agent_mail'
  AND table_name = 'agent_profiles';

COMMENT ON INDEX vibe.idx_agent_profiles_active IS
'Fast filtering on agent active status without deserializing JSONB data.';

-- 6. Covering Index for Agent Profiles (Index-only scan optimization)
-- PostgreSQL 11+ feature - includes data column to avoid heap lookups
CREATE INDEX IF NOT EXISTS idx_agent_profiles_covering
ON vibe.documents USING btree (client_id, collection, table_name, document_id)
INCLUDE (data)
WHERE deleted_at IS NULL
  AND collection = 'agent_mail'
  AND table_name = 'agent_profiles';

COMMENT ON INDEX vibe.idx_agent_profiles_covering IS
'Index-only scan for agent profiles - includes data column to avoid table heap access (PostgreSQL 11+).';

-- 7. Increase statistics for query planner optimization
-- Helps PostgreSQL choose better indexes for filtered queries
ALTER TABLE vibe.documents ALTER COLUMN collection SET STATISTICS 1000;
ALTER TABLE vibe.documents ALTER COLUMN table_name SET STATISTICS 1000;

-- 8. Rebuild statistics with new targets
ANALYZE vibe.documents;

-- Log migration completion
DO $$
BEGIN
    RAISE NOTICE 'Migration 20260119_AgentAuthIndexes completed successfully';
    RAISE NOTICE 'Created 6 specialized indexes for agent authentication';
    RAISE NOTICE 'Updated statistics targets for collection and table_name columns';
END $$;


-- DOWN MIGRATION (Rollback)
-- ========================================

-- Uncomment below to rollback this migration

/*
-- Drop specialized indexes
DROP INDEX IF EXISTS vibe.idx_agent_profiles_id;
DROP INDEX IF EXISTS vibe.idx_agent_capabilities_agent_id;
DROP INDEX IF EXISTS vibe.idx_agent_teams_id;
DROP INDEX IF EXISTS vibe.idx_agent_profiles_username;
DROP INDEX IF EXISTS vibe.idx_agent_profiles_active;
DROP INDEX IF EXISTS vibe.idx_agent_profiles_covering;

-- Restore default statistics
ALTER TABLE vibe.documents ALTER COLUMN collection SET STATISTICS DEFAULT;
ALTER TABLE vibe.documents ALTER COLUMN table_name SET STATISTICS DEFAULT;

-- Rebuild statistics
ANALYZE vibe.documents;

RAISE NOTICE 'Migration 20260119_AgentAuthIndexes rolled back successfully';
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
  AND indexname LIKE 'idx_agent%'
ORDER BY indexname;

-- Check statistics targets
SELECT
    s.attname,
    s.n_distinct,
    a.attstattarget
FROM pg_stats s
JOIN pg_attribute a ON (s.schemaname || '.' || s.tablename)::regclass = a.attrelid AND s.attname = a.attname
WHERE s.schemaname = 'vibe'
  AND s.tablename = 'documents'
  AND s.attname IN ('collection', 'table_name');

-- Test query performance (should use new indexes)
EXPLAIN ANALYZE
SELECT document_id, data
FROM vibe.documents
WHERE client_id = 1
  AND collection = 'agent_mail'
  AND table_name = 'agent_profiles'
  AND (data->>'id')::integer = 1
  AND deleted_at IS NULL;


-- PERFORMANCE EXPECTATIONS
-- ========================================
/*
Before: Full table scan or suboptimal index scan with in-memory JSONB filtering
After:  Direct index lookup on JSONB expression
Expected improvement: 10-100x faster for single agent lookups

Query patterns optimized:
1. GetAgentProfileAsync(id) - Direct ID lookup
2. GetAgentCapabilitiesAsync(agentId) - Capabilities by agent_id
3. GetAgentTeamAsync(teamId) - Team by ID
4. FindByUsername(username) - OAuth authentication
5. IsAgentActiveAsync(id) - Active status check

Index sizes (estimated):
- idx_agent_profiles_id: ~8KB per 1000 agents
- idx_agent_capabilities_agent_id: ~8KB per 1000 capabilities
- idx_agent_teams_id: ~8KB per 1000 teams
- idx_agent_profiles_username: ~16KB per 1000 agents (string index)
- idx_agent_profiles_active: ~12KB per 1000 agents
- idx_agent_profiles_covering: ~512KB per 1000 agents (includes JSONB data)

Total overhead: ~500KB per 1000 agents (negligible for performance gain)
*/


-- MAINTENANCE NOTES
-- ========================================
/*
1. Run ANALYZE after bulk agent data imports
2. Monitor index usage with pg_stat_user_indexes
3. Consider REINDEX if index bloat exceeds 30%
4. Covering index (idx_agent_profiles_covering) uses most space but provides best performance
5. If agent data grows beyond 100K records, consider partitioning by client_id
*/
