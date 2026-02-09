-- ========================================
-- Vibe Database - Flattened Views (Part 2)
-- Migration: 20260119_VibeIndexOptimization_Part2_Views
-- Date: 2026-01-19
-- Purpose: Create flattened views for common collections to eliminate JSONB extraction overhead
-- Spec: VIBE_JSONB_OPTIMIZATION_SPEC.md Part 2
-- ========================================

-- UP MIGRATION
-- ========================================

BEGIN;

-- ========================================
-- Agent Mail Collections (when agent system is provisioned)
-- ========================================

-- 2.1 Agent Profiles View
CREATE OR REPLACE VIEW vibe.v_agent_profiles AS
SELECT
  document_id,
  client_id,
  user_id,
  collection,
  table_name,
  (data->>'id')::int AS agent_id,
  data->>'name' AS agent_name,
  data->>'display_name' AS display_name,
  data->>'username' AS username,
  (data->>'owner_user_id')::int AS owner_user_id,
  (data->>'team_id')::int AS team_id,
  data->>'role_preset' AS role_preset,
  (data->>'is_active')::boolean AS is_active,
  data->'capabilities' AS capabilities_json,
  data->'skills' AS skills_json,
  created_at,
  updated_at,
  deleted_at
FROM vibe.documents
WHERE collection = 'agent_mail'
  AND table_name = 'agent_profiles'
  AND deleted_at IS NULL;

COMMENT ON VIEW vibe.v_agent_profiles IS
'Flattened view of agent profiles - eliminates JSONB extraction overhead. Use this for agent profile queries instead of raw JSONB.';

-- 2.2 Agent Capabilities View
CREATE OR REPLACE VIEW vibe.v_agent_capabilities AS
SELECT
  document_id,
  client_id,
  collection,
  table_name,
  (data->>'id')::int AS capability_id,
  (data->>'agent_id')::int AS agent_id,
  data->>'capability' AS capability_name,
  (data->>'enabled')::boolean AS is_enabled,
  data->'config' AS config_json,
  created_at,
  deleted_at
FROM vibe.documents
WHERE collection = 'agent_mail'
  AND table_name = 'agent_capabilities'
  AND deleted_at IS NULL;

COMMENT ON VIEW vibe.v_agent_capabilities IS
'Flattened view of agent capabilities - fast lookup of agent permissions and capabilities.';

-- 2.3 Agent Teams View
CREATE OR REPLACE VIEW vibe.v_agent_teams AS
SELECT
  document_id,
  client_id,
  collection,
  table_name,
  (data->>'id')::int AS team_id,
  data->>'name' AS team_name,
  data->>'team_type' AS team_type,
  (data->>'owner_user_id')::int AS owner_user_id,
  data->>'tenant_id' AS tenant_id,
  (data->>'is_active')::boolean AS is_active,
  data->'shared_rules' AS shared_rules_json,
  created_at,
  updated_at,
  deleted_at
FROM vibe.documents
WHERE collection = 'agent_mail'
  AND table_name = 'agent_teams'
  AND deleted_at IS NULL;

COMMENT ON VIEW vibe.v_agent_teams IS
'Flattened view of agent teams - team management and organization structure.';

-- ========================================
-- Vibe App Collections (system-level data)
-- ========================================

-- 2.4 Users View
CREATE OR REPLACE VIEW vibe.v_users AS
SELECT
  document_id,
  client_id,
  user_id,
  collection,
  table_name,
  (data->>'id')::int AS vibe_user_id,
  data->>'email' AS email,
  data->>'username' AS username,
  data->>'display_name' AS display_name,
  (data->>'is_active')::boolean AS is_active,
  (data->>'email_verified')::boolean AS email_verified,
  data->'roles' AS roles_json,
  data->'preferences' AS preferences_json,
  created_at,
  updated_at,
  deleted_at
FROM vibe.documents
WHERE collection = 'vibe_app'
  AND table_name = 'users'
  AND deleted_at IS NULL;

COMMENT ON VIEW vibe.v_users IS
'Flattened view of Vibe app users - eliminates JSONB extraction for user lookups.';

-- 2.5 Login Sessions View
CREATE OR REPLACE VIEW vibe.v_login_sessions AS
SELECT
  document_id,
  client_id,
  user_id,
  collection,
  table_name,
  (data->>'id')::int AS session_id,
  (data->>'user_id')::int AS session_user_id,
  data->>'session_token' AS session_token,
  data->>'ip_address' AS ip_address,
  data->>'user_agent' AS user_agent,
  (data->>'is_active')::boolean AS is_active,
  (data->>'expires_at')::text AS expires_at,
  created_at,
  deleted_at
FROM vibe.documents
WHERE collection = 'vibe_app'
  AND table_name = 'login_sessions'
  AND deleted_at IS NULL;

COMMENT ON VIEW vibe.v_login_sessions IS
'Flattened view of login sessions - fast session lookups without JSONB parsing.';

-- ========================================
-- Ideal Resume Collections
-- ========================================

