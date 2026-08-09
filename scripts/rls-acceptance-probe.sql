-- =====================================================================================
-- STANDING RLS ACCEPTANCE TEST (188798)
--
-- WHY THIS EXISTS
-- `base-schema.v2.sql` shipped 7 policies with USING and ZERO WITH CHECK. Applying that
-- schema to "fix" isolation would have INSTALLED a cross-tenant channel.
--
-- THE MECHANISM, MEASURED RATHER THAN ASSUMED (this probe was run against both shapes)
-- "No WITH CHECK means writes are unrestricted" is NOT what PostgreSQL does, and saying
-- so overstates one thing while missing the real hole. When WITH CHECK is omitted, the
-- USING expression is applied to writes as well. So a plain cross-tenant INSERT (tenant
-- A writing a row owned by tenant B) IS refused even by the broken policy -- VERIFIED.
--
-- The actual channel is the `OR client_id = 0` disjunct. Used as the implicit write
-- check, it lets ANY tenant INSERT a row with client_id = 0 -- and because that same
-- disjunct appears in every USING clause, that row is then READABLE BY EVERY TENANT.
-- Not a leak between two tenants: a broadcast channel, writable by anyone.
-- VERIFIED: broken shape accepted the client_id=0 write; the 93 shape refused it.
--
-- This is why the prescribed fix -- WITH CHECK (client_id = current_setting(...)) with
-- NO `OR client_id = 0` -- is exactly right: it removes that disjunct from the write
-- path while leaving global rows readable. The fix was correct; the reasoning under it
-- was not, and a probe that encodes the wrong mechanism stops catching the right thing.
--
-- WHY FLAG CHECKS CANNOT CATCH IT
-- `rowsecurity = true` is TRUE for a table with a correct policy and TRUE for a table
-- with a write-open one. ENABLE + FORCE + a policy named `tenant_isolation` were all
-- present on every affected table. ONLY REFUSAL DISCRIMINATES. This probe therefore
-- asserts observed behaviour and never reads a catalog flag as evidence of safety.
--
-- HOW TO RUN (the role matters more than the SQL)
--   psql "postgres://vibe_rls_user:...@host:port/db" -f scripts/rls-acceptance-probe.sql
--
-- Everything runs inside a transaction that ALWAYS rolls back. No DDL, no residue.
-- =====================================================================================

\set ON_ERROR_STOP on

BEGIN;

DO $probe$
DECLARE
    v_super   boolean;
    v_bypass  boolean;
    r         record;
    -- Synthetic tenants, far outside real client_id space. Never committed.
    tenant_a  constant integer := 990001;
    tenant_b  constant integer := 990002;
    n         integer;
    pass_ct   integer := 0;
    fail_ct   integer := 0;
    skip_ct   integer := 0;
    nocov_ct  integer := 0;
    findings  text[] := '{}';
