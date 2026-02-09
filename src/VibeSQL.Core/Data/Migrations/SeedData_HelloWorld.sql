-- =====================================================================================
-- Seed Data: Hello World Project for Vibe Agents
-- Date: 2026-01-20
-- Purpose:
--   Creates a starter "Hello World" project so new users don't see an empty screen.
--   Includes:
--   - Hello World project
--   - 4 starter agents in bullpen (Coordinator, Backend Dev, Frontend Dev, QA)
--   - 1 active platform agent (VibeSqlPert - always-on SQL helper, free to users)
--   - Sample Getting Started spec document
--   - 3 sample kanban tasks
--
-- Note: This script is idempotent - checks if data exists before inserting.
-- =====================================================================================

DO $$
DECLARE
    v_project_id INT;
    v_spec_doc_id INT;
    v_coordinator_agent_id INT;
    v_backend_agent_id INT;
    v_frontend_agent_id INT;
    v_qa_agent_id INT;
    v_vibesql_agent_id INT;
BEGIN
    -- Check if Hello World project already exists
    IF NOT EXISTS (
        SELECT 1 FROM vibe.documents
        WHERE client_id = 0
        AND collection = 'vibe_agents'
        AND table_name = 'vibe_projects'
        AND data::jsonb->>'name' = 'Hello World'
        AND deleted_at IS NULL
    ) THEN

        RAISE NOTICE 'Creating Hello World seed data...';

        -- =====================================================================================
        -- 1. CREATE PROJECT
        -- =====================================================================================

        -- Get next project ID
        SELECT COALESCE(MAX((data::jsonb->>'id')::int), 0) + 1 INTO v_project_id
        FROM vibe.documents
        WHERE client_id = 0
        AND collection = 'vibe_agents'
        AND table_name = 'vibe_projects';

        -- Insert Hello World project
        INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, updated_at)
        VALUES (
            0,
            'vibe_agents',
            'vibe_projects',
            jsonb_build_object(
                'id', v_project_id,
                'owner_user_id', 1,
                'client_id', 0,
                'name', 'Hello World',
                'description', 'Your first Vibe Agents project - explore agents, mail, and task management',
                'is_active', true,
                'created_at', NOW(),
                'updated_at', NOW()
            ),
            NOW(),
            NOW()
        );

        RAISE NOTICE 'Created project: Hello World (ID: %)', v_project_id;

        -- =====================================================================================
        -- 2. CREATE STARTER AGENTS (IN BULLPEN)
        -- =====================================================================================

        -- Get base agent ID
        SELECT COALESCE(MAX((data::jsonb->>'id')::int), 0) + 1 INTO v_coordinator_agent_id
        FROM vibe.documents
        WHERE client_id = 0
        AND collection = 'vibe_agents'
        AND table_name = 'agent_profiles';

        v_backend_agent_id := v_coordinator_agent_id + 1;
        v_frontend_agent_id := v_coordinator_agent_id + 2;
        v_qa_agent_id := v_coordinator_agent_id + 3;
        v_vibesql_agent_id := v_coordinator_agent_id + 4;

        -- Coordinator Agent (Primary role)
        INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, updated_at)
        VALUES (
            0,
            'vibe_agents',
            'agent_profiles',
            jsonb_build_object(
                'id', v_coordinator_agent_id,
                'project_id', v_project_id,
                'status', 'bullpen',
                'role', 'primary',
                'name', 'CoordinatorAgent',
                'display_name', '[AI] Coordinator',
                'username', 'coordinator@agents.demo.local',
                'role_preset', 'coordinator',
                'model', 'claude-opus-4',
                'tier', 'starter',
                'is_shared', false,
                'created_at', NOW(),
                'updated_at', NOW()
            ),
            NOW(),
            NOW()
        );

        -- Backend Developer Agent
        INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, updated_at)
        VALUES (
            0,
            'vibe_agents',
            'agent_profiles',
            jsonb_build_object(
                'id', v_backend_agent_id,
                'project_id', v_project_id,
                'status', 'bullpen',
                'role', 'support',
                'name', 'BackendAgent',
                'display_name', '[AI] Backend Developer',
                'username', 'backend@agents.demo.local',
                'role_preset', 'backend-developer',
                'model', 'claude-sonnet-4',
                'tier', 'starter',
                'is_shared', false,
                'created_at', NOW(),
                'updated_at', NOW()
            ),
            NOW(),
            NOW()
        );

        -- Frontend Developer Agent
        INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, updated_at)
        VALUES (
            0,
            'vibe_agents',
            'agent_profiles',
            jsonb_build_object(
                'id', v_frontend_agent_id,
                'project_id', v_project_id,
                'status', 'bullpen',
                'role', 'support',
                'name', 'FrontendAgent',
                'display_name', '[AI] Frontend Developer',
                'username', 'frontend@agents.demo.local',
                'role_preset', 'frontend-developer',
                'model', 'claude-sonnet-4',
                'tier', 'starter',
                'is_shared', false,
                'created_at', NOW(),
                'updated_at', NOW()
            ),
            NOW(),
            NOW()
        );

        -- QA Reviewer Agent
        INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, updated_at)
        VALUES (
            0,
            'vibe_agents',
            'agent_profiles',
            jsonb_build_object(
                'id', v_qa_agent_id,
                'project_id', v_project_id,
                'status', 'bullpen',
                'role', 'support',
                'name', 'QAAgent',
                'display_name', '[AI] QA Reviewer',
                'username', 'qa@agents.demo.local',
                'role_preset', 'code-reviewer',
                'model', 'claude-sonnet-4',
                'tier', 'starter',
                'is_shared', false,
                'created_at', NOW(),
                'updated_at', NOW()
            ),
            NOW(),
            NOW()
        );

        -- VibeSqlPert Agent (Specialist - ACTIVE and platform-paid)
        INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, updated_at)
        VALUES (
            0,
            'vibe_agents',
            'agent_profiles',
            jsonb_build_object(
                'id', v_vibesql_agent_id,
                'project_id', v_project_id,
                'status', 'active',
                'role', 'specialist',
                'name', 'VibeSqlPert',
                'display_name', '[AI] Vibe SQL Helper',
                'username', 'vibesqlpert@agents.demo.local',
                'role_preset', 'custom',
                'model', 'groq-llama',
                'tier', 'platform',
                'is_shared', false,
                'is_platform_paid', true,
                'created_at', NOW(),
                'updated_at', NOW()
            ),
            NOW(),
            NOW()
        );

        RAISE NOTICE 'Created 4 starter agents in bullpen + VibeSqlPert (active, platform-paid)';

        -- =====================================================================================
        -- 3. CREATE SAMPLE SPEC DOCUMENT
        -- =====================================================================================

        -- Get next document ID
        SELECT COALESCE(MAX((data::jsonb->>'document_id')::int), 0) + 1 INTO v_spec_doc_id
        FROM vibe.documents
        WHERE client_id = 0
        AND collection = 'vibe_agents'
        AND table_name = 'agent_documents';

        -- Insert Getting Started spec
        INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, updated_at)
        VALUES (
            0,
            'vibe_agents',
            'agent_documents',
            jsonb_build_object(
                'document_id', v_spec_doc_id,
                'agent_id', v_coordinator_agent_id,
                'document_type', 'spec',
                'filename', 'getting-started.md',
                'mime_type', 'text/markdown',
                'size_bytes', 1024,
                'content_md', E'# Getting Started with Vibe Agents\n\nWelcome to your first Vibe Agents project!\n\n## What You Can Do\n\n1. **Activate Agents** - Move agents from the bullpen to active status\n2. **Send Mail** - Coordinate with agents using the built-in mail system\n3. **Manage Tasks** - Create and track kanban tasks\n4. **Run Autonomously** - Let agents work on their own with stop conditions\n\n## Quick Start\n\n### Step 1: Activate an Agent\n\nGo to your bullpen and activate the CoordinatorAgent. This agent will help coordinate your project.\n\n### Step 2: Send Your First Mail\n\nTry sending a mail message to your active agent:\n```\nTo: CoordinatorAgent\nSubject: Hello!\nBody: What can you help me with?\n```\n\n### Step 3: Create a Task\n\nAdd a new kanban task to track your work:\n- Title: Set up my first project\n- Status: backlog\n- Milestone: Getting Started\n\n## Next Steps\n\n- Explore the agent profiles and customize them\n- Create your own project\n- Set up autonomy mode for hands-free operation\n- Add documents and specs for your agents to reference\n\nHave fun building with Vibe Agents!',
                'title', 'Getting Started with Vibe Agents',
                'version', 1,
                'tags', ARRAY['tutorial', 'getting-started'],
                'created_by', 1,
                'created_at', NOW(),
                'updated_at', NOW(),
                'is_deleted', false
            ),
            NOW(),
            NOW()
        );

        RAISE NOTICE 'Created Getting Started spec document (ID: %)', v_spec_doc_id;

        -- =====================================================================================
        -- 4. CREATE SAMPLE KANBAN TASKS
        -- =====================================================================================

        -- Task 1: Read the Getting Started guide
        INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, updated_at)
        VALUES (
            0,
            'vibe_agents',
            'kanban_tasks',
            jsonb_build_object(
                'task_id', 1,
                'project_id', v_project_id,
                'spec_id', v_spec_doc_id,
                'milestone', 'Getting Started',
                'title', 'Read the Getting Started guide',
                'description', 'Review the Getting Started spec to learn about Vibe Agents',
                'status', 'backlog',
                'priority', 'medium',
                'created_at', NOW(),
                'updated_at', NOW()
            ),
            NOW(),
            NOW()
        );

        -- Task 2: Activate your first agent
        INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, updated_at)
        VALUES (
            0,
            'vibe_agents',
            'kanban_tasks',
            jsonb_build_object(
                'task_id', 2,
                'project_id', v_project_id,
                'spec_id', v_spec_doc_id,
                'milestone', 'Getting Started',
                'title', 'Activate your first agent',
                'description', 'Move CoordinatorAgent from bullpen to active status',
                'status', 'backlog',
                'priority', 'high',
                'created_at', NOW(),
                'updated_at', NOW()
            ),
            NOW(),
            NOW()
        );

        -- Task 3: Send your first mail
        INSERT INTO vibe.documents (client_id, collection, table_name, data, created_at, updated_at)
        VALUES (
            0,
            'vibe_agents',
            'kanban_tasks',
            jsonb_build_object(
                'task_id', 3,
                'project_id', v_project_id,
                'spec_id', v_spec_doc_id,
                'milestone', 'Getting Started',
                'title', 'Send your first mail',
                'description', 'Try sending a mail message to your active agent',
                'status', 'backlog',
                'priority', 'medium',
                'created_at', NOW(),
                'updated_at', NOW()
            ),
            NOW(),
            NOW()
        );

        RAISE NOTICE 'Created 3 sample kanban tasks';

        RAISE NOTICE '=== Hello World seed data creation complete! ===';

    ELSE
        RAISE NOTICE 'Hello World project already exists - skipping seed data';
    END IF;

