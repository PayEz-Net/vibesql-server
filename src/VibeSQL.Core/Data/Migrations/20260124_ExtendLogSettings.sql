-- =====================================================================================
-- Extend client_log_settings with retention and category-based settings
-- =====================================================================================
-- Purpose: Add granular log level controls, retention, and size limits for MVP Admin
-- Date: 2026-01-24
-- Author: DotNetPert
-- Spec: BAPert - MVP Admin Logging Spec (Mail ID 2720)
-- =====================================================================================

-- Update client_log_settings schema with new fields for all clients
UPDATE vibe.collection_schemas
SET json_schema = jsonb_set(
    json_schema::jsonb,
    '{tables,client_log_settings}',
    '{
      "type": "object",
      "description": "Per-client log level and retention configuration",
      "properties": {
        "setting_id": {
          "type": "integer",
          "description": "Primary key",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "log_level": {
          "type": "string",
          "description": "Global minimum log level (legacy, use category levels instead)",
          "enum": ["debug", "info", "warn", "error", "fatal"],
          "default": "info"
        },
        "level_api": {
          "type": "integer",
          "description": "Log level for API category (0=debug, 1=info, 2=warn, 3=error, 4=critical)",
          "default": 1
        },
        "level_auth": {
          "type": "integer",
          "description": "Log level for Auth category",
          "default": 1
        },
        "level_database": {
          "type": "integer",
          "description": "Log level for Database category",
          "default": 2
        },
        "level_agent": {
          "type": "integer",
          "description": "Log level for Agent category",
          "default": 1
        },
        "level_system": {
          "type": "integer",
          "description": "Log level for System category",
          "default": 2
        },
        "retention_debug_days": {
          "type": "integer",
          "description": "Days to retain debug logs",
          "default": 7
        },
        "retention_info_days": {
          "type": "integer",
          "description": "Days to retain info logs",
          "default": 30
        },
        "retention_warn_days": {
          "type": "integer",
          "description": "Days to retain warning logs",
          "default": 60
        },
        "retention_error_days": {
          "type": "integer",
          "description": "Days to retain error logs",
          "default": 90
        },
        "retention_critical_days": {
          "type": "integer",
          "description": "Days to retain critical logs",
          "default": 180
        },
        "max_size_mb": {
          "type": "integer",
          "description": "Maximum log storage in MB",
          "default": 10
        },
        "max_rows": {
          "type": "integer",
          "description": "Maximum log rows",
          "default": 20000
        },
        "updated_by": {
          "type": "integer",
          "description": "User ID who last updated settings"
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "Setting creation timestamp"
        },
        "updated_at": {
          "type": "string",
          "format": "date-time",
          "description": "Last update timestamp"
        }
      },
      "required": ["setting_id"]
    }'::jsonb
),
updated_at = CURRENT_TIMESTAMP
WHERE collection = 'vibe_app';

-- =====================================================================================
-- Create global_log_settings table for system-wide defaults (singleton)
-- =====================================================================================

-- Create the global settings table in vibe schema (not document-based, true singleton)
CREATE TABLE IF NOT EXISTS vibe.global_log_settings (
    id INTEGER PRIMARY KEY DEFAULT 1 CHECK (id = 1),

    -- Category log levels (0=debug, 1=info, 2=warn, 3=error, 4=critical)
    level_api INTEGER NOT NULL DEFAULT 1,
    level_auth INTEGER NOT NULL DEFAULT 1,
    level_database INTEGER NOT NULL DEFAULT 2,
    level_agent INTEGER NOT NULL DEFAULT 1,
    level_system INTEGER NOT NULL DEFAULT 2,

    -- Retention by level (days)
    retention_debug_days INTEGER NOT NULL DEFAULT 7,
    retention_info_days INTEGER NOT NULL DEFAULT 30,
    retention_warn_days INTEGER NOT NULL DEFAULT 60,
    retention_error_days INTEGER NOT NULL DEFAULT 90,
    retention_critical_days INTEGER NOT NULL DEFAULT 180,

    -- Size limits
    max_size_mb INTEGER NOT NULL DEFAULT 10,
    max_rows INTEGER NOT NULL DEFAULT 20000,

    -- Audit
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by INTEGER
);

-- Insert singleton row with defaults
INSERT INTO vibe.global_log_settings (id)
VALUES (1)
ON CONFLICT (id) DO NOTHING;

-- =====================================================================================
-- Create index on data_logs for efficient pruning queries
-- =====================================================================================

-- Index for age-based pruning: find old logs by level
CREATE INDEX IF NOT EXISTS ix_vibe_documents_data_logs_prune
ON vibe.documents ((data->>'level'), (data->>'created_at'))
WHERE table_name = 'data_logs';

-- =====================================================================================
-- VERIFICATION
-- =====================================================================================

SELECT
    'client_log_settings schema updated' as check_item,
    json_schema::jsonb->'tables'->'client_log_settings'->'properties'->>'level_api' IS NOT NULL as has_level_api,
    json_schema::jsonb->'tables'->'client_log_settings'->'properties'->>'retention_debug_days' IS NOT NULL as has_retention,
    json_schema::jsonb->'tables'->'client_log_settings'->'properties'->>'max_size_mb' IS NOT NULL as has_size_limits
FROM vibe.collection_schemas
WHERE collection = 'vibe_app'
LIMIT 1;

SELECT
    'global_log_settings exists' as check_item,
    EXISTS (SELECT 1 FROM vibe.global_log_settings WHERE id = 1) as has_singleton;

-- =====================================================================================
-- MIGRATION COMPLETE
-- =====================================================================================
-- Changes:
-- 1. Extended client_log_settings with category levels, retention, size limits
-- 2. Created vibe.global_log_settings singleton table for system defaults
-- 3. Added index for efficient log pruning queries
-- =====================================================================================
