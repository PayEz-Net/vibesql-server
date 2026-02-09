-- =====================================================================================
-- Add vibe_projects and vibe_project_members to vibe_agents schema
-- =====================================================================================
-- Purpose: Add project scoping tables to vibe_agents schema
-- Date: 2026-01-20
-- Author: DotNetPert
-- Database: vibe
-- Schema: vibe
-- =====================================================================================

-- Update vibe_agents schema to version 2.1.0 with new tables
UPDATE vibe.collection_schemas
SET
    json_schema = '{
        "schema": "vibe_agents",
        "version": "2.1.0",
        "description": "IdealVibe Agents - Complete data-driven agent management system",
        "tables": [
            "vibe_projects",
            "vibe_project_members",
            "agent_tier_limits",
            "agent_teams",
            "agent_profiles",
            "agent_capabilities",
            "agent_safety_rules",
            "agent_mcp_servers",
            "agent_skills",
            "agent_mail_messages",
            "agent_mail_macros",
            "agent_kanban_boards",
            "agent_kanban_tasks",
            "agent_attention_queue",
            "agent_runner_triggers",
            "agent_runner_sessions"
        ]
    }'::jsonb,
    version = 2,
    updated_at = CURRENT_TIMESTAMP
WHERE client_id = 0
  AND collection = 'vibe_agents';

-- =====================================================================================
-- VERIFICATION
-- =====================================================================================

-- Verify schema was updated
SELECT
    collection_schema_id,
    client_id,
    collection,
    json_schema->>'version' as version,
    jsonb_array_length(json_schema->'tables') as table_count,
    version as schema_version,
    is_active,
    updated_at
FROM vibe.collection_schemas
WHERE collection = 'vibe_agents'
  AND client_id = 0;

-- Show all tables in schema
SELECT
    jsonb_array_elements_text(json_schema->'tables') as table_name
FROM vibe.collection_schemas
WHERE collection = 'vibe_agents'
  AND client_id = 0
ORDER BY table_name;

-- =====================================================================================
-- MIGRATION COMPLETE
-- =====================================================================================
-- ✅ Updated vibe_agents schema to version 2.1.0
-- ✅ Added vibe_projects table (user projects for scoping)
-- ✅ Added vibe_project_members table (project collaboration)
-- ✅ Total tables: 16 (was 14)
-- ✅ agent_teams already has project_id FK reference
-- ✅ agent_mail_messages already has project_id FK reference
-- =====================================================================================