-- 2.6 Resumes View
CREATE OR REPLACE VIEW vibe.v_resumes AS
SELECT
  document_id,
  client_id,
  user_id,
  collection,
  table_name,
  (data->>'id')::int AS resume_id,
  data->>'title' AS resume_title,
  data->>'status' AS status,
  (data->>'is_published')::boolean AS is_published,
  data->'content' AS content_json,
  data->'metadata' AS metadata_json,
  created_at,
  updated_at,
  deleted_at
FROM vibe.documents
WHERE collection = 'ideal_resume'
  AND table_name = 'resumes'
  AND deleted_at IS NULL;

COMMENT ON VIEW vibe.v_resumes IS
'Flattened view of resumes - fast resume queries without JSONB extraction.';

-- ========================================
-- Generic Collection View (for ad-hoc queries)
-- ========================================

-- 2.7 All Documents View (with common fields extracted)
CREATE OR REPLACE VIEW vibe.v_documents_flat AS
SELECT
  document_id,
  client_id,
  user_id,
  collection,
  table_name,
  (data->>'id')::int AS record_id,
  data->>'name' AS name,
  data->>'title' AS title,
  data->>'status' AS status,
  (data->>'is_active')::boolean AS is_active,
  data->>'email' AS email,
  created_at,
  updated_at,
  deleted_at,
  data AS full_data
FROM vibe.documents
WHERE deleted_at IS NULL;

COMMENT ON VIEW vibe.v_documents_flat IS
'Generic flattened view with common fields extracted. Use collection-specific views for better performance. Includes full_data column for remaining fields.';

-- Log migration completion
DO $$
BEGIN
    RAISE NOTICE '=================================================';
    RAISE NOTICE 'Migration 20260119_VibeIndexOptimization_Part2_Views completed';
    RAISE NOTICE '=================================================';
    RAISE NOTICE 'Created 7 flattened views for common collections';
    RAISE NOTICE 'Views eliminate JSONB extraction overhead in queries';
    RAISE NOTICE 'Use v_agent_profiles, v_users, v_resumes, etc. for fast queries';
END $$;

COMMIT;


-- DOWN MIGRATION (Rollback)
-- ========================================

-- Uncomment below to rollback this migration

/*
BEGIN;

-- Drop views
DROP VIEW IF EXISTS vibe.v_agent_profiles;
DROP VIEW IF EXISTS vibe.v_agent_capabilities;
DROP VIEW IF EXISTS vibe.v_agent_teams;
DROP VIEW IF EXISTS vibe.v_users;
DROP VIEW IF EXISTS vibe.v_login_sessions;
DROP VIEW IF EXISTS vibe.v_resumes;
DROP VIEW IF EXISTS vibe.v_documents_flat;

RAISE NOTICE 'Migration 20260119_VibeIndexOptimization_Part2_Views rolled back successfully';

COMMIT;
*/


-- VERIFICATION QUERIES
-- ========================================

-- Verify views were created
SELECT
    schemaname,
    viewname,
    viewowner
FROM pg_views
WHERE schemaname = 'vibe'
  AND viewname LIKE 'v_%'
ORDER BY viewname;

-- Test agent profiles view (when data exists)
SELECT * FROM vibe.v_agent_profiles LIMIT 5;

-- Test users view
SELECT * FROM vibe.v_users LIMIT 5;

-- Test login sessions view
SELECT * FROM vibe.v_login_sessions
WHERE is_active = true
ORDER BY created_at DESC
LIMIT 10;


-- USAGE EXAMPLES
-- ========================================

/*
-- Before (raw JSONB extraction):
SELECT
  document_id,
  (data->>'agent_id')::int AS agent_id,
  data->>'agent_name' AS agent_name,
  (data->>'is_active')::boolean AS is_active
FROM vibe.documents
WHERE collection = 'agent_mail'
  AND table_name = 'agent_profiles'
  AND (data->>'is_active')::boolean = true
  AND deleted_at IS NULL;

-- After (using view):
SELECT
  document_id,
  agent_id,
  agent_name,
  is_active
FROM vibe.v_agent_profiles
WHERE is_active = true;

-- Performance improvement: Eliminates JSONB extraction overhead
-- Readability improvement: Much simpler SQL
-- Maintainability improvement: Field mappings centralized in view definition
*/


-- VIEW USAGE BEST PRACTICES
-- ========================================

/*
1. Use views for SELECT queries only (not INSERT/UPDATE/DELETE)
2. Views don't cache data - they're query templates
3. Indexes on base table (vibe.documents) are still used
4. For complex queries, views may not always choose optimal plan - use EXPLAIN
5. Views are updateable if they meet PostgreSQL criteria (single table, no aggregates)
6. Views work great with ORMs (map to view like a table)

Performance considerations:
- Views don't add overhead beyond query simplification
- Expression indexes on base table still apply
- Partial indexes on base table still apply
- PostgreSQL query planner optimizes through views

When NOT to use views:
- Very complex multi-join queries (may confuse planner)
- Aggregation queries with GROUP BY (create materialized views instead)
- Real-time analytics (use materialized views instead)
*/
