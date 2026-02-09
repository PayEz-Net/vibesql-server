-- =====================================================================================
-- Migration: Add agent_documents to vibe_agents collection
-- Date: 2026-01-20
-- Version: 2.2.0 → 2.3.0
-- Description:
--   Adds agent_documents and agent_mail_attachments tables to vibe_agents for
--   Phase 1 document storage. Documents <100KB stored in content_md column,
--   blob_url stubbed for Phase 2 Azure Blob Storage integration.
--
-- Purpose:
--   - Persistent document storage for agents (specs, reports, reviews)
--   - Mail attachment support via junction table
--   - Version tracking with self-referencing FK
--   - Hybrid storage strategy (inline + blob)
-- =====================================================================================

-- Update vibe_agents schema from v2.2.0 to v2.3.0
UPDATE vibe.collection_schemas
SET
    json_schema = '{
        "schema": "vibe_agents",
        "version": "2.3.0",
        "description": "IdealVibe Agents - Document Storage Phase 1",
        "tables": [
            "vibe_projects",
            "vibe_project_members",
            "kanban_tasks",
            "agent_documents",
            "agent_mail_attachments",
            "agent_tier_limits",
            "agent_profiles",
            "agent_credentials",
            "agent_teams",
            "agent_team_members",
            "agent_mail_boxes",
            "agent_mail_messages",
            "agent_capabilities",
            "agent_profile_capabilities",
            "agent_execution_logs",
            "agent_runner_triggers",
            "agent_runner_schedules",
            "agent_runner_sessions",
            "agent_runner_outputs"
        ]
    }'::jsonb,
    version = 4,
    updated_at = CURRENT_TIMESTAMP
WHERE client_id = 0
  AND collection = 'vibe_agents';

-- Verify update
SELECT
    collection,
    json_schema->>'version' as schema_version,
    jsonb_array_length(json_schema->'tables') as table_count,
    json_schema->'tables' as tables
FROM vibe.collection_schemas
WHERE client_id = 0 AND collection = 'vibe_agents';
