-- =====================================================================================
-- Update vibe_agents Schema with Full Table Definitions
-- =====================================================================================
-- Purpose: Replace string-only table names with full table definitions
-- Date: 2026-02-05
-- Author: DotNetPert
-- Database: vibe
-- =====================================================================================
-- ISSUE: Original schema had tables as simple strings: ["vibe_projects", "agent_teams", ...]
-- FIX: Update to full table objects with name, description, schema, field counts
-- =====================================================================================

-- Update the global template schema (client_id=0)
UPDATE vibe.collection_schemas
SET json_schema = '{
  "schema": "vibe_agents",
  "version": "2.6.0",
  "description": "IdealVibe Agents - Complete data-driven agent management system",
  "tables": [
    {
      "name": "vibe_projects",
      "description": "User projects - scopes agents to specific work contexts",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "owner_user_id": {"type": "integer", "required": true, "description": "Project owner"},
        "client_id": {"type": "integer", "required": true, "description": "IDP client for multi-tenant isolation"},
        "name": {"type": "string", "maxLength": 128, "required": true},
        "description": {"type": "string", "maxLength": 512},
        "is_active": {"type": "boolean", "default": true},
        "created_at": {"type": "timestamp", "autoNow": true},
        "updated_at": {"type": "timestamp"}
      },
      "indexes": ["owner_user_id", "client_id"]
    },
    {
      "name": "vibe_project_members",
      "description": "Project members - users who can collaborate on a project",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "project_id": {"type": "integer", "required": true, "fk": "vibe_projects.id"},
        "user_id": {"type": "integer", "required": true},
        "role": {"type": "string", "enum": ["owner", "admin", "member", "viewer"], "default": "member"},
        "invited_by": {"type": "integer"},
        "joined_at": {"type": "timestamp", "autoNow": true}
      },
      "indexes": ["project_id", "user_id"]
    },
    {
      "name": "agent_tier_limits",
      "description": "Resource limits per subscription tier",
      "schema": {
        "tier_code": {"type": "string", "primaryKey": true, "enum": ["free-trial", "starter", "pro", "team", "enterprise"]},
        "max_agents": {"type": "integer", "default": 10},
        "max_teams": {"type": "integer", "default": 1},
        "agent_mail_enabled": {"type": "boolean", "default": true},
        "agent_runner_enabled": {"type": "boolean", "default": false},
        "kanban_enabled": {"type": "boolean", "default": true},
        "architecture_rules_enabled": {"type": "boolean", "default": true},
        "schema_designer_enabled": {"type": "boolean", "default": false},
        "max_mail_per_day": {"type": "integer", "default": 100},
        "max_runner_sessions_per_day": {"type": "integer", "default": 0}
      },
      "indexes": ["tier_code"]
    },
    {
      "name": "agent_teams",
      "description": "Groups of agents with shared configuration",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "owner_user_id": {"type": "integer", "required": true},
        "tenant_id": {"type": "string", "maxLength": 64},
        "project_id": {"type": "integer", "fk": "vibe_projects.id"},
        "name": {"type": "string", "maxLength": 128, "required": true},
        "display_name": {"type": "string", "maxLength": 128},
        "team_type": {"type": "string", "enum": ["dotnet-api", "web-nextjs", "app-react-native", "full-stack", "coordinator", "custom"]},
        "description": {"type": "string", "maxLength": 512},
        "is_active": {"type": "boolean", "default": true},
        "created_at": {"type": "timestamp", "autoNow": true},
        "created_by": {"type": "integer"}
      },
      "indexes": ["owner_user_id", "project_id", "team_type"]
    },
    {
      "name": "agent_profiles",
      "description": "Complete agent identity - everything needed to activate an agent",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "team_id": {"type": "integer", "fk": "agent_teams.id"},
        "project_id": {"type": "integer", "fk": "vibe_projects.id"},
        "status": {"type": "string", "enum": ["active", "bullpen", "inactive"], "default": "bullpen"},
        "role": {"type": "string", "enum": ["primary", "support", "specialist"], "default": "support"},
        "name": {"type": "string", "maxLength": 64, "required": true},
        "display_name": {"type": "string", "maxLength": 128},
        "username": {"type": "string", "maxLength": 128, "unique": true},
        "role_preset": {"type": "string", "enum": ["backend-developer", "frontend-developer", "mobile-developer", "code-reviewer", "coordinator", "custom"]},
        "personality_preset": {"type": "string", "enum": ["professional-concise", "friendly-verbose", "security-focused", "creative", "custom"]},
        "identity_prompt": {"type": "string", "maxLength": 16384},
        "expertise_tags": {"type": "array", "items": "string"},
        "avatar_url": {"type": "string", "maxLength": 512},
        "is_active": {"type": "boolean", "default": true},
        "is_coordinator": {"type": "boolean", "default": false},
        "last_activated_at": {"type": "timestamp"},
        "created_at": {"type": "timestamp", "autoNow": true},
        "updated_at": {"type": "timestamp"}
      },
      "indexes": ["team_id", "project_id", "name", "status", "role_preset"]
    },
    {
      "name": "agent_capabilities",
      "description": "Feature flags for each agent",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "agent_id": {"type": "integer", "required": true, "fk": "agent_profiles.id"},
        "capability": {"type": "string", "enum": ["agent_mail", "architecture_rules", "kanban_board", "agent_runner", "schema_designer", "query_builder", "file_access", "web_search", "code_execution"]},
        "enabled": {"type": "boolean", "default": true},
        "config": {"type": "object"},
        "granted_at": {"type": "timestamp", "autoNow": true}
      },
      "indexes": ["agent_id", "capability"]
    },
    {
      "name": "agent_safety_rules",
      "description": "Destructive Command Guard (DCG) - safety guardrails preventing dangerous operations",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "agent_id": {"type": "integer", "required": true, "fk": "agent_profiles.id", "unique": true},
        "blocked_patterns": {"type": "array", "items": "string", "default": ["rm -rf /", "DROP DATABASE", "TRUNCATE"]},
        "approval_required_patterns": {"type": "array", "items": "string", "default": ["DELETE FROM", "git push.*main"]},
        "blocked_paths": {"type": "array", "items": "string", "default": ["/etc", "~/.ssh", "~/.aws"]},
        "allowed_paths": {"type": "array", "items": "string"},
        "max_files_per_session": {"type": "integer", "default": 50},
        "max_deletions_per_session": {"type": "integer", "default": 10},
        "max_lines_per_edit": {"type": "integer", "default": 500},
        "allow_production_changes": {"type": "boolean", "default": false},
        "allow_network_requests": {"type": "boolean", "default": true},
        "allow_shell_commands": {"type": "boolean", "default": true}
      },
      "indexes": ["agent_id"]
    },
    {
      "name": "agent_mcp_servers",
      "description": "MCP servers available to this agent",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "agent_id": {"type": "integer", "required": true, "fk": "agent_profiles.id"},
        "server_name": {"type": "string", "maxLength": 64, "required": true},
        "server_url": {"type": "string", "maxLength": 512, "required": true},
        "server_type": {"type": "string", "enum": ["agent-mail", "architecture", "vibe-sql", "schema-designer", "custom"]},
        "auth_type": {"type": "string", "enum": ["none", "bearer", "api_key"], "default": "bearer"},
        "is_enabled": {"type": "boolean", "default": true},
        "config": {"type": "object"}
      },
      "indexes": ["agent_id", "server_type"]
    },
    {
      "name": "agent_skills",
      "description": "Claude Code skills installed for this agent",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "agent_id": {"type": "integer", "required": true, "fk": "agent_profiles.id"},
        "skill_name": {"type": "string", "maxLength": 128, "required": true},
        "skill_source": {"type": "string", "maxLength": 256, "required": true},
        "skill_version": {"type": "string", "maxLength": 32},
        "is_active": {"type": "boolean", "default": true},
        "installed_at": {"type": "timestamp", "autoNow": true}
      },
      "indexes": ["agent_id"]
    },
    {
      "name": "agent_mail_messages",
      "description": "Inter-agent mail system - async communication between agents",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "project_id": {"type": "integer", "fk": "vibe_projects.id"},
        "from_agent_id": {"type": "integer", "required": true, "fk": "agent_profiles.id"},
        "to_agent_id": {"type": "integer", "required": true, "fk": "agent_profiles.id"},
        "subject": {"type": "string", "maxLength": 256, "required": true},
        "body": {"type": "string", "maxLength": 65536, "required": true},
        "is_read": {"type": "boolean", "default": false},
        "is_archived": {"type": "boolean", "default": false},
        "priority": {"type": "string", "enum": ["low", "normal", "high", "urgent"], "default": "normal"},
        "created_at": {"type": "timestamp", "autoNow": true},
        "read_at": {"type": "timestamp"}
      },
      "indexes": ["project_id", "from_agent_id", "to_agent_id", "is_read", "priority"]
    },
    {
      "name": "agent_mail_macros",
      "description": "Predefined mail templates for common message patterns",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "team_id": {"type": "integer", "fk": "agent_teams.id"},
        "macro_name": {"type": "string", "maxLength": 64, "required": true},
        "subject_template": {"type": "string", "maxLength": 256, "required": true},
        "body_template": {"type": "string", "maxLength": 4096, "required": true},
        "default_to_role": {"type": "string", "maxLength": 64}
      },
      "indexes": ["team_id", "macro_name"]
    },
    {
      "name": "agent_kanban_boards",
      "description": "Kanban boards for agent task tracking",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "team_id": {"type": "integer", "fk": "agent_teams.id", "unique": true},
        "name": {"type": "string", "maxLength": 128, "default": "Team Board"},
        "lanes_json": {"type": "array", "items": "string", "default": ["backlog", "ready", "in_progress", "review", "done"]},
        "created_at": {"type": "timestamp", "autoNow": true}
      },
      "indexes": ["team_id"]
    },
    {
      "name": "agent_kanban_tasks",
      "description": "Tasks on the Kanban board",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "board_id": {"type": "integer", "required": true, "fk": "agent_kanban_boards.id"},
        "title": {"type": "string", "maxLength": 256, "required": true},
        "description": {"type": "string", "maxLength": 4096},
        "lane": {"type": "string", "maxLength": 32, "default": "backlog"},
        "assigned_agent_id": {"type": "integer", "fk": "agent_profiles.id"},
        "created_by_agent_id": {"type": "integer", "fk": "agent_profiles.id"},
        "priority": {"type": "string", "enum": ["low", "normal", "high", "urgent"], "default": "normal"},
        "labels": {"type": "array", "items": "string"},
        "due_date": {"type": "timestamp"},
        "created_at": {"type": "timestamp", "autoNow": true},
        "updated_at": {"type": "timestamp"}
      },
      "indexes": ["board_id", "lane", "assigned_agent_id", "priority"]
    },
    {
      "name": "agent_attention_queue",
      "description": "Notifications and alerts for agents",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "agent_id": {"type": "integer", "required": true, "fk": "agent_profiles.id"},
        "level": {"type": "string", "enum": ["info", "warning", "error", "success"], "required": true},
        "code": {"type": "string", "maxLength": 64, "required": true},
        "title": {"type": "string", "maxLength": 256, "required": true},
        "message": {"type": "string", "maxLength": 1024, "required": true},
        "action_url": {"type": "string", "maxLength": 512},
        "action_label": {"type": "string", "maxLength": 64},
        "is_dismissed": {"type": "boolean", "default": false},
        "created_at": {"type": "timestamp", "autoNow": true},
        "expires_at": {"type": "timestamp"}
      },
      "indexes": ["agent_id", "level", "code", "is_dismissed"]
    },
    {
      "name": "agent_runner_triggers",
      "description": "Auto-start triggers for agents (Agent Runner feature)",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "agent_id": {"type": "integer", "required": true, "fk": "agent_profiles.id"},
        "trigger_type": {"type": "string", "enum": ["new_mail", "mail_from_agent", "mail_subject_match", "task_assigned", "schedule"], "required": true},
        "trigger_config": {"type": "object", "required": true},
        "is_enabled": {"type": "boolean", "default": true},
        "cooldown_minutes": {"type": "integer", "default": 5},
        "max_sessions_per_day": {"type": "integer", "default": 10}
      },
      "indexes": ["agent_id", "trigger_type"]
    },
    {
      "name": "agent_runner_sessions",
      "description": "Auto-started agent sessions history",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "session_id": {"type": "string", "maxLength": 64, "unique": true, "required": true},
        "agent_id": {"type": "integer", "required": true, "fk": "agent_profiles.id"},
        "trigger_id": {"type": "integer", "fk": "agent_runner_triggers.id"},
        "trigger_message_id": {"type": "integer", "fk": "agent_mail_messages.id"},
        "status": {"type": "string", "enum": ["pending", "running", "completed", "failed", "timeout"], "default": "pending"},
        "started_at": {"type": "timestamp"},
        "completed_at": {"type": "timestamp"},
        "token_usage": {"type": "integer"},
        "error_message": {"type": "string", "maxLength": 1024}
      },
      "indexes": ["agent_id", "session_id", "status"]
    },
    {
      "name": "kanban_tasks",
      "description": "Task/spec tracking for agents - persistent memory across sessions",
      "schema": {
        "task_id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "title": {"type": "string", "maxLength": 256, "required": true},
        "description": {"type": "string"},
        "status": {"type": "string", "enum": ["backlog", "in_progress", "review", "done", "blocked"], "default": "backlog"},
        "priority": {"type": "string", "enum": ["low", "medium", "high", "critical"], "default": "medium"},
        "assigned_to": {"type": "string", "maxLength": 64},
        "created_by": {"type": "string", "maxLength": 64},
        "spec_path": {"type": "string", "maxLength": 512},
        "files_changed": {"type": "array", "items": "string"},
        "blockers": {"type": "string"},
        "review_notes": {"type": "string"},
        "created_at": {"type": "timestamp", "autoNow": true},
        "updated_at": {"type": "timestamp"},
        "completed_at": {"type": "timestamp"}
      },
      "indexes": ["status", "priority", "assigned_to"]
    },
    {
      "name": "agent_documents",
      "description": "Document storage for agent specs, reports, and mail attachments",
      "schema": {
        "document_id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "agent_id": {"type": "integer", "required": true, "fk": "agents.id"},
        "document_type": {"type": "string", "enum": ["spec", "report", "review", "attachment", "plan", "log"], "default": "attachment"},
        "filename": {"type": "string", "maxLength": 255, "required": true},
        "mime_type": {"type": "string", "maxLength": 128, "default": "text/markdown"},
        "size_bytes": {"type": "integer", "required": true},
        "content_md": {"type": "string", "maxLength": 102400},
        "blob_url": {"type": "string", "maxLength": 512},
        "version": {"type": "integer", "default": 1},
        "parent_document_id": {"type": "integer", "fk": "agent_documents.document_id"},
        "title": {"type": "string", "maxLength": 512},
        "tags": {"type": "array", "items": "string"},
        "metadata": {"type": "object"},
        "created_by": {"type": "integer", "required": true},
        "created_at": {"type": "timestamp", "autoNow": true},
        "updated_at": {"type": "timestamp"},
        "is_deleted": {"type": "boolean", "default": false}
      },
      "indexes": ["agent_id", "document_type", "version", "is_deleted"]
    },
    {
      "name": "agent_mail_attachments",
      "description": "Junction table linking documents to mail messages",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "message_id": {"type": "integer", "required": true, "fk": "agent_mail_messages.id"},
        "document_id": {"type": "integer", "required": true, "fk": "agent_documents.document_id"},
        "attachment_order": {"type": "integer", "default": 0},
        "created_at": {"type": "timestamp", "autoNow": true}
      },
      "indexes": ["message_id", "document_id"]
    },
    {
      "name": "autonomy_settings",
      "description": "Autonomy mode configuration for supervised autonomous operation",
      "schema": {
        "setting_id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "project_id": {"type": "integer", "required": true, "fk": "vibe_projects.id"},
        "enabled": {"type": "boolean", "default": false},
        "mode": {"type": "string", "enum": ["attended", "autonomous"], "default": "attended"},
        "stop_condition": {"type": "string", "enum": ["milestone", "blocker", "time", "manual"], "default": "milestone"},
        "current_spec_id": {"type": "integer", "fk": "agent_documents.document_id"},
        "current_milestone": {"type": "string", "maxLength": 256},
        "max_runtime_hours": {"type": "integer", "default": 8},
        "started_at": {"type": "timestamp"},
        "notify_phone": {"type": "string", "maxLength": 20},
        "notify_email": {"type": "string", "maxLength": 256},
        "skip_permissions": {"type": "boolean", "default": false},
        "coordinator_loop_enabled": {"type": "boolean", "default": false},
        "coordinator_loop_interval_minutes": {"type": "integer", "default": 5},
        "escalation_sensitivity": {"type": "integer", "minimum": 1, "maximum": 4, "default": 2},
        "escalation_shutdown_mode": {"type": "string", "enum": ["soft", "hard", "pause"], "default": "soft"},
        "created_at": {"type": "timestamp", "autoNow": true},
        "updated_at": {"type": "timestamp"}
      },
      "indexes": ["project_id", "enabled", "mode"]
    },
    {
      "name": "standup_entries",
      "description": "Structured standup log for queryable agent activity tracking",
      "schema": {
        "entry_id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "project_id": {"type": "integer", "required": true, "fk": "vibe_projects.id"},
        "agent_id": {"type": "integer", "required": true, "fk": "agent_profiles.id"},
        "event_type": {"type": "string", "enum": ["started", "completed", "blocked", "review_requested", "review_passed", "review_failed", "milestone_done"], "required": true},
        "task_id": {"type": "integer", "fk": "kanban_tasks.task_id"},
        "summary": {"type": "string", "maxLength": 512, "required": true},
        "details_md": {"type": "string"},
        "created_at": {"type": "timestamp", "autoNow": true}
      },
      "indexes": ["project_id", "agent_id", "event_type", "created_at"]
    },
    {
      "name": "day_plans",
      "description": "Agent day plans for context lock and milestone tracking",
      "schema": {
        "plan_id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "project_id": {"type": "integer", "required": true, "fk": "vibe_projects.id"},
        "agent_id": {"type": "integer", "required": true, "fk": "agent_profiles.id"},
        "spec_id": {"type": "integer", "required": true, "fk": "agent_documents.document_id"},
        "milestone": {"type": "string", "maxLength": 256, "required": true},
        "objective": {"type": "string", "maxLength": 512, "required": true},
        "tasks_json": {"type": "object", "required": true},
        "files_json": {"type": "object"},
        "dependencies_json": {"type": "object"},
        "acceptance_criteria_json": {"type": "object"},
        "status": {"type": "string", "enum": ["draft", "active", "completed", "abandoned"], "default": "draft"},
        "created_at": {"type": "timestamp", "autoNow": true},
        "completed_at": {"type": "timestamp"}
      },
      "indexes": ["project_id", "agent_id", "spec_id", "status"]
    },
    {
      "name": "escalation_log",
      "description": "Log of escalation events for coordinator emergency shutdowns",
      "schema": {
        "id": {"type": "integer", "primaryKey": true, "autoIncrement": true},
        "client_id": {"type": "integer", "required": true},
        "project_id": {"type": "integer", "required": true, "fk": "vibe_projects.id"},
        "triggered_at": {"type": "timestamp", "autoNow": true},
        "sensitivity_level": {"type": "integer", "minimum": 1, "maximum": 4},
        "trigger_type": {"type": "string", "enum": ["system_failure", "data_integrity", "security_incident", "all_agents_blocked", "repeated_blocker", "stale_review", "quality_degradation", "agent_spinning", "manual"]},
        "trigger_details": {"type": "object"},
        "shutdown_mode": {"type": "string", "enum": ["soft", "hard", "pause"]},
        "notification_channels": {"type": "array", "items": "string"},
        "notification_sent_at": {"type": "timestamp"},
        "resolved_at": {"type": "timestamp"},
        "resolved_by": {"type": "string", "maxLength": 255},
        "resolution_action": {"type": "string", "enum": ["resumed", "cancelled", "reconfigured"]},
        "resolution_notes": {"type": "string"}
      },
      "indexes": ["client_id", "project_id", "triggered_at", "trigger_type"]
    }
  ]
}'::jsonb,
    version = 2,
    updated_at = CURRENT_TIMESTAMP
