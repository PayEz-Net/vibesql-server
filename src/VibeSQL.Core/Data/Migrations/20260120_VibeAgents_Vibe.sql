-- =====================================================================================
-- Vibe Agents Provisioning - Vibe Database
-- =====================================================================================
-- Purpose: Create vibe_agents schema definition, seed tier limits, default project & agents
-- Date: 2026-01-20
-- Author: DotNetPert
-- Database: vibe
-- Schema: vibe
-- Format: Vibe SQL (object-keyed tables with x-vibe-* annotations)
-- =====================================================================================

-- =====================================================================================
-- PART 1: SCHEMA DEFINITION
-- =====================================================================================
-- Create vibe_agents schema in collection_schemas (client_id=0 = global/system template)

INSERT INTO vibe.collection_schemas (
    client_id,
    collection,
    json_schema,
    version,
    is_active,
    created_at,
    created_by
)
VALUES (
    0, -- System/global template
    'vibe_agents',
    '{
  "tableGroup": "vibe_agents",
  "tables": {
    "vibe_projects": {
      "type": "object",
      "description": "User projects - scopes agents to specific work contexts",
      "required": ["id", "owner_user_id", "name"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique project identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "owner_user_id": {
          "type": "integer",
          "description": "User who owns this project",
          "x-vibe-fk": "users.user_id",
          "x-vibe-index": true
        },
        "client_id": {
          "type": "integer",
          "description": "Client this project belongs to",
          "x-vibe-index": true
        },
        "name": {
          "type": "string",
          "description": "Project name"
        },
        "description": {
          "type": "string",
          "description": "Project description"
        },
        "settings": {
          "type": "object",
          "description": "Project-specific settings JSON"
        },
        "is_active": {
          "type": "boolean",
          "description": "Whether project is active",
          "default": true
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "Creation timestamp"
        },
        "updated_at": {
          "type": "string",
          "format": "date-time",
          "description": "Last update timestamp"
        }
      }
    },
    "vibe_project_members": {
      "type": "object",
      "description": "Project members - users who can collaborate on a project",
      "required": ["id", "project_id", "user_id"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique membership identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "project_id": {
          "type": "integer",
          "description": "Project this membership belongs to",
          "x-vibe-fk": "vibe_projects.id",
          "x-vibe-index": true
        },
        "user_id": {
          "type": "integer",
          "description": "User who is a member",
          "x-vibe-index": true
        },
        "role": {
          "type": "string",
          "description": "Member role in the project",
          "enum": ["owner", "admin", "member", "viewer"]
        },
        "invited_by": {
          "type": "integer",
          "description": "User who invited this member"
        },
        "joined_at": {
          "type": "string",
          "format": "date-time",
          "description": "When member joined"
        }
      }
    },
    "agent_tier_limits": {
      "type": "object",
      "description": "Resource limits per subscription tier",
      "required": ["tier_code"],
      "properties": {
        "tier_code": {
          "type": "string",
          "description": "Tier identifier",
          "x-vibe-pk": true
        },
        "max_agents": {
          "type": "integer",
          "description": "Maximum agents allowed (-1 = unlimited)",
          "default": 10
        },
        "max_teams": {
          "type": "integer",
          "description": "Maximum teams allowed (-1 = unlimited)",
          "default": 1
        },
        "agent_mail_enabled": {
          "type": "boolean",
          "description": "Whether agent mail is enabled",
          "default": true
        },
        "agent_runner_enabled": {
          "type": "boolean",
          "description": "Whether agent runner is enabled",
          "default": false
        },
        "kanban_enabled": {
          "type": "boolean",
          "description": "Whether kanban boards are enabled",
          "default": true
        },
        "max_mail_per_day": {
          "type": "integer",
          "description": "Maximum mail messages per day (-1 = unlimited)",
          "default": 100
        }
      }
    },
    "agent_teams": {
      "type": "object",
      "description": "Groups of agents with shared configuration",
      "required": ["id", "owner_user_id", "name"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique team identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "owner_user_id": {
          "type": "integer",
          "description": "User who owns this team",
          "x-vibe-fk": "users.user_id",
          "x-vibe-index": true
        },
        "project_id": {
          "type": "integer",
          "description": "Project this team belongs to",
          "x-vibe-fk": "vibe_projects.id",
          "x-vibe-index": true
        },
        "name": {
          "type": "string",
          "description": "Team name"
        },
        "display_name": {
          "type": "string",
          "description": "Team display name"
        },
        "team_type": {
          "type": "string",
          "description": "Type of team",
          "enum": ["dotnet-api", "web-nextjs", "full-stack", "custom"]
        },
        "description": {
          "type": "string",
          "description": "Team description"
        },
        "is_active": {
          "type": "boolean",
          "description": "Whether team is active",
          "default": true
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "Creation timestamp"
        },
        "created_by": {
          "type": "integer",
          "description": "User who created this team"
        }
      }
    },
    "agent_profiles": {
      "type": "object",
      "description": "Complete agent identity - everything needed to activate an agent",
      "required": ["id", "name"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique agent identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "owner_user_id": {
          "type": "integer",
          "description": "IDP user ID who owns this agent template (null=system default). Note: No FK constraint as this is cross-client.",
          "x-vibe-index": true
        },
        "team_id": {
          "type": "integer",
          "description": "Team this agent belongs to",
          "x-vibe-fk": "agent_teams.id",
          "x-vibe-index": true
        },
        "project_id": {
          "type": "integer",
          "description": "Project this agent belongs to",
          "x-vibe-fk": "vibe_projects.id",
          "x-vibe-index": true
        },
        "is_template": {
          "type": "boolean",
          "description": "Whether this is a reusable template (client_id=0)",
          "default": false
        },
        "status": {
          "type": "string",
          "description": "Agent status",
          "enum": ["active", "bullpen", "inactive"],
          "x-vibe-index": true
        },
        "name": {
          "type": "string",
          "description": "Agent name (unique identifier)",
          "x-vibe-index": true
        },
        "display_name": {
          "type": "string",
          "description": "Agent display name"
        },
        "role_preset": {
          "type": "string",
          "description": "Agent role preset",
          "enum": ["backend-developer", "frontend-developer", "coordinator", "qa-engineer", "devops", "custom"]
        },
        "identity_prompt": {
          "type": "string",
          "description": "System prompt defining agent identity"
        },
        "expertise_tags": {
          "type": "array",
          "description": "Agent expertise tags"
        },
        "is_active": {
          "type": "boolean",
          "description": "Whether agent is active",
          "default": true
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "Creation timestamp"
        }
      }
    },
    "agent_capabilities": {
      "type": "object",
      "description": "Feature flags for each agent",
      "required": ["id", "agent_id", "capability"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique capability identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "agent_id": {
          "type": "integer",
          "description": "Agent this capability belongs to",
          "x-vibe-fk": "agent_profiles.id",
          "x-vibe-index": true
        },
        "capability": {
          "type": "string",
          "description": "Capability name",
          "enum": ["agent_mail", "kanban_board", "agent_runner", "schema_designer", "code_execution"],
          "x-vibe-index": true
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether capability is enabled",
          "default": true
        },
        "config": {
          "type": "object",
          "description": "Capability configuration"
        },
        "granted_at": {
          "type": "string",
          "format": "date-time",
          "description": "When capability was granted"
        }
      }
    },
    "agent_safety_rules": {
      "type": "object",
      "description": "Destructive Command Guard - safety guardrails",
      "required": ["id", "agent_id"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique rule identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "agent_id": {
          "type": "integer",
          "description": "Agent these rules apply to",
          "x-vibe-fk": "agent_profiles.id",
          "x-vibe-index": true
        },
        "blocked_patterns": {
          "type": "array",
          "description": "Command patterns that are blocked"
        },
        "approval_required_patterns": {
          "type": "array",
          "description": "Command patterns requiring approval"
        },
        "blocked_paths": {
          "type": "array",
          "description": "File paths that are blocked"
        },
        "max_files_per_session": {
          "type": "integer",
          "description": "Maximum files agent can modify per session",
          "default": 50
        },
        "allow_shell_commands": {
          "type": "boolean",
          "description": "Whether shell commands are allowed",
          "default": true
        }
      }
    },
    "agent_mcp_servers": {
      "type": "object",
      "description": "MCP servers available to agents",
      "required": ["id", "agent_id", "server_name", "server_url"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique server identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "agent_id": {
          "type": "integer",
          "description": "Agent this server is available to",
          "x-vibe-fk": "agent_profiles.id",
          "x-vibe-index": true
        },
        "server_name": {
          "type": "string",
          "description": "Server name"
        },
        "server_url": {
          "type": "string",
          "description": "Server URL"
        },
        "server_type": {
          "type": "string",
          "description": "Type of MCP server",
          "enum": ["agent-mail", "architecture", "vibe-sql", "custom"]
        },
        "is_enabled": {
          "type": "boolean",
          "description": "Whether server is enabled",
          "default": true
        },
        "config": {
          "type": "object",
          "description": "Server configuration"
        }
      }
    },
    "agent_skills": {
      "type": "object",
      "description": "Claude Code skills installed for agents",
      "required": ["id", "agent_id", "skill_name"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique skill identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "agent_id": {
          "type": "integer",
          "description": "Agent this skill belongs to",
          "x-vibe-fk": "agent_profiles.id",
          "x-vibe-index": true
        },
        "skill_name": {
          "type": "string",
          "description": "Skill name"
        },
        "skill_source": {
          "type": "string",
          "description": "Where skill was installed from"
        },
        "is_active": {
          "type": "boolean",
          "description": "Whether skill is active",
          "default": true
        },
        "installed_at": {
          "type": "string",
          "format": "date-time",
          "description": "When skill was installed"
        }
      }
    },
    "agent_mail_messages": {
      "type": "object",
      "description": "Inter-agent mail system - async communication between agents",
      "required": ["id", "from_agent_id", "to_agent_id", "subject", "body"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique message identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "project_id": {
          "type": "integer",
          "description": "Project context for this message",
          "x-vibe-fk": "vibe_projects.id",
          "x-vibe-index": true
        },
        "from_agent_id": {
          "type": "integer",
          "description": "Sending agent",
          "x-vibe-fk": "agent_profiles.id",
          "x-vibe-index": true
        },
        "to_agent_id": {
          "type": "integer",
          "description": "Receiving agent",
          "x-vibe-fk": "agent_profiles.id",
          "x-vibe-index": true
        },
        "subject": {
          "type": "string",
          "description": "Message subject"
        },
        "body": {
          "type": "string",
          "description": "Message body"
        },
        "is_read": {
          "type": "boolean",
          "description": "Whether message has been read",
          "default": false,
          "x-vibe-index": true
        },
        "priority": {
          "type": "string",
          "description": "Message priority",
          "enum": ["low", "normal", "high", "urgent"],
          "default": "normal"
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "When message was sent"
        },
        "read_at": {
          "type": "string",
          "format": "date-time",
          "description": "When message was read"
        }
      }
    },
    "agent_mail_macros": {
      "type": "object",
      "description": "Predefined mail templates for common message patterns",
      "required": ["id", "macro_name"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique macro identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "team_id": {
          "type": "integer",
          "description": "Team this macro belongs to",
          "x-vibe-fk": "agent_teams.id",
          "x-vibe-index": true
        },
        "macro_name": {
          "type": "string",
          "description": "Macro name"
        },
        "subject_template": {
          "type": "string",
          "description": "Subject line template"
        },
        "body_template": {
          "type": "string",
          "description": "Body template"
        }
      }
    },
    "agent_kanban_boards": {
      "type": "object",
      "description": "Kanban boards for agent task tracking",
      "required": ["id"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique board identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "team_id": {
          "type": "integer",
          "description": "Team this board belongs to",
          "x-vibe-fk": "agent_teams.id",
          "x-vibe-index": true
        },
        "name": {
          "type": "string",
          "description": "Board name"
        },
        "lanes_json": {
          "type": "array",
          "description": "Lane configuration"
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "Creation timestamp"
        }
      }
    },
    "agent_kanban_tasks": {
      "type": "object",
      "description": "Tasks on the Kanban board",
      "required": ["id", "board_id", "title"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique task identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "board_id": {
          "type": "integer",
          "description": "Board this task belongs to",
          "x-vibe-fk": "agent_kanban_boards.id",
          "x-vibe-index": true
        },
        "title": {
          "type": "string",
          "description": "Task title"
        },
        "description": {
          "type": "string",
          "description": "Task description"
        },
        "lane": {
          "type": "string",
          "description": "Current lane",
          "x-vibe-index": true
        },
        "assigned_agent_id": {
          "type": "integer",
          "description": "Agent assigned to this task",
          "x-vibe-fk": "agent_profiles.id",
          "x-vibe-index": true
        },
        "priority": {
          "type": "string",
          "description": "Task priority",
          "enum": ["low", "normal", "high", "urgent"],
          "default": "normal"
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "Creation timestamp"
        }
      }
    },
    "agent_attention_queue": {
      "type": "object",
      "description": "Notifications and alerts for agents",
      "required": ["id", "agent_id"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique notification identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "agent_id": {
          "type": "integer",
          "description": "Agent this notification is for",
          "x-vibe-fk": "agent_profiles.id",
          "x-vibe-index": true
        },
        "level": {
          "type": "string",
          "description": "Notification level",
          "enum": ["info", "warning", "error", "success"]
        },
        "code": {
          "type": "string",
          "description": "Notification code"
        },
        "title": {
          "type": "string",
          "description": "Notification title"
        },
        "message": {
          "type": "string",
          "description": "Notification message"
        },
        "is_dismissed": {
          "type": "boolean",
          "description": "Whether notification is dismissed",
          "default": false,
          "x-vibe-index": true
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "Creation timestamp"
        }
      }
    },
    "agent_runner_triggers": {
      "type": "object",
      "description": "Auto-start triggers for agents",
      "required": ["id", "agent_id", "trigger_type"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique trigger identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "agent_id": {
          "type": "integer",
          "description": "Agent this trigger is for",
          "x-vibe-fk": "agent_profiles.id",
          "x-vibe-index": true
        },
        "trigger_type": {
          "type": "string",
          "description": "Type of trigger",
          "enum": ["new_mail", "task_assigned", "schedule"],
          "x-vibe-index": true
        },
        "trigger_config": {
          "type": "object",
          "description": "Trigger configuration"
        },
        "is_enabled": {
          "type": "boolean",
          "description": "Whether trigger is enabled",
          "default": true
        },
        "cooldown_minutes": {
          "type": "integer",
          "description": "Cooldown between triggers in minutes",
          "default": 5
        }
      }
    },
    "agent_runner_sessions": {
      "type": "object",
      "description": "Auto-started agent sessions history",
      "required": ["id", "agent_id"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique session identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "session_id": {
          "type": "string",
          "description": "External session identifier"
        },
        "agent_id": {
          "type": "integer",
          "description": "Agent this session belongs to",
          "x-vibe-fk": "agent_profiles.id",
          "x-vibe-index": true
        },
        "trigger_id": {
          "type": "integer",
          "description": "Trigger that started this session",
          "x-vibe-fk": "agent_runner_triggers.id"
        },
        "status": {
          "type": "string",
          "description": "Session status",
          "enum": ["pending", "running", "completed", "failed"],
          "x-vibe-index": true
        },
        "started_at": {
          "type": "string",
          "format": "date-time",
          "description": "Session start timestamp"
        },
        "completed_at": {
          "type": "string",
          "format": "date-time",
          "description": "Session completion timestamp"
        },
        "token_usage": {
          "type": "integer",
          "description": "Tokens consumed in session"
        }
      }
    }
  }
}'::jsonb,
    1, -- version
    true, -- is_active
    CURRENT_TIMESTAMP,
    0 -- created_by (system)
);

