-- =====================================================================================
-- Migration: Add kanban_tasks to vibe_agents collection
-- Date: 2026-01-20
-- Description:
--   Adds kanban_tasks table to vibe_agents for persistent task/spec tracking.
--   Agents query on session start to restore working memory across context resets.
--
-- Purpose:
--   - Persistent memory for agents (survives compaction)
--   - Single source of truth for task state
--   - Async coordination between agents
--   - Full audit trail in Vibe SQL
-- =====================================================================================

-- Update vibe_agents schema from v2.1.0 to v2.2.0
UPDATE vibe.collection_schemas
SET
    json_schema = '{
        "schema": "vibe_agents",
        "version": "2.2.0",
        "description": "IdealVibe Agents - Complete data-driven agent management system",
        "tables": [
            "vibe_projects",
            "vibe_project_members",
            "kanban_tasks",
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
            "agent_runner_sessions"
        ]
    }'::jsonb,
    version = 3,
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
