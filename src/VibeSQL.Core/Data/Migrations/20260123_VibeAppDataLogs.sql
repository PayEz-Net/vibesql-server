-- =====================================================================================
-- Add data_logs table to vibe_app schema
-- =====================================================================================
-- Purpose: Add data_logs table for queryable Vibe operation logging
-- Date: 2026-01-23
-- Author: DotNetPert
-- Spec: BAPert P0 URGENT - Vibe Data Logging (Mail ID 2618)
-- =====================================================================================

-- Add data_logs table to vibe_app schema for all clients
-- Uses jsonb_set to add the table definition to existing schema

UPDATE vibe.collection_schemas
SET json_schema = jsonb_set(
    json_schema::jsonb,
    '{tables,data_logs}',
    '{
      "type": "object",
      "description": "Vibe operation logs for debugging and monitoring",
      "properties": {
        "log_id": {
          "type": "integer",
          "description": "Primary key - unique log identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "level": {
          "type": "string",
          "description": "Log level",
          "enum": ["debug", "info", "warn", "error", "fatal"]
        },
        "category": {
          "type": "string",
          "description": "Log category",
          "enum": ["schema", "document", "query", "auth", "fk", "encryption", "migration", "system"]
        },
        "user_id": {
          "type": "integer",
          "description": "User who triggered the operation",
          "x-vibe-fk": "users.user_id"
        },
        "collection_name": {
          "type": "string",
          "description": "Collection involved in the operation"
        },
        "table_name": {
          "type": "string",
          "description": "Table involved in the operation"
        },
        "operation": {
          "type": "string",
          "description": "Operation type (insert, update, delete, query, etc.)"
        },
        "message": {
          "type": "string",
          "description": "Log message"
        },
        "details": {
          "type": "object",
          "description": "Additional details as JSON"
        },
        "error_code": {
          "type": "string",
          "description": "Error code if applicable"
        },
        "stack_trace": {
          "type": "string",
          "description": "Stack trace for errors"
        },
        "request_id": {
          "type": "string",
          "description": "Request ID for tracing"
        },
        "duration_ms": {
          "type": "integer",
          "description": "Operation duration in milliseconds"
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "Log timestamp"
        },
        "created_by": {
          "type": "integer",
          "description": "User ID who created this log entry"
        }
      },
      "required": ["log_id", "level", "category", "message", "created_at"]
    }'::jsonb
),
updated_at = CURRENT_TIMESTAMP
WHERE collection = 'vibe_app';

-- Also add client_log_settings table for per-client log level config
UPDATE vibe.collection_schemas
SET json_schema = jsonb_set(
    json_schema::jsonb,
    '{tables,client_log_settings}',
    '{
      "type": "object",
      "description": "Per-client log level configuration",
      "properties": {
        "setting_id": {
          "type": "integer",
          "description": "Primary key",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "log_level": {
          "type": "string",
          "description": "Minimum log level to capture",
          "enum": ["debug", "info", "warn", "error", "fatal"],
          "default": "info"
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
      "required": ["setting_id", "log_level"]
    }'::jsonb
),
updated_at = CURRENT_TIMESTAMP
WHERE collection = 'vibe_app';

-- =====================================================================================
-- VERIFICATION
-- =====================================================================================

SELECT
    client_id,
    collection,
    json_schema::jsonb->'tables'->>'data_logs' IS NOT NULL as has_data_logs,
    json_schema::jsonb->'tables'->>'client_log_settings' IS NOT NULL as has_log_settings
FROM vibe.collection_schemas
WHERE collection = 'vibe_app';

-- =====================================================================================
-- MIGRATION COMPLETE
-- =====================================================================================
-- Changes:
-- 1. Added data_logs table to vibe_app schema (all clients)
-- 2. Added client_log_settings table to vibe_app schema (all clients)
-- =====================================================================================
