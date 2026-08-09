-- ─────────────────────────────────────────────────────────────────────────────
-- verify-rls-isolation.sql — acceptance test for the tenant isolation declared
-- in base-schema.v2.sql.
--
-- WHY THIS EXISTS. `rowsecurity = true` is not evidence of isolation. A policy
-- with USING and no WITH CHECK reports rowsecurity = true and still lets one
-- tenant write a shared row that every other tenant reads back. A flag check
-- cannot tell a working policy from a broken one. Only a refusal can.
--
-- So this script does not inspect catalogue flags and call it a pass. It
-- attempts the write that must fail, and fails loudly if the database allows it.
--
-- RUN AS: a role that CANNOT bypass RLS. Superusers and roles with BYPASSRLS
-- skip policies entirely and will make a broken schema look perfect. The script
-- refuses to run if the effective role can bypass, because a green result from
-- the wrong role is worse than no result.
--
--   psql -d <db> -v probe_role=<non_superuser_role> -f verify-rls-isolation.sql
--
-- Exits non-zero on failure (ON_ERROR_STOP), so it is usable as a CI gate.
-- Creates and drops its own schema; touches no application table.
-- ─────────────────────────────────────────────────────────────────────────────

\set ON_ERROR_STOP on

BEGIN;

-- Make the psql variable visible to the PL/pgSQL blocks below.
SELECT set_config('vibe.probe_role', :'probe_role', true);

-- ── 0. Refuse to produce a meaningless pass ──────────────────────────────────
DO $$
DECLARE r record;
BEGIN
    SELECT rolsuper, rolbypassrls INTO r
    FROM pg_roles WHERE rolname = current_setting('vibe.probe_role', true);

    IF r IS NULL THEN
        RAISE EXCEPTION 'probe role % does not exist', current_setting('vibe.probe_role', true);
    END IF;
    IF r.rolsuper OR r.rolbypassrls THEN
        RAISE EXCEPTION
            'probe role % can BYPASS RLS (rolsuper=%, rolbypassrls=%) -- this test would pass on a broken schema. Use a plain application role.',
            current_setting('vibe.probe_role', true), r.rolsuper, r.rolbypassrls;
    END IF;
END $$;

-- ── 1. Build the isolation shape under test ──────────────────────────────────
DROP SCHEMA IF EXISTS rls_acceptance CASCADE;
CREATE SCHEMA rls_acceptance;

CREATE TABLE rls_acceptance.probe (client_id int NOT NULL, note text);
ALTER TABLE rls_acceptance.probe ENABLE ROW LEVEL SECURITY;
ALTER TABLE rls_acceptance.probe FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON rls_acceptance.probe
    USING      (client_id = current_setting('app.client_id', true)::integer OR client_id = 0)
    WITH CHECK (client_id = current_setting('app.client_id', true)::integer);

GRANT USAGE ON SCHEMA rls_acceptance TO :"probe_role";
GRANT SELECT, INSERT ON rls_acceptance.probe TO :"probe_role";

-- ── 2. Exercise it as the unprivileged role ──────────────────────────────────
SET LOCAL ROLE :"probe_role";
SET LOCAL app.client_id = '9';

DO $$
DECLARE
    leaked   int;
    refused  boolean := false;
BEGIN
    -- 2a. A tenant MUST be able to write its own rows. If this fails the policy
    --     is too strict and the test is telling you about a real outage.
    INSERT INTO rls_acceptance.probe VALUES (9, 'tenant 9 own row');

    -- 2b. THE LOAD-BEARING ASSERTION: a tenant must NOT be able to write a
    --     shared (client_id = 0) row. If this succeeds, the write check is
    --     inheriting USING and isolation is broken.
    BEGIN
        INSERT INTO rls_acceptance.probe VALUES (0, 'injected by tenant 9');
    EXCEPTION WHEN insufficient_privilege THEN
        refused := true;
    END;

    IF NOT refused THEN
        RAISE EXCEPTION
            'RLS ISOLATION BROKEN: tenant 9 inserted a client_id = 0 row. The policy is missing WITH CHECK, so USING is being reused as the write check and "OR client_id = 0" has become a write permission.';
    END IF;

    -- 2c. And confirm the read side from a DIFFERENT tenant. Even with the write
    --     refused, a stray shared row must not be how tenants see each other.
    PERFORM set_config('app.client_id', '7', true);
    SELECT count(*) INTO leaked
    FROM rls_acceptance.probe WHERE note = 'injected by tenant 9';

    IF leaked > 0 THEN
        RAISE EXCEPTION
            'RLS ISOLATION BROKEN: tenant 7 can read % row(s) written by tenant 9.', leaked;
    END IF;

    RAISE NOTICE 'PASS: cross-tenant write REFUSED and cross-tenant read returned nothing.';
END $$;

RESET ROLE;
DROP SCHEMA rls_acceptance CASCADE;

COMMIT;
