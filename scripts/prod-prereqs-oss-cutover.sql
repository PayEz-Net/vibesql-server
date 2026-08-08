-- ===========================================================================
-- PROD PREREQUISITES for replacing PayEz.VibeSql.Server.Api with the OSS build
-- ===========================================================================
-- RUN THIS BEFORE APPLYING THE NEW IMAGE. Not after, and not alongside.
--
-- The OSS build fails closed in two places the fork did not:
--   * every document write sets app.client_id and is subject to RLS
--   * every document insert writes an audit row, and the audit row's
--     admin_user_id is NOT NULL and must reference a real user IN THE SAME TENANT
-- If a tenant has no type='system' user, the audit INSERT matches no row, writes
-- nothing, and the controller throws rather than silently skipping the audit.
-- That is intentional — a silent audit gap is the defect this work exists to
-- remove — but it means seeding must PRECEDE the deploy or writes start failing
-- on prod tenants the moment the pod comes up.
--
-- Run as a role that owns the vibe schema (NOT as vsql_server_user; it
-- deliberately has no DDL rights).
-- Idempotent: safe to re-run.
-- ===========================================================================


-- ---------------------------------------------------------------------------
-- 1. Least-privilege application role
-- ---------------------------------------------------------------------------
-- The fork connected with a role that bypassed RLS, which is why the missing
-- tenant context in the write path was invisible for months. Superusers ignore
-- RLS entirely, and FORCE only defeats the OWNER exemption — so the
-- tenant_isolation policies on all eight tables were decorative for that
-- connection. This role makes them real.
--
-- Deliberately: NOSUPERUSER, NOBYPASSRLS, NOCREATEDB, NOCREATEROLE, no DDL.
-- Consequence to be aware of: schema provisioning and system-user seeding are
-- therefore MANUAL — VibeSchemaInitializer exists in the OSS repo but is NOT
-- registered, precisely because it does DDL this role cannot perform.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'vsql_server_user') THEN
        CREATE ROLE vsql_server_user LOGIN
            NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS
            PASSWORD 'REPLACE_ME_BEFORE_RUNNING';
        RAISE NOTICE 'created role vsql_server_user';
    ELSE
        RAISE NOTICE 'role vsql_server_user already exists — leaving password alone';
    END IF;
END $$;

GRANT CONNECT ON DATABASE payez_vibe TO vsql_server_user;
GRANT USAGE   ON SCHEMA vibe        TO vsql_server_user;

-- Data access on the product tables.
GRANT SELECT, INSERT, UPDATE, DELETE
    ON ALL TABLES IN SCHEMA vibe TO vsql_server_user;

-- Sequences: the service inserts rows whose PKs come from sequences, so USAGE
-- (nextval) is required. SELECT is included for currval/lastval paths.
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA vibe TO vsql_server_user;

-- APPEND-ONLY AUDIT. The service may write audit rows and read them back; it may
-- NOT rewrite or delete its own history. This is the property that makes the
-- audit trail worth having — revoke after the blanket grant above.
REVOKE UPDATE, DELETE ON vibe.audit_logs FROM vsql_server_user;

-- Future tables/sequences created by the owner inherit the same shape.
ALTER DEFAULT PRIVILEGES IN SCHEMA vibe
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO vsql_server_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA vibe
    GRANT USAGE, SELECT ON SEQUENCES TO vsql_server_user;


-- ---------------------------------------------------------------------------
-- 2. Audit integrity constraints
-- ---------------------------------------------------------------------------
-- Three tiers, by what the existing data can actually support:
--   (a) columns already populated on every row -> NOT NULL as-is
--   (b) columns partially populated           -> backfill a sentinel, then NOT NULL
--   (c) columns genuinely conditional         -> stay nullable, with a CHECK that
--                                                makes the condition explicit
-- 'unrecorded' is a deliberate sentinel, not a guess: it says "this event
-- happened before the column was required", which is different from NULL meaning
-- "we don't know if it applies".

UPDATE vibe.audit_logs SET admin_email = 'unrecorded' WHERE admin_email IS NULL;
UPDATE vibe.audit_logs SET target_type = 'unrecorded' WHERE target_type IS NULL;
UPDATE vibe.audit_logs SET target_id   = 'unrecorded' WHERE target_id   IS NULL;
UPDATE vibe.audit_logs SET user_agent  = 'unrecorded' WHERE user_agent  IS NULL;

ALTER TABLE vibe.audit_logs
    ALTER COLUMN client_id     SET NOT NULL,
    ALTER COLUMN admin_user_id SET NOT NULL,
    ALTER COLUMN admin_email   SET NOT NULL,
    ALTER COLUMN category      SET NOT NULL,
    ALTER COLUMN action        SET NOT NULL,
    ALTER COLUMN target_type   SET NOT NULL,
    ALTER COLUMN target_id     SET NOT NULL,
    ALTER COLUMN is_success    SET NOT NULL,
    ALTER COLUMN user_agent    SET NOT NULL,
    ALTER COLUMN created_at    SET NOT NULL;