-- =====================================================================================
-- PART 2: SEED DATA - AGENT TIER LIMITS
-- =====================================================================================

INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, created_by)
VALUES
(0, 'vibe_agents', 'agent_tier_limits', '{"tier_code": "free-trial", "max_agents": 10, "max_teams": 1, "agent_mail_enabled": true, "agent_runner_enabled": false, "kanban_enabled": true, "max_mail_per_day": 100}'::jsonb, CURRENT_TIMESTAMP, 0),
(0, 'vibe_agents', 'agent_tier_limits', '{"tier_code": "starter", "max_agents": 3, "max_teams": 1, "agent_mail_enabled": true, "agent_runner_enabled": false, "kanban_enabled": true, "max_mail_per_day": 250}'::jsonb, CURRENT_TIMESTAMP, 0),
(0, 'vibe_agents', 'agent_tier_limits', '{"tier_code": "pro", "max_agents": 10, "max_teams": 3, "agent_mail_enabled": true, "agent_runner_enabled": true, "kanban_enabled": true, "max_mail_per_day": 1000}'::jsonb, CURRENT_TIMESTAMP, 0),
(0, 'vibe_agents', 'agent_tier_limits', '{"tier_code": "team", "max_agents": 25, "max_teams": 10, "agent_mail_enabled": true, "agent_runner_enabled": true, "kanban_enabled": true, "max_mail_per_day": 5000}'::jsonb, CURRENT_TIMESTAMP, 0),
(0, 'vibe_agents', 'agent_tier_limits', '{"tier_code": "enterprise", "max_agents": -1, "max_teams": -1, "agent_mail_enabled": true, "agent_runner_enabled": true, "kanban_enabled": true, "max_mail_per_day": -1}'::jsonb, CURRENT_TIMESTAMP, 0);

