-- ========================================
-- Vibe Database - Stored Procedures (Part 3)
-- Migration: 20260119_VibeIndexOptimization_Part3_Procedures
-- Date: 2026-01-19
-- Purpose: Create stored procedures for common query patterns
-- Spec: VIBE_JSONB_OPTIMIZATION_SPEC.md Part 3
-- ========================================

-- UP MIGRATION
-- ========================================

BEGIN;

-- ========================================
-- Agent Management Procedures
-- ========================================

-- 3.1 Get Agent Profile by ID
CREATE OR REPLACE FUNCTION vibe.get_agent_profile(
  p_client_id INT,
  p_agent_id INT
) RETURNS TABLE (
  document_id INT,
  agent_data JSONB,
  created_at TIMESTAMPTZ,
  updated_at TIMESTAMPTZ
) AS $$
  SELECT document_id, data, created_at, updated_at
  FROM vibe.documents
  WHERE client_id = p_client_id
    AND collection = 'agent_mail'
    AND table_name = 'agent_profiles'
    AND (data->>'id')::int = p_agent_id
    AND deleted_at IS NULL
  LIMIT 1;
$$ LANGUAGE SQL STABLE;

COMMENT ON FUNCTION vibe.get_agent_profile IS
'Fast agent profile lookup by ID. Returns full document with metadata.';

-- 3.2 Get Agent with Capabilities (joined data)
CREATE OR REPLACE FUNCTION vibe.get_agent_with_capabilities(
  p_client_id INT,
  p_agent_id INT
) RETURNS JSONB AS $$
DECLARE
  v_agent JSONB;
  v_capabilities JSONB;
BEGIN
  -- Get agent profile
  SELECT data INTO v_agent
  FROM vibe.documents
  WHERE client_id = p_client_id
    AND collection = 'agent_mail'
    AND table_name = 'agent_profiles'
    AND (data->>'id')::int = p_agent_id
    AND deleted_at IS NULL
  LIMIT 1;

  IF v_agent IS NULL THEN
    RETURN NULL;
  END IF;

  -- Get capabilities
  SELECT jsonb_agg(data->'capability') INTO v_capabilities
  FROM vibe.documents
  WHERE client_id = p_client_id
    AND collection = 'agent_mail'
    AND table_name = 'agent_capabilities'
    AND (data->>'agent_id')::int = p_agent_id
    AND (data->>'enabled')::boolean = true
    AND deleted_at IS NULL;

  -- Merge and return
  RETURN v_agent || jsonb_build_object('_capabilities', COALESCE(v_capabilities, '[]'::jsonb));
END;
$$ LANGUAGE plpgsql STABLE;

COMMENT ON FUNCTION vibe.get_agent_with_capabilities IS
'Returns agent profile with capabilities merged into _capabilities array. Single round-trip.';

-- 3.3 List Agents for Owner
CREATE OR REPLACE FUNCTION vibe.list_agents_for_owner(
  p_client_id INT,
  p_owner_user_id INT,
  p_include_inactive BOOLEAN DEFAULT FALSE
) RETURNS SETOF JSONB AS $$
  SELECT data
  FROM vibe.documents
  WHERE client_id = p_client_id
    AND collection = 'agent_mail'
    AND table_name = 'agent_profiles'
    AND (data->>'owner_user_id')::int = p_owner_user_id
    AND (p_include_inactive OR data->>'is_active' = 'true')
    AND deleted_at IS NULL
  ORDER BY created_at DESC;
$$ LANGUAGE SQL STABLE;

COMMENT ON FUNCTION vibe.list_agents_for_owner IS
'Lists all agents owned by a user. Optional include_inactive parameter.';

-- 3.4 Count Active Agents for Owner
CREATE OR REPLACE FUNCTION vibe.count_user_agents(
  p_client_id INT,
  p_owner_user_id INT
) RETURNS INT AS $$
  SELECT COUNT(*)::int
  FROM vibe.documents
  WHERE client_id = p_client_id
    AND collection = 'agent_mail'
    AND table_name = 'agent_profiles'
    AND (data->>'owner_user_id')::int = p_owner_user_id
    AND data->>'is_active' = 'true'
    AND deleted_at IS NULL;
$$ LANGUAGE SQL STABLE;

COMMENT ON FUNCTION vibe.count_user_agents IS
'Counts active agents for tier limit checks. Returns count as integer.';

-- 3.5 Check Agent Has Capability
CREATE OR REPLACE FUNCTION vibe.agent_has_capability(
  p_client_id INT,
  p_agent_id INT,
  p_capability TEXT
) RETURNS BOOLEAN AS $$
  SELECT EXISTS (
    SELECT 1
    FROM vibe.documents
    WHERE client_id = p_client_id
      AND collection = 'agent_mail'
      AND table_name = 'agent_capabilities'
      AND (data->>'agent_id')::int = p_agent_id
      AND data->>'capability' = p_capability
      AND data->>'enabled' = 'true'
      AND deleted_at IS NULL
  );
$$ LANGUAGE SQL STABLE;

COMMENT ON FUNCTION vibe.agent_has_capability IS
'Fast capability check. Returns true if agent has enabled capability.';

-- ========================================
-- Generic Document Procedures
-- ========================================

-- 3.6 Get Document by Collection and ID
CREATE OR REPLACE FUNCTION vibe.get_document_by_id(
  p_client_id INT,
  p_collection VARCHAR,
  p_table_name VARCHAR,
  p_record_id INT
) RETURNS JSONB AS $$
  SELECT data
  FROM vibe.documents
  WHERE client_id = p_client_id
    AND collection = p_collection
    AND table_name = p_table_name
    AND (data->>'id')::int = p_record_id
    AND deleted_at IS NULL
  LIMIT 1;
