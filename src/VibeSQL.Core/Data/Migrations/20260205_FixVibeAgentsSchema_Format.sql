-- =====================================================================================
-- Fix Vibe Agents Schema Format - Vibe Database
-- =====================================================================================
-- Purpose: Update vibe_agents schema to correct Vibe SQL format with x-vibe-* annotations
-- Date: 2026-02-05
-- Author: DotNetPert
-- Database: vibe
-- Schema: vibe
-- =====================================================================================
-- IMPORTANT: This migration fixes the schema format from array-based to object-keyed
-- format which is required for the UI to display tables correctly.
-- =====================================================================================

-- Update all vibe_agents schemas to use correct format
UPDATE vibe.collection_schemas
SET json_schema = '{
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
    "agents": {
      "type": "object",
      "description": "AI agents with specific capabilities and personalities",
      "required": ["id", "name", "agent_type"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique agent identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "name": {
          "type": "string",
          "description": "Agent display name"
        },
        "agent_type": {
          "type": "string",
          "description": "Type of agent",
          "enum": ["assistant", "specialist", "coordinator", "worker"]
        },
        "description": {
          "type": "string",
          "description": "Agent description and capabilities"
        },
        "system_prompt": {
          "type": "string",
          "description": "System prompt defining agent behavior"
        },
        "model": {
          "type": "string",
          "description": "AI model to use",
          "default": "gpt-4"
        },
        "temperature": {
          "type": "number",
          "description": "Model temperature setting",
          "default": 0.7
        },
        "max_tokens": {
          "type": "integer",
          "description": "Maximum response tokens"
        },
        "tools": {
          "type": "array",
          "description": "Available tools for this agent"
        },
        "capabilities": {
          "type": "object",
          "description": "Agent capability flags"
        },
        "avatar_url": {
          "type": "string",
          "description": "Agent avatar image URL"
        },
        "is_active": {
          "type": "boolean",
          "description": "Whether agent is active",
          "default": true
        },
        "is_system": {
          "type": "boolean",
          "description": "Whether this is a system agent",
          "default": false
        },
        "created_by": {
          "type": "integer",
          "description": "User who created this agent",
          "x-vibe-fk": "users.user_id"
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
    "agent_teams": {
      "type": "object",
      "description": "Teams of agents working together on tasks",
      "required": ["id", "name"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique team identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "name": {
          "type": "string",
          "description": "Team name"
        },
        "description": {
          "type": "string",
          "description": "Team purpose and description"
        },
        "project_id": {
          "type": "integer",
          "description": "Project this team belongs to",
          "x-vibe-fk": "vibe_projects.id",
          "x-vibe-index": true
        },
        "coordinator_agent_id": {
          "type": "integer",
          "description": "Lead agent coordinating the team",
          "x-vibe-fk": "agents.id"
        },
        "settings": {
          "type": "object",
          "description": "Team configuration settings"
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
        "updated_at": {
          "type": "string",
          "format": "date-time",
          "description": "Last update timestamp"
        }
      }
    },
    "agent_team_members": {
      "type": "object",
      "description": "Agents assigned to teams with specific roles",
      "required": ["id", "team_id", "agent_id"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique membership identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "team_id": {
          "type": "integer",
          "description": "Team this membership belongs to",
          "x-vibe-fk": "agent_teams.id",
          "x-vibe-index": true
        },
        "agent_id": {
          "type": "integer",
          "description": "Agent in this team",
          "x-vibe-fk": "agents.id",
          "x-vibe-index": true
        },
        "role": {
          "type": "string",
          "description": "Agent role in the team",
          "enum": ["member", "lead", "specialist", "reviewer"]
        },
        "joined_at": {
          "type": "string",
          "format": "date-time",
          "description": "When agent joined the team"
        }
      }
    },
    "agent_mail_messages": {
      "type": "object",
      "description": "Inter-agent communication messages",
      "required": ["id", "from_agent_id", "to_agent_id", "subject"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique message identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "from_agent_id": {
          "type": "integer",
          "description": "Sending agent",
          "x-vibe-fk": "agents.id",
          "x-vibe-index": true
        },
        "to_agent_id": {
          "type": "integer",
          "description": "Receiving agent",
          "x-vibe-fk": "agents.id",
          "x-vibe-index": true
        },
        "thread_id": {
          "type": "integer",
          "description": "Conversation thread",
          "x-vibe-fk": "agent_mail_threads.id",
          "x-vibe-index": true
        },
        "subject": {
          "type": "string",
          "description": "Message subject"
        },
        "body": {
          "type": "string",
          "description": "Message content"
        },
        "priority": {
          "type": "string",
          "description": "Message priority",
          "enum": ["low", "normal", "high", "urgent"],
          "default": "normal"
        },
        "status": {
          "type": "string",
          "description": "Message delivery status",
          "enum": ["pending", "delivered", "read", "archived"],
          "default": "pending"
        },
        "attachments": {
          "type": "array",
          "description": "Message attachments"
        },
        "metadata": {
          "type": "object",
          "description": "Additional message metadata"
        },
        "sent_at": {
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
    "agent_mail_threads": {
      "type": "object",
      "description": "Conversation threads between agents",
      "required": ["id", "subject"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique thread identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "subject": {
          "type": "string",
          "description": "Thread subject"
        },
        "project_id": {
          "type": "integer",
          "description": "Project context for this thread",
          "x-vibe-fk": "vibe_projects.id",
          "x-vibe-index": true
        },
        "task_id": {
          "type": "integer",
          "description": "Related task if any",
          "x-vibe-fk": "agent_tasks.id"
        },
        "participant_agent_ids": {
          "type": "array",
          "description": "Agents participating in thread"
        },
        "status": {
          "type": "string",
          "description": "Thread status",
          "enum": ["active", "resolved", "archived"],
          "default": "active"
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "Thread creation timestamp"
        },
        "updated_at": {
          "type": "string",
          "format": "date-time",
          "description": "Last activity timestamp"
        }
      }
    },
    "agent_tasks": {
      "type": "object",
      "description": "Tasks assigned to agents",
      "required": ["id", "title", "status"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique task identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "title": {
          "type": "string",
          "description": "Task title"
        },
        "description": {
          "type": "string",
          "description": "Task description and requirements"
        },
        "project_id": {
          "type": "integer",
          "description": "Project this task belongs to",
          "x-vibe-fk": "vibe_projects.id",
          "x-vibe-index": true
        },
        "assigned_agent_id": {
          "type": "integer",
          "description": "Agent assigned to this task",
          "x-vibe-fk": "agents.id",
          "x-vibe-index": true
        },
        "assigned_team_id": {
          "type": "integer",
          "description": "Team assigned to this task",
          "x-vibe-fk": "agent_teams.id"
        },
        "parent_task_id": {
          "type": "integer",
          "description": "Parent task for subtasks",
          "x-vibe-fk": "agent_tasks.id"
        },
        "status": {
          "type": "string",
          "description": "Task status",
          "enum": ["pending", "in_progress", "blocked", "review", "completed", "cancelled"],
          "default": "pending"
        },
        "priority": {
          "type": "string",
          "description": "Task priority",
          "enum": ["low", "normal", "high", "critical"],
          "default": "normal"
        },
        "due_at": {
          "type": "string",
          "format": "date-time",
          "description": "Task due date"
        },
        "started_at": {
          "type": "string",
          "format": "date-time",
          "description": "When work started"
        },
        "completed_at": {
          "type": "string",
          "format": "date-time",
          "description": "When task was completed"
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
    "agent_task_results": {
      "type": "object",
      "description": "Results and outputs from agent tasks",
      "required": ["id", "task_id", "agent_id"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique result identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "task_id": {
          "type": "integer",
          "description": "Task this result belongs to",
          "x-vibe-fk": "agent_tasks.id",
          "x-vibe-index": true
        },
        "agent_id": {
          "type": "integer",
          "description": "Agent that produced this result",
          "x-vibe-fk": "agents.id",
          "x-vibe-index": true
        },
        "result_type": {
          "type": "string",
          "description": "Type of result",
          "enum": ["output", "artifact", "report", "decision"]
        },
        "content": {
          "type": "string",
          "description": "Result content"
        },
        "artifacts": {
          "type": "array",
          "description": "Generated artifacts"
        },
        "metrics": {
          "type": "object",
          "description": "Performance metrics"
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "When result was created"
        }
      }
    },
    "agent_memory": {
      "type": "object",
      "description": "Persistent memory storage for agents",
      "required": ["id", "agent_id", "memory_type", "key"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique memory identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "agent_id": {
          "type": "integer",
          "description": "Agent this memory belongs to",
          "x-vibe-fk": "agents.id",
          "x-vibe-index": true
        },
        "memory_type": {
          "type": "string",
          "description": "Type of memory",
          "enum": ["short_term", "long_term", "episodic", "semantic"],
          "x-vibe-index": true
        },
        "key": {
          "type": "string",
          "description": "Memory key for retrieval",
          "x-vibe-index": true
        },
        "value": {
          "type": "object",
          "description": "Memory content"
        },
        "importance": {
          "type": "number",
          "description": "Memory importance score",
          "default": 0.5
        },
        "access_count": {
          "type": "integer",
          "description": "Number of times accessed",
          "default": 0
        },
        "last_accessed_at": {
          "type": "string",
          "format": "date-time",
          "description": "Last access timestamp"
        },
        "expires_at": {
          "type": "string",
          "format": "date-time",
          "description": "When memory expires"
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "Creation timestamp"
        }
      }
    },
    "agent_tools": {
      "type": "object",
      "description": "Available tools that agents can use",
      "required": ["id", "name", "tool_type"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique tool identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "name": {
          "type": "string",
          "description": "Tool name",
          "x-vibe-index": true
        },
        "tool_type": {
          "type": "string",
          "description": "Type of tool",
          "enum": ["api", "function", "query", "script"]
        },
        "description": {
          "type": "string",
          "description": "Tool description and usage"
        },
        "parameters_schema": {
          "type": "object",
          "description": "JSON schema for tool parameters"
        },
        "endpoint": {
          "type": "string",
          "description": "API endpoint or function reference"
        },
        "auth_required": {
          "type": "boolean",
          "description": "Whether authentication is required",
          "default": false
        },
        "is_active": {
          "type": "boolean",
          "description": "Whether tool is available",
          "default": true
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "Creation timestamp"
        }
      }
    },
    "agent_tool_executions": {
      "type": "object",
      "description": "Log of tool executions by agents",
      "required": ["id", "agent_id", "tool_id"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique execution identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "agent_id": {
          "type": "integer",
          "description": "Agent that executed the tool",
          "x-vibe-fk": "agents.id",
          "x-vibe-index": true
        },
        "tool_id": {
          "type": "integer",
          "description": "Tool that was executed",
          "x-vibe-fk": "agent_tools.id",
          "x-vibe-index": true
        },
        "task_id": {
          "type": "integer",
          "description": "Task context if any",
          "x-vibe-fk": "agent_tasks.id"
        },
        "input_params": {
          "type": "object",
          "description": "Input parameters used"
        },
        "output": {
          "type": "object",
          "description": "Execution output"
        },
        "status": {
          "type": "string",
          "description": "Execution status",
          "enum": ["pending", "running", "success", "error"]
        },
        "error_message": {
          "type": "string",
          "description": "Error message if failed"
        },
        "duration_ms": {
          "type": "integer",
          "description": "Execution duration in milliseconds"
        },
        "executed_at": {
          "type": "string",
          "format": "date-time",
          "description": "Execution timestamp"
        }
      }
    },
    "agent_workflows": {
      "type": "object",
      "description": "Defined workflows for agent automation",
      "required": ["id", "name"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique workflow identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "name": {
          "type": "string",
          "description": "Workflow name",
          "x-vibe-index": true
        },
        "description": {
          "type": "string",
          "description": "Workflow description"
        },
        "project_id": {
          "type": "integer",
          "description": "Project this workflow belongs to",
          "x-vibe-fk": "vibe_projects.id",
          "x-vibe-index": true
        },
        "trigger_type": {
          "type": "string",
          "description": "What triggers this workflow",
          "enum": ["manual", "scheduled", "event", "webhook"]
        },
        "trigger_config": {
          "type": "object",
          "description": "Trigger configuration"
        },
        "steps": {
          "type": "array",
          "description": "Workflow step definitions"
        },
        "is_active": {
          "type": "boolean",
          "description": "Whether workflow is active",
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
    "agent_workflow_runs": {
      "type": "object",
      "description": "Execution instances of workflows",
      "required": ["id", "workflow_id", "status"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique run identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "workflow_id": {
          "type": "integer",
          "description": "Workflow being executed",
          "x-vibe-fk": "agent_workflows.id",
          "x-vibe-index": true
        },
        "triggered_by": {
          "type": "string",
          "description": "What triggered this run"
        },
        "status": {
          "type": "string",
          "description": "Run status",
          "enum": ["pending", "running", "paused", "completed", "failed", "cancelled"]
        },
        "current_step": {
          "type": "integer",
          "description": "Current step index"
        },
        "step_results": {
          "type": "array",
          "description": "Results from each step"
        },
        "context": {
          "type": "object",
          "description": "Workflow execution context"
        },
        "error_message": {
          "type": "string",
          "description": "Error message if failed"
        },
        "started_at": {
          "type": "string",
          "format": "date-time",
          "description": "Run start timestamp"
        },
        "completed_at": {
          "type": "string",
          "format": "date-time",
          "description": "Run completion timestamp"
        }
      }
    },
    "agent_conversations": {
      "type": "object",
      "description": "Conversation sessions with agents",
      "required": ["id", "agent_id"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique conversation identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "agent_id": {
          "type": "integer",
          "description": "Agent in this conversation",
          "x-vibe-fk": "agents.id",
          "x-vibe-index": true
        },
        "user_id": {
          "type": "integer",
          "description": "Human user if any",
          "x-vibe-index": true
        },
        "project_id": {
          "type": "integer",
          "description": "Project context",
          "x-vibe-fk": "vibe_projects.id"
        },
        "title": {
          "type": "string",
          "description": "Conversation title"
        },
        "status": {
          "type": "string",
          "description": "Conversation status",
          "enum": ["active", "paused", "ended"],
          "default": "active"
        },
        "message_count": {
          "type": "integer",
          "description": "Number of messages",
          "default": 0
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "Conversation start timestamp"
        },
        "updated_at": {
          "type": "string",
          "format": "date-time",
          "description": "Last message timestamp"
        }
      }
    },
    "agent_conversation_messages": {
      "type": "object",
      "description": "Messages within agent conversations",
      "required": ["id", "conversation_id", "role", "content"],
      "properties": {
        "id": {
          "type": "integer",
          "description": "Unique message identifier",
          "x-vibe-pk": true,
          "x-vibe-auto-increment": true
        },
        "conversation_id": {
          "type": "integer",
          "description": "Parent conversation",
          "x-vibe-fk": "agent_conversations.id",
          "x-vibe-index": true
        },
        "role": {
          "type": "string",
          "description": "Message sender role",
          "enum": ["user", "assistant", "system", "tool"]
        },
        "content": {
          "type": "string",
          "description": "Message content"
        },
        "tool_calls": {
          "type": "array",
          "description": "Tool calls made in this message"
        },
        "tool_call_id": {
          "type": "string",
          "description": "ID if this is a tool response"
        },
        "tokens_used": {
          "type": "integer",
          "description": "Tokens consumed"
        },
        "created_at": {
          "type": "string",
          "format": "date-time",
          "description": "Message timestamp"
        }
      }
    }
  }
}'::jsonb
WHERE collection = 'vibe_agents';

-- =====================================================================================
-- VERIFICATION
-- =====================================================================================

-- Verify schema format is correct (should show object keys not array items)
SELECT
    client_id,
    collection,
    json_schema->>'tableGroup' as table_group,
    jsonb_typeof(json_schema->'tables') as tables_type
FROM vibe.collection_schemas
WHERE collection = 'vibe_agents';

-- Count tables
SELECT
    client_id,
    jsonb_object_keys(json_schema->'tables') as table_name
FROM vibe.collection_schemas
WHERE collection = 'vibe_agents' AND client_id = 0;

-- =====================================================================================
-- MIGRATION COMPLETE
-- =====================================================================================
-- Fixed vibe_agents schema to use correct Vibe SQL format:
-- - Object-keyed tables (not arrays)
-- - x-vibe-pk for primary keys
-- - x-vibe-auto-increment for auto-increment fields
-- - x-vibe-fk for foreign keys
-- - x-vibe-index for indexed fields
-- - required arrays for required fields
-- - enum arrays for enumerated values
-- - default values where appropriate
-- =====================================================================================
