-- =====================================================================================
-- Migration: Add autonomy tables to vibe_agents collection
-- Date: 2026-01-20
-- Version: 2.3.0 → 2.4.0
-- Description:
--   Adds autonomy_settings, standup_entries, and day_plans tables to vibe_agents
--   for supervised autonomous operation Phase 2.
--
-- Purpose:
--   - Autonomy mode configuration (start/stop, stop conditions)
--   - Structured standup logging (queryable activity tracking)
--   - Day plans for context lock and milestone tracking
--   - Enables agents to work autonomously with human checkpoints
-- =====================================================================================

-- Update vibe_agents schema from v2.3.0 to v2.4.0
UPDATE vibe.collection_schemas
SET
    json_schema = '{
        "schema": "vibe_agents",
        "version": "2.4.0",
        "description": "IdealVibe Agents - Supervised Autonomy Phase 2",
        "tables": [
            "vibe_projects",
            "vibe_project_members",
            "kanban_tasks",
            "agent_documents",
            "agent_mail_attachments",
            "autonomy_settings",
            "standup_entries",
            "day_plans",
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
    version = 5,
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