END $$;

-- Verify seed data
SELECT
    'Projects' as table_name,
    COUNT(*) as count,
    jsonb_agg(data::jsonb->>'name') as names
FROM vibe.documents
WHERE client_id = 0
AND collection = 'vibe_agents'
AND table_name = 'vibe_projects'
AND deleted_at IS NULL

UNION ALL

SELECT
    'Agents' as table_name,
    COUNT(*) as count,
    jsonb_agg(data::jsonb->>'name') as names
FROM vibe.documents
WHERE client_id = 0
AND collection = 'vibe_agents'
AND table_name = 'agent_profiles'
AND data::jsonb->>'project_id' = (
    SELECT data::jsonb->>'id'
    FROM vibe.documents
    WHERE client_id = 0
    AND collection = 'vibe_agents'
    AND table_name = 'vibe_projects'
    AND data::jsonb->>'name' = 'Hello World'
    LIMIT 1
)

UNION ALL

SELECT
    'Documents' as table_name,
    COUNT(*) as count,
    jsonb_agg(data::jsonb->>'title') as names
FROM vibe.documents
WHERE client_id = 0
AND collection = 'vibe_agents'
AND table_name = 'agent_documents'
AND data::jsonb->>'title' = 'Getting Started with Vibe Agents'

UNION ALL

SELECT
    'Tasks' as table_name,
    COUNT(*) as count,
    jsonb_agg(data::jsonb->>'title') as names
FROM vibe.documents
WHERE client_id = 0
AND collection = 'vibe_agents'
AND table_name = 'kanban_tasks'
AND data::jsonb->>'project_id' = (
    SELECT data::jsonb->>'id'
    FROM vibe.documents
    WHERE client_id = 0
    AND collection = 'vibe_agents'
    AND table_name = 'vibe_projects'
    AND data::jsonb->>'name' = 'Hello World'
    LIMIT 1
);