WHERE client_id = 0
  AND collection = 'vibe_agents'
  AND is_active = true;

-- Also update any client-specific schemas that were copied from the template
UPDATE vibe.collection_schemas
SET json_schema = (
    SELECT json_schema
    FROM vibe.collection_schemas
    WHERE client_id = 0 AND collection = 'vibe_agents' AND is_active = true
),
    version = version + 1,
    updated_at = CURRENT_TIMESTAMP
WHERE collection = 'vibe_agents'
  AND is_active = true
  AND client_id > 0;

-- =====================================================================================
-- VERIFICATION
-- =====================================================================================

-- Check updated schema has table objects instead of strings
SELECT
    client_id,
    collection,
    version,
    jsonb_array_length(json_schema->'tables') as table_count,
    (json_schema->'tables'->0->>'name') as first_table_name,
    (json_schema->'tables'->0->>'description') as first_table_desc
FROM vibe.collection_schemas
WHERE collection = 'vibe_agents'
  AND is_active = true
ORDER BY client_id;

-- =====================================================================================
-- MIGRATION COMPLETE
-- =====================================================================================
-- Updated vibe_agents schema with 23 full table definitions including:
-- - vibe_projects, vibe_project_members
-- - agent_tier_limits, agent_teams, agent_profiles
-- - agent_capabilities, agent_safety_rules, agent_mcp_servers, agent_skills
-- - agent_mail_messages, agent_mail_macros, agent_mail_attachments
-- - agent_kanban_boards, agent_kanban_tasks, kanban_tasks
-- - agent_attention_queue, agent_runner_triggers, agent_runner_sessions
-- - agent_documents, autonomy_settings, standup_entries, day_plans, escalation_log
-- =====================================================================================