$$ LANGUAGE SQL STABLE;

COMMENT ON FUNCTION vibe.get_document_by_id IS
'Generic document lookup by ID within collection. Returns JSONB data.';

-- 3.7 List Documents in Collection
CREATE OR REPLACE FUNCTION vibe.list_documents(
  p_client_id INT,
  p_collection VARCHAR,
  p_table_name VARCHAR,
  p_user_id INT DEFAULT NULL,
  p_active_only BOOLEAN DEFAULT TRUE,
  p_limit INT DEFAULT 100,
  p_offset INT DEFAULT 0
) RETURNS SETOF JSONB AS $$
  SELECT data
  FROM vibe.documents
  WHERE client_id = p_client_id
    AND collection = p_collection
    AND table_name = p_table_name
    AND (p_user_id IS NULL OR user_id = p_user_id)
    AND (NOT p_active_only OR data->>'is_active' = 'true')
    AND deleted_at IS NULL
  ORDER BY created_at DESC
  LIMIT p_limit
  OFFSET p_offset;
$$ LANGUAGE SQL STABLE;

COMMENT ON FUNCTION vibe.list_documents IS
'Generic paginated document listing with optional user_id and active_only filtering.';

-- 3.8 Count Documents in Collection
CREATE OR REPLACE FUNCTION vibe.count_documents(
  p_client_id INT,
  p_collection VARCHAR,
  p_table_name VARCHAR,
  p_user_id INT DEFAULT NULL,
  p_active_only BOOLEAN DEFAULT TRUE
) RETURNS INT AS $$
  SELECT COUNT(*)::int
  FROM vibe.documents
  WHERE client_id = p_client_id
    AND collection = p_collection
    AND table_name = p_table_name
    AND (p_user_id IS NULL OR user_id = p_user_id)
    AND (NOT p_active_only OR data->>'is_active' = 'true')
    AND deleted_at IS NULL;
$$ LANGUAGE SQL STABLE;

COMMENT ON FUNCTION vibe.count_documents IS
'Generic document count with optional user_id and active_only filtering.';

-- ========================================
-- Utility Procedures
-- ========================================

-- 3.9 Soft Delete Document
CREATE OR REPLACE FUNCTION vibe.soft_delete_document(
  p_document_id INT,
  p_deleted_by INT DEFAULT NULL
) RETURNS BOOLEAN AS $$
  UPDATE vibe.documents
  SET deleted_at = NOW(),
      updated_by = COALESCE(p_deleted_by, updated_by)
  WHERE document_id = p_document_id
    AND deleted_at IS NULL
  RETURNING TRUE;
$$ LANGUAGE SQL VOLATILE;

COMMENT ON FUNCTION vibe.soft_delete_document IS
'Soft deletes a document by setting deleted_at timestamp. Returns true if successful.';

-- Log migration completion
DO $$
BEGIN
    RAISE NOTICE '=================================================';
    RAISE NOTICE 'Migration 20260119_VibeIndexOptimization_Part3_Procedures completed';
    RAISE NOTICE '=================================================';
    RAISE NOTICE 'Created 9 stored procedures for common query patterns';
    RAISE NOTICE 'Agent procedures: get_agent_profile, get_agent_with_capabilities, list_agents_for_owner';
    RAISE NOTICE 'Generic procedures: get_document_by_id, list_documents, count_documents';
    RAISE NOTICE 'Use CALL or SELECT depending on procedure type';
END $$;

COMMIT;


-- DOWN MIGRATION (Rollback)
-- ========================================

/*
BEGIN;

DROP FUNCTION IF EXISTS vibe.get_agent_profile;
DROP FUNCTION IF EXISTS vibe.get_agent_with_capabilities;
DROP FUNCTION IF EXISTS vibe.list_agents_for_owner;
DROP FUNCTION IF EXISTS vibe.count_user_agents;
DROP FUNCTION IF EXISTS vibe.agent_has_capability;
DROP FUNCTION IF EXISTS vibe.get_document_by_id;
DROP FUNCTION IF EXISTS vibe.list_documents;
DROP FUNCTION IF EXISTS vibe.count_documents;
DROP FUNCTION IF EXISTS vibe.soft_delete_document;

RAISE NOTICE 'Migration 20260119_VibeIndexOptimization_Part3_Procedures rolled back successfully';

COMMIT;
*/


-- VERIFICATION QUERIES
-- ========================================

-- List all functions in vibe schema
SELECT
    n.nspname as schema,
    p.proname as function_name,
    pg_get_function_arguments(p.oid) as arguments,
    pg_get_function_result(p.oid) as returns
FROM pg_proc p
JOIN pg_namespace n ON p.pronamespace = n.oid
WHERE n.nspname = 'vibe'
  AND p.proname NOT LIKE 'pg_%'
ORDER BY p.proname;


-- USAGE EXAMPLES
-- ========================================

/*
-- Get agent profile
SELECT * FROM vibe.get_agent_profile(1, 123);

-- Get agent with capabilities
SELECT vibe.get_agent_with_capabilities(1, 123);

-- List all agents for owner
SELECT * FROM vibe.list_agents_for_owner(1, 456);

-- Count active agents
SELECT vibe.count_user_agents(1, 456);

-- Check capability
SELECT vibe.agent_has_capability(1, 123, 'agent_mail');

-- Generic document lookup
SELECT vibe.get_document_by_id(1, 'vibe_app', 'users', 789);

-- List documents with pagination
SELECT * FROM vibe.list_documents(1, 'vibe_app', 'users', NULL, TRUE, 10, 0);

-- Count documents
SELECT vibe.count_documents(1, 'vibe_app', 'users', NULL, TRUE);

-- Soft delete
SELECT vibe.soft_delete_document(12345, 456);
*/