-- =====================================================================================
-- PART 3: SEED DATA - DEFAULT PROJECT (Template)
-- =====================================================================================

INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, created_by)
VALUES (
    0, 'vibe_agents', 'vibe_projects',
    '{
        "id": 1,
        "owner_user_id": 1,
        "client_id": 0,
        "name": "Default Agent Team",
        "description": "Default project with starter agent team for development workflows",
        "settings": {
            "default_model": "claude-sonnet-4",
            "enable_agent_mail": true,
            "enable_kanban": true
        },
        "is_active": true
    }'::jsonb,
    CURRENT_TIMESTAMP, 0
);

-- =====================================================================================
-- PART 4: SEED DATA - DEFAULT TEAM
-- =====================================================================================

INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, created_by)
VALUES (
    0, 'vibe_agents', 'agent_teams',
    '{
        "id": 1,
        "owner_user_id": 1,
        "project_id": 1,
        "name": "core-dev-team",
        "display_name": "Core Development Team",
        "team_type": "full-stack",
        "description": "Primary development team with backend, frontend, QA, and DevOps agents",
        "is_active": true
    }'::jsonb,
    CURRENT_TIMESTAMP, 0
);

-- =====================================================================================
-- PART 5: SEED DATA - DEFAULT AGENTS
-- =====================================================================================

