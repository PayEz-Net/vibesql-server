-- =====================================================================================
-- Migration: Add acp_secrets table to vibe_agents collection
-- Date: 2026-01-24
-- Version: 2.4.0 → 2.5.0
-- Description:
--   Adds acp_secrets table for storing encrypted agent authorization tokens.
--   Enables ACP agents to authenticate to Vibe APIs on behalf of authorized users.
--
-- Purpose:
--   - Store encrypted refresh tokens for agent-to-vibe authentication
--   - Track token lifecycle (created, rotated, revoked)
--   - Enable user-level control over agent access
-- =====================================================================================

-- Update vibe_agents schema from v2.4.0 to v2.5.0
UPDATE vibe.collection_schemas
SET
    json_schema = '{
        "schema": "vibe_agents",
        "version": "2.5.0",
        "description": "IdealVibe Agents - ACP Agent Authentication",
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
            "agent_runner_outputs",
            "acp_secrets"
        ]
    }'::jsonb,
    version = 6,
    updated_at = CURRENT_TIMESTAMP
WHERE client_id = 0
  AND collection = 'vibe_agents';

-- Create acp_secrets table in IDP database (not Vibe SQL)
-- This table stores encrypted refresh tokens for ACP agent authentication
-- Note: This should be run on the IDP database, not the Vibe database

-- For PostgreSQL (IDP database):
CREATE TABLE IF NOT EXISTS identity.acp_secrets (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             INTEGER NOT NULL,                    -- FK to asp_net_users
    client_id           INTEGER NOT NULL,                    -- FK to idp_clients (ACP application)
    secret_type         VARCHAR(50) NOT NULL,                -- 'refresh_token', 'signing_key'
    encrypted_value     TEXT NOT NULL,                       -- Encrypted token value
    enc_key_id          INTEGER NOT NULL,                    -- Encryption key ID for decryption
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at          TIMESTAMPTZ NULL,                    -- created_at + 3 days for refresh tokens
    last_used_at        TIMESTAMPTZ NULL,
    last_rotated_at     TIMESTAMPTZ NULL,
    is_revoked          BOOLEAN NOT NULL DEFAULT FALSE,
    revoked_at          TIMESTAMPTZ NULL,
    revoked_reason      VARCHAR(255) NULL,

    CONSTRAINT uq_acp_secrets_user_client_type UNIQUE (user_id, client_id, secret_type),
    CONSTRAINT fk_acp_secrets_user FOREIGN KEY (user_id) REFERENCES asp_net_users(id) ON DELETE CASCADE,
    CONSTRAINT fk_acp_secrets_client FOREIGN KEY (client_id) REFERENCES idp_clients(idp_client_id) ON DELETE CASCADE
);

-- Create index for quick lookups
CREATE INDEX IF NOT EXISTS ix_acp_secrets_user_client
    ON identity.acp_secrets (user_id, client_id)
    WHERE is_revoked = FALSE;

-- Create index for expiry checks
CREATE INDEX IF NOT EXISTS ix_acp_secrets_expires
    ON identity.acp_secrets (expires_at)
    WHERE is_revoked = FALSE AND expires_at IS NOT NULL;

-- Verify update
SELECT
    collection,
    json_schema->>'version' as schema_version,
    jsonb_array_length(json_schema->'tables') as table_count
FROM vibe.collection_schemas
WHERE client_id = 0 AND collection = 'vibe_agents';

COMMENT ON TABLE identity.acp_secrets IS 'Stores encrypted refresh tokens for ACP agent authentication';
COMMENT ON COLUMN identity.acp_secrets.encrypted_value IS 'Encrypted using ITokenizationService with General key';
COMMENT ON COLUMN identity.acp_secrets.expires_at IS 'Refresh token TTL is 3 days from creation/rotation';
