-- =====================================================================================
-- Vibe Agents - Seed Data for agent_tier_limits
-- =====================================================================================
-- Purpose: Insert default tier limits for IdealVibe Agents system
-- Date: 2026-01-19
-- Author: DotNetPert
-- Spec: AGENT_SYSTEM_VIBE_SQL_SPEC.md Part 2.1
-- =====================================================================================

-- NOTE: This assumes vibe_agents schema has been registered via Vibe SQL
-- and documents table contains agent_tier_limits collection

-- =====================================================================================
-- SEED DATA: Agent Tier Limits
-- =====================================================================================

-- Free Trial Tier (simplified: 10 agents max)
INSERT INTO vibe.documents (client_id, collection, table_name, data)
VALUES (
  0, -- System/global client_id
  'vibe_agents',
  'agent_tier_limits',
  jsonb_build_object(
    'tier_code', 'free-trial',
    'max_agents', 10,
    'max_teams', 1,
    'agent_mail_enabled', true,
    'agent_runner_enabled', false,
    'kanban_enabled', true,
    'architecture_rules_enabled', true,
    'schema_designer_enabled', false,
    'max_mail_per_day', 100,
    'max_runner_sessions_per_day', 0
  )
)
ON CONFLICT (client_id, collection, table_name, ((data->>'tier_code')::text))
DO UPDATE SET data = EXCLUDED.data;

-- Starter Tier (paid, more agents)
INSERT INTO vibe.documents (client_id, collection, table_name, data)
VALUES (
  0,
  'vibe_agents',
  'agent_tier_limits',
  jsonb_build_object(
    'tier_code', 'starter',
    'max_agents', 3,
    'max_teams', 1,
    'agent_mail_enabled', true,
    'agent_runner_enabled', false,
    'kanban_enabled', true,
    'architecture_rules_enabled', true,
    'schema_designer_enabled', false,
    'max_mail_per_day', 250,
    'max_runner_sessions_per_day', 0
  )
)
ON CONFLICT (client_id, collection, table_name, ((data->>'tier_code')::text))
DO UPDATE SET data = EXCLUDED.data;

-- Pro Tier (full features)
INSERT INTO vibe.documents (client_id, collection, table_name, data)
VALUES (
  0,
  'vibe_agents',
  'agent_tier_limits',
  jsonb_build_object(
    'tier_code', 'pro',
    'max_agents', 10,
    'max_teams', 3,
    'agent_mail_enabled', true,
    'agent_runner_enabled', true,
    'kanban_enabled', true,
    'architecture_rules_enabled', true,
    'schema_designer_enabled', true,
    'max_mail_per_day', 1000,
    'max_runner_sessions_per_day', 50
  )
)
ON CONFLICT (client_id, collection, table_name, ((data->>'tier_code')::text))
DO UPDATE SET data = EXCLUDED.data;

-- Team Tier (larger teams)
INSERT INTO vibe.documents (client_id, collection, table_name, data)
VALUES (
  0,
  'vibe_agents',
  'agent_tier_limits',
  jsonb_build_object(
    'tier_code', 'team',
    'max_agents', 25,
    'max_teams', 10,
    'agent_mail_enabled', true,
    'agent_runner_enabled', true,
    'kanban_enabled', true,
    'architecture_rules_enabled', true,
    'schema_designer_enabled', true,
    'max_mail_per_day', 5000,
    'max_runner_sessions_per_day', 200
  )
)
ON CONFLICT (client_id, collection, table_name, ((data->>'tier_code')::text))
DO UPDATE SET data = EXCLUDED.data;

-- Enterprise Tier (unlimited)
INSERT INTO vibe.documents (client_id, collection, table_name, data)
VALUES (
  0,
  'vibe_agents',
  'agent_tier_limits',
  jsonb_build_object(
    'tier_code', 'enterprise',
    'max_agents', -1,  -- -1 = unlimited
    'max_teams', -1,   -- -1 = unlimited
    'agent_mail_enabled', true,
    'agent_runner_enabled', true,
    'kanban_enabled', true,
    'architecture_rules_enabled', true,
    'schema_designer_enabled', true,
    'max_mail_per_day', -1,  -- unlimited
    'max_runner_sessions_per_day', -1  -- unlimited
  )
)
ON CONFLICT (client_id, collection, table_name, ((data->>'tier_code')::text))
DO UPDATE SET data = EXCLUDED.data;

-- =====================================================================================
-- VERIFICATION
-- =====================================================================================

-- Verify all 5 tiers were inserted
SELECT
  data->>'tier_code' as tier_code,
  (data->>'max_agents')::int as max_agents,
  (data->>'max_teams')::int as max_teams,
  (data->>'agent_runner_enabled')::boolean as runner_enabled,
  (data->>'max_mail_per_day')::int as max_mail_per_day
FROM vibe.documents
WHERE collection = 'vibe_agents'
  AND table_name = 'agent_tier_limits'
  AND client_id = 0
ORDER BY
  CASE data->>'tier_code'
    WHEN 'free-trial' THEN 1
    WHEN 'starter' THEN 2
    WHEN 'pro' THEN 3
    WHEN 'team' THEN 4
    WHEN 'enterprise' THEN 5
  END;

-- =====================================================================================
-- MIGRATION COMPLETE
-- =====================================================================================
-- Seeded 5 agent tier limits:
-- 1. free-trial: 10 agents, no runner, 100 mail/day
-- 2. starter: 3 agents, no runner, 250 mail/day
-- 3. pro: 10 agents, runner enabled, 1000 mail/day, schema designer
-- 4. team: 25 agents, runner enabled, 5000 mail/day
-- 5. enterprise: unlimited agents, unlimited mail
-- =====================================================================================