-- BAPert - Backend API Specialist
INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, created_by)
VALUES (
    0, 'vibe_agents', 'agent_profiles',
    '{
        "id": 1,
        "owner_user_id": null,
        "team_id": 1,
        "project_id": 1,
        "is_template": true,
        "status": "active",
        "name": "BAPert",
        "display_name": "Backend API Specialist",
        "role_preset": "backend-developer",
        "identity_prompt": "You are BAPert, a senior backend API developer specializing in .NET Core, C#, and RESTful API design. You focus on clean architecture, SOLID principles, and performance optimization. You communicate professionally and provide detailed technical explanations.",
        "expertise_tags": ["dotnet", "csharp", "api", "sql", "architecture", "performance"],
        "is_active": true
    }'::jsonb,
    CURRENT_TIMESTAMP, 0
);

-- NextPert - Frontend/Next.js Specialist
INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, created_by)
VALUES (
    0, 'vibe_agents', 'agent_profiles',
    '{
        "id": 2,
        "owner_user_id": null,
        "team_id": 1,
        "project_id": 1,
        "is_template": true,
        "status": "active",
        "name": "NextPert",
        "display_name": "Frontend Next.js Specialist",
        "role_preset": "frontend-developer",
        "identity_prompt": "You are NextPert, a senior frontend developer specializing in Next.js, React, TypeScript, and modern UI/UX patterns. You focus on component architecture, state management, and responsive design. You create accessible, performant user interfaces.",
        "expertise_tags": ["nextjs", "react", "typescript", "tailwind", "ui", "accessibility"],
        "is_active": true
    }'::jsonb,
    CURRENT_TIMESTAMP, 0
);