-- A failure that does not say why is not an audit record. error_message stays
-- nullable because it is meaningless on success — the CHECK is what makes that
-- conditional explicit rather than merely permitted.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'chk_failure_has_reason'
    ) THEN
        ALTER TABLE vibe.audit_logs
            ADD CONSTRAINT chk_failure_has_reason
            CHECK (is_success OR error_message IS NOT NULL);
    END IF;
END $$;


-- ---------------------------------------------------------------------------
-- 3. Per-tenant system user  ***THE ONE THAT BLOCKS THE DEPLOY***
-- ---------------------------------------------------------------------------
-- Document inserts can legitimately carry no authenticated caller (scheduled
-- jobs, migrations, system-initiated writes). audit_logs.admin_user_id is NOT
-- NULL and must resolve to a real user in the SAME tenant — user_id is unique
-- PER TENANT, not globally, so an unscoped lookup would happily attribute one
-- tenant's audit row to another tenant's person.
--
-- user_id = 1 is the reserved system id, taken when free; a tenant where 1 is
-- already occupied gets max+1 instead.
--
-- idp_user_id is deliberately omitted: a service account has no IDP identity.
-- The collection's `required` list is enforced at the API layer, not here, and
-- this writes the row directly — the same path by which client 9's original
-- system user was created. Known asymmetry: you cannot create a system user
-- through the public API today.

DO $$
DECLARE
    t      RECORD;
    new_id INT;
BEGIN
    FOR t IN
        SELECT DISTINCT client_id
        FROM vibe.documents
        WHERE collection = 'vibe_app' AND table_name = 'users' AND deleted_at IS NULL
    LOOP
        IF NOT EXISTS (
            SELECT 1 FROM vibe.documents
            WHERE client_id = t.client_id
              AND collection = 'vibe_app' AND table_name = 'users'
              AND deleted_at IS NULL
              AND data->>'type' = 'system'
        ) THEN
            SELECT CASE
                     WHEN EXISTS (
                       SELECT 1 FROM vibe.documents
                       WHERE client_id = t.client_id
                         AND collection = 'vibe_app' AND table_name = 'users'
                         AND deleted_at IS NULL
                         AND (data->>'user_id')::int = 1)
                     THEN COALESCE(MAX((data->>'user_id')::int), 0) + 1
                     ELSE 1
                   END
              INTO new_id
              FROM vibe.documents
             WHERE client_id = t.client_id
               AND collection = 'vibe_app' AND table_name = 'users'
               AND deleted_at IS NULL;

            INSERT INTO vibe.documents
                (client_id, collection, table_name, data, created_at, created_by)
            VALUES (
                t.client_id, 'vibe_app', 'users',
                jsonb_build_object(
                    'name',       'vibe_app_system',
                    'type',       'system',
                    'email',      'system@vibe_app.vibe',
                    'user_id',    new_id,
                    'created_by', new_id,
                    'updated_by', new_id
                ),
                now(), new_id);

            RAISE NOTICE 'seeded system user_id=% for client_id=%', new_id, t.client_id;
        END IF;
    END LOOP;
END $$;


-- ---------------------------------------------------------------------------
-- 4. VERIFY BEFORE DEPLOYING  — every row must read OK
-- ---------------------------------------------------------------------------
SELECT 'role exists' AS check,
       CASE WHEN EXISTS (SELECT 1 FROM pg_roles WHERE rolname='vsql_server_user')
            THEN 'OK' ELSE 'FAIL' END AS result
UNION ALL
SELECT 'role cannot bypass RLS',
       CASE WHEN (SELECT NOT rolbypassrls AND NOT rolsuper FROM pg_roles
                  WHERE rolname='vsql_server_user') THEN 'OK' ELSE 'FAIL' END
UNION ALL
SELECT 'audit is append-only for the service',
       CASE WHEN NOT has_table_privilege('vsql_server_user','vibe.audit_logs','UPDATE')
             AND NOT has_table_privilege('vsql_server_user','vibe.audit_logs','DELETE')
            THEN 'OK' ELSE 'FAIL' END
UNION ALL
SELECT 'every tenant with users has a system user',
       CASE WHEN NOT EXISTS (
              SELECT 1
              FROM (SELECT DISTINCT client_id FROM vibe.documents
                     WHERE collection='vibe_app' AND table_name='users'
                       AND deleted_at IS NULL) c
              WHERE NOT EXISTS (
                  SELECT 1 FROM vibe.documents d
                   WHERE d.client_id = c.client_id
                     AND d.collection='vibe_app' AND d.table_name='users'
                     AND d.deleted_at IS NULL AND d.data->>'type'='system')
            ) THEN 'OK' ELSE 'FAIL — deploy will throw on insert for those tenants' END
UNION ALL
SELECT 'failure-must-say-why constraint',
       CASE WHEN EXISTS (SELECT 1 FROM pg_constraint
                          WHERE conname='chk_failure_has_reason')
            THEN 'OK' ELSE 'FAIL' END;
