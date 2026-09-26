-- =====================================================================================
-- Agent Mail Performance Indexes, part 2
-- =====================================================================================
-- Purpose: complete the agent-mail index set on vibe.documents, so a database that is
--          provisioned OR converged by the .NET path ends up with the same indexes the
--          hand-run AKS/migrations build on the live database.
-- Date:    2026-09-27
-- Author:  DotNetPert-Scout (PAY-1862 step 4; Jon 15:15 via rigpert 65206)
-- Database: vibe   Schema: vibe
--
-- =====================================================================================
-- *** READ THIS BEFORE ASSUMING THIS FILE RUNS. ***
--
-- MEASURED 2026-09-27 against origin/master:
--   * NOTHING on master reads Data/Migrations/*.sql. The csproj embeds them
--     (<EmbeddedResource Include="Data\Migrations\**\*.sql" />) but `git grep
--     GetManifestResource|Data.Migrations` over *.cs returns NOTHING, and there is no EF
--     migration (no Migrations/*.cs, and VibeDbContext is not registered at startup).
--     VibeSQL.Server/Program.cs registers exactly ONE hosted service: JwksCache.
--   * scripts/base-schema.v2.sql does NOT exist on origin/master; it lives only on the
--     unmerged branch fix/wire-audit-log-repository, where VibeSchemaInitializer.cs (the
--     class that would run it) carries a docstring saying it is NOT REGISTERED.
-- So today this file, like 20260314_AgentMailIndexes.sql before it, is INERT on master.
-- It is written to be CORRECT the moment the install/convergence path exists, and to be
-- safe to hand-run now. Do not read its presence as "this ran".
--
-- WHY PLAIN CREATE INDEX, NOT CONCURRENTLY:
--   The intended convergence path (VibeSchemaInitializer / a multi-statement NpgsqlCommand)
--   runs its script inside an IMPLICIT TRANSACTION, and CREATE INDEX CONCURRENTLY is
--   illegal there - it throws and the pod would crash-loop. So the converged form is plain.
--   The CONCURRENTLY forms for the LIVE table (no write-lock during the build) live in
--   PAY-1862 steps 1-3: PayEz-Core/AKS/migrations/02_vibe_AgentMailUnreadIndex.sql and
--   20260924_vibe_AgentMailInboxProjectIdBackfill.sql. Same definitions, different lock
--   trade-off. Both are IF NOT EXISTS, so applying one and then the other is a no-op.
--
-- SCOPE NOTE: agent_mail state lives in the SHARED table vibe.documents (tenants do not get
-- their own tables), so these are database-wide partial indexes: create once per database
-- and every tenant, present and future, is covered.
-- =====================================================================================

-- Index 4: unread-count lookups. Predicate is NOT a subset of index 1, so it is a separate
-- index (partial on read_at IS NULL), not a redundant duplicate.
CREATE INDEX IF NOT EXISTS idx_agent_mail_inbox_unread
ON vibe.documents (client_id, ((data->>'agent_id')::int), created_at DESC)
WHERE collection = 'agent_mail'
  AND table_name = 'agent_mail_inbox'
  AND deleted_at IS NULL
  AND (data->>'read_at') IS NULL;

-- Index 5: project-filtered inbox (PAY-1821). Leading columns match idx_agent_mail_inbox_by_agent
-- so this serves the project-filtered read without a second scan of the same predicate.
CREATE INDEX IF NOT EXISTS idx_agent_mail_inbox_by_agent_project
ON vibe.documents (client_id, ((data->>'agent_id')::int), ((data->>'project_id')::int))
WHERE collection = 'agent_mail'
  AND table_name = 'agent_mail_inbox'
  AND deleted_at IS NULL;

-- -------------------------------------------------------------------------------------
-- NOT INCLUDED, deliberately: the six idx_agent_mail_pins_* indexes. They are on a SEPARATE
-- table (agent_mail_pins), which exists in NO schema this repository provisions - it is
-- created only by PayEz.Services/PayEz.Infrastructure/Migrations/Vibe/20260127_add_agent_mail_pins.sql,
-- and `git grep agent_mail_pins origin/master` returns nothing. Adding its indexes here would
-- be a second definition of a table this file does not create. That is a separate decision.
-- -------------------------------------------------------------------------------------