-- QAPert - Quality Assurance Specialist
INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, created_by)
VALUES (
    0, 'vibe_agents', 'agent_profiles',
    '{
        "id": 3,
        "owner_user_id": null,
        "team_id": 1,
        "project_id": 1,
        "is_template": true,
        "status": "active",
        "name": "QAPert",
        "display_name": "QA Testing Specialist",
        "role_preset": "qa-engineer",
        "identity_prompt": "You are QAPert, a senior QA engineer specializing in test automation, integration testing, and quality assurance processes. You write comprehensive test plans, identify edge cases, and ensure code quality through rigorous testing strategies.",
        "expertise_tags": ["testing", "automation", "cypress", "jest", "xunit", "quality"],
        "is_active": true
    }'::jsonb,
    CURRENT_TIMESTAMP, 0
);

-- DevOpsPert - DevOps/Infrastructure Specialist
INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, created_by)
VALUES (
    0, 'vibe_agents', 'agent_profiles',
    '{
        "id": 4,
        "owner_user_id": null,
        "team_id": 1,
        "project_id": 1,
        "is_template": true,
        "status": "active",
        "name": "DevOpsPert",
        "display_name": "DevOps Infrastructure Specialist",
        "role_preset": "devops",
        "identity_prompt": "You are DevOpsPert, a senior DevOps engineer specializing in CI/CD pipelines, Docker, Kubernetes, and cloud infrastructure. You focus on automation, monitoring, and reliable deployments. You ensure systems are scalable, secure, and maintainable.",
        "expertise_tags": ["docker", "kubernetes", "cicd", "azure", "linux", "monitoring"],
        "is_active": true
    }'::jsonb,
    CURRENT_TIMESTAMP, 0
);