BEGIN
    -- =================================================================================
    -- PRECONDITION: THE ROLE. This is the single choice that separates proof from a
    -- comfortable lie. A superuser (or any role with rolbypassrls) BYPASSES RLS
    -- entirely, so every probe below would pass while proving nothing whatsoever.
    -- We ABORT rather than warn: a probe that can silently run in a mode where it
    -- cannot fail is worse than no probe, because its green is trusted.
    -- =================================================================================
    SELECT rolsuper, rolbypassrls INTO v_super, v_bypass
      FROM pg_roles WHERE rolname = current_user;

    IF v_super OR v_bypass THEN
        RAISE EXCEPTION
          'ABORT: connected as "%" (rolsuper=%, rolbypassrls=%). RLS IS BYPASSED FOR THIS ROLE, so every assertion below would report a CONFIDENT FALSE NEGATIVE. Re-run as a non-superuser role with rolbypassrls = false (e.g. vibe_rls_user).',
          current_user, v_super, v_bypass;
    END IF;

    RAISE NOTICE '=== RLS ACCEPTANCE PROBE ===';
    RAISE NOTICE 'role=% (rolsuper=false, rolbypassrls=false verified)', current_user;
    RAISE NOTICE '';

    -- =================================================================================
    -- TARGETS ARE DISCOVERED, NEVER ENUMERATED.
    -- A hardcoded list of 7 tables is the original defect in a new costume: it goes
    -- stale the moment someone adds table 8, and it reports green about a table it
    -- was never told to look at. We ask the catalog instead.
    -- =================================================================================
    FOR r IN
        SELECT c.relname AS tbl
          FROM pg_class c
          JOIN pg_namespace ns ON ns.oid = c.relnamespace
         WHERE ns.nspname = 'vibe'
           AND c.relkind = 'r'
           AND c.relrowsecurity
           AND EXISTS (SELECT 1 FROM pg_attribute a
                        WHERE a.attrelid = c.oid AND a.attname = 'client_id' AND a.attnum > 0)
         ORDER BY c.relname
    LOOP
        PERFORM set_config('app.client_id', tenant_a::text, true);

        -- -----------------------------------------------------------------------------
        -- POSITIVE CONTROL, RUN FIRST AND TREATED AS DECISIVE.
        -- A refused INSERT only means "RLS refused it" if the SAME insert SUCCEEDS when
        -- the tenant matches. Otherwise a NOT NULL column, an FK, or a typo produces a
        -- refusal we would score as isolation -- a false PASS. If the control cannot
        -- insert, this table is reported UNPROVEN, never PASS.
        -- -----------------------------------------------------------------------------
        BEGIN
            EXECUTE format('INSERT INTO vibe.%I (client_id) VALUES (%L)', r.tbl, tenant_a);
        EXCEPTION WHEN OTHERS THEN
            skip_ct := skip_ct + 1;
            RAISE NOTICE '  ?  % UNPROVEN - control INSERT failed (%). Probe cannot speak to this table.',
                          r.tbl, SQLERRM;
            CONTINUE;
        END;

        -- -----------------------------------------------------------------------------
        -- HALF 1, THE WRITE. Missing WITH CHECK shows up HERE and nowhere else.
        -- -----------------------------------------------------------------------------
        BEGIN
            EXECUTE format('INSERT INTO vibe.%I (client_id) VALUES (%L)', r.tbl, tenant_b);
            fail_ct := fail_ct + 1;
            findings := findings || format('%s: CROSS-TENANT WRITE ACCEPTED (tenant %s wrote a row owned by %s) - policy has no WITH CHECK', r.tbl, tenant_a, tenant_b);
            RAISE NOTICE '  X  % FAIL - cross-tenant INSERT ACCEPTED', rpad(r.tbl, 30);
        EXCEPTION WHEN insufficient_privilege THEN
            -- refused, as required
            BEGIN
                -- Broadcast variant: client_id = 0 is globally READABLE under every
                -- USING clause here, so an accepted 0-write is a channel to ALL tenants,
                -- not just one.
                EXECUTE format('INSERT INTO vibe.%I (client_id) VALUES (0)', r.tbl);
                fail_ct := fail_ct + 1;
                findings := findings || format('%s: BROADCAST WRITE ACCEPTED (client_id=0 is readable by EVERY tenant)', r.tbl);
                RAISE NOTICE '  X  % FAIL - client_id=0 broadcast INSERT ACCEPTED', rpad(r.tbl, 30);
            EXCEPTION WHEN insufficient_privilege THEN
                -- -------------------------------------------------------------------
                -- HALF 2, THE READ-BACK AS A DIFFERENT TENANT.
                -- An accepted write is a gap; another tenant READING the row is a
                -- complete channel. Most probes stop at the first half.
                -- -------------------------------------------------------------------
                PERFORM set_config('app.client_id', tenant_b::text, true);
                EXECUTE format('SELECT count(*) FROM vibe.%I WHERE client_id = %L', r.tbl, tenant_a) INTO n;
                IF n > 0 THEN
                    fail_ct := fail_ct + 1;
                    findings := findings || format('%s: CROSS-TENANT READ (tenant %s saw %s row(s) owned by %s)', r.tbl, tenant_b, n, tenant_a);
                    RAISE NOTICE '  X  % FAIL - tenant % read % row(s) of tenant %', rpad(r.tbl, 30), tenant_b, n, tenant_a;
                ELSE
                    pass_ct := pass_ct + 1;
                    RAISE NOTICE '  OK % write REFUSED, broadcast REFUSED, read-back 0 rows', rpad(r.tbl, 30);
                END IF;
                PERFORM set_config('app.client_id', tenant_a::text, true);
            END;
        END;
    END LOOP;

    -- =================================================================================
    -- COVERAGE, REPORTED SEPARATELY AND NEVER SILENTLY.
    -- 93 lesson: 19 of 29 tables had RLS and 10 had none. A table with NO policy is not
    -- a passing table -- it is an unasked question, and it must not be invisible just
    -- because the loop above had nothing to iterate over.
    -- =================================================================================
    RAISE NOTICE '';
    FOR r IN
        SELECT c.relname AS tbl
          FROM pg_class c
          JOIN pg_namespace ns ON ns.oid = c.relnamespace
         WHERE ns.nspname = 'vibe' AND c.relkind = 'r' AND NOT c.relrowsecurity
           AND EXISTS (SELECT 1 FROM pg_attribute a
                        WHERE a.attrelid = c.oid AND a.attname = 'client_id' AND a.attnum > 0)
         ORDER BY c.relname
    LOOP
        nocov_ct := nocov_ct + 1;
        RAISE NOTICE '  !  % NO RLS AT ALL (has client_id, no row security)', rpad(r.tbl, 30);
    END LOOP;

    RAISE NOTICE '';
    RAISE NOTICE '=== isolated=%  FAILED=%  unproven=%  no-RLS=% ===', pass_ct, fail_ct, skip_ct, nocov_ct;

    IF fail_ct > 0 THEN
        RAISE EXCEPTION E'RLS ACCEPTANCE FAILED - % cross-tenant channel(s):\n  %',
              fail_ct, array_to_string(findings, E'\n  ');
    END IF;

    IF pass_ct = 0 THEN
        -- Zero passes is not success. An empty run means wrong database, wrong schema,
        -- or every table unproven -- all of which would otherwise exit 0 and read green.
        RAISE EXCEPTION 'RLS ACCEPTANCE INCONCLUSIVE - not one table was actually proven isolated (unproven=%, no-RLS=%). Green here would be meaningless.', skip_ct, nocov_ct;
    END IF;
END
$probe$;

-- Always. The probe leaves nothing behind, on any database, including on failure.
ROLLBACK;
