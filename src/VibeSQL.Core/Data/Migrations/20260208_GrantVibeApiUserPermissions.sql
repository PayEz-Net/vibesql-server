-- Migration: Grant vibe_api_user permissions on vibe schemas
-- Date: 2026-02-08
-- Purpose: External.Id.Api uses vibe_api_user to access vibe schema for agent provisioning
-- Run as: postgres superuser

-- =============================================================================
-- VIBE SCHEMA PERMISSIONS
-- =============================================================================

-- Grant schema usage
GRANT USAGE ON SCHEMA vibe TO vibe_api_user;

-- Grant all privileges on existing tables
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA vibe TO vibe_api_user;

-- Grant all privileges on existing sequences
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA vibe TO vibe_api_user;

-- Set default privileges for future tables
ALTER DEFAULT PRIVILEGES IN SCHEMA vibe GRANT ALL PRIVILEGES ON TABLES TO vibe_api_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA vibe GRANT ALL PRIVILEGES ON SEQUENCES TO vibe_api_user;

-- =============================================================================
-- VIBE_AGENTS SCHEMA PERMISSIONS (if exists)
-- =============================================================================

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'vibe_agents') THEN
        EXECUTE 'GRANT USAGE ON SCHEMA vibe_agents TO vibe_api_user';
        EXECUTE 'GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA vibe_agents TO vibe_api_user';
        EXECUTE 'GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA vibe_agents TO vibe_api_user';
        EXECUTE 'ALTER DEFAULT PRIVILEGES IN SCHEMA vibe_agents GRANT ALL PRIVILEGES ON TABLES TO vibe_api_user';
        EXECUTE 'ALTER DEFAULT PRIVILEGES IN SCHEMA vibe_agents GRANT ALL PRIVILEGES ON SEQUENCES TO vibe_api_user';
        RAISE NOTICE 'Granted permissions on vibe_agents schema';
    ELSE
        RAISE NOTICE 'vibe_agents schema does not exist yet - will be created during provisioning';
    END IF;
END $$;

-- =============================================================================
-- VERIFICATION
-- =============================================================================

-- Show granted permissions
SELECT
    table_schema,
    table_name,
    privilege_type
FROM information_schema.table_privileges
WHERE grantee = 'vibe_api_user'
  AND table_schema IN ('vibe', 'vibe_agents')
ORDER BY table_schema, table_name, privilege_type;