-- CoordPert - Team Coordinator
INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, created_by)
VALUES (
    0, 'vibe_agents', 'agent_profiles',
    '{
        "id": 5,
        "owner_user_id": null,
        "team_id": 1,
        "project_id": 1,
        "is_template": true,
        "status": "active",
        "name": "CoordPert",
        "display_name": "Team Coordinator",
        "role_preset": "coordinator",
        "identity_prompt": "You are CoordPert, a technical project coordinator who orchestrates work between team agents. You break down complex tasks, assign work to appropriate specialists, track progress, and ensure smooth communication. You synthesize information and provide clear status updates.",
        "expertise_tags": ["coordination", "planning", "communication", "project-management"],
        "is_active": true
    }'::jsonb,
    CURRENT_TIMESTAMP, 0
);

-- =====================================================================================
-- PART 6: SEED DATA - DEFAULT KANBAN BOARD
-- =====================================================================================

INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, created_by)
VALUES (
    0, 'vibe_agents', 'agent_kanban_boards',
    '{
        "id": 1,
        "team_id": 1,
        "name": "Development Board",
        "lanes_json": [
            {"id": "backlog", "name": "Backlog", "order": 1},
            {"id": "todo", "name": "To Do", "order": 2},
            {"id": "in-progress", "name": "In Progress", "order": 3},
            {"id": "review", "name": "Review", "order": 4},
            {"id": "done", "name": "Done", "order": 5}
        ]
    }'::jsonb,
    CURRENT_TIMESTAMP, 0
);

-- =====================================================================================
-- VERIFICATION
-- =====================================================================================

-- 1. Verify schema was created with correct format
SELECT
    collection_schema_id,
    client_id,
    collection,
    version,
    json_schema->>'tableGroup' as table_group,
    jsonb_typeof(json_schema->'tables') as tables_format
FROM vibe.collection_schemas
WHERE collection = 'vibe_agents' AND client_id = 0;

-- 2. Count tables in schema (should show 16)
SELECT COUNT(*) as table_count
FROM vibe.collection_schemas,
     jsonb_object_keys(json_schema->'tables') as table_name
WHERE collection = 'vibe_agents' AND client_id = 0;

-- 3. Verify seed data counts
SELECT table_name, COUNT(*) as count
FROM vibe.documents
WHERE client_id = 0 AND collection = 'vibe_agents'
GROUP BY table_name
ORDER BY table_name;

-- =====================================================================================
-- MIGRATION COMPLETE
-- =====================================================================================
-- Schema: 16 tables with x-vibe-* annotations
-- Tier Limits: 5 tiers (free-trial, starter, pro, team, enterprise)
-- Default Project: "Default Agent Team"
-- Default Team: "Core Development Team"
-- Default Agents: BAPert, NextPert, QAPert, DevOpsPert, CoordPert
-- Default Kanban: Development Board with 5 lanes
-- =====================================================================================
