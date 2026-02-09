-- =====================================================================================
-- Migration: Add Bullpen and Project Scoping to agent_profiles
-- Date: 2026-01-20
-- Version: 2.4.0 to 2.5.0
-- Description:
--   Adds project_id FK, status enum, and role enum to agent_profiles.
--   Enables bullpen concept (reserve pool of agents) and project scoping.
--
-- Purpose:
--   - Every agent tied to a project (project_id FK required)
--   - Agent status: active (assigned), bullpen (reserve), inactive (disabled)
--   - Agent role: primary (main), support (helper), specialist (expert)
--   - Removes 2x2 agent limit (tier-based limits remain in agent_tier_limits)
-- =====================================================================================

-- Update vibe_agents schema from v2.4.0 to v2.5.0
UPDATE vibe.collection_schemas
SET
    json_schema = '{
        "schema": "vibe_agents",
        "version": "2.5.0",
        "description": "IdealVibe Agents - Bullpen and Project Scoping",
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
    version = 6,
    updated_at = CURRENT_TIMESTAMP
WHERE client_id = 0
  AND collection = 'vibe_agents';

-- Verify update
SELECT
    collection,
    json_schema->>'version' as schema_version,
    jsonb_array_length(json_schema->'tables') as table_count
FROM vibe.collection_schemas
WHERE client_id = 0 AND collection = 'vibe_agents';
