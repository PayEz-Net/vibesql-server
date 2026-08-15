# Security

## Advisory: tenant isolation policies in `scripts/base-schema.v2.sql` let any tenant write a row every tenant could read (fixed)

**If you provisioned a database from `scripts/base-schema.v2.sql` before it carried
`WITH CHECK`, your database is still affected. Updating the file does not fix a database
already created from it — the policies must be altered in place. Steps below.**

### Affected

`scripts/base-schema.v2.sql` (present only on feature branches, not on `master`) as it existed on branch `fix/wire-audit-log-repository`
before commit `4a06e03`. The file was never present on `master` or `main`, so it is
reachable only if you took it from that branch, or from an image built from it.

### What was wrong

All seven `tenant_isolation` policies declared `USING` and nothing else:

```sql
CREATE POLICY tenant_isolation ON vibe.documents
    USING (client_id = current_setting('app.client_id', true)::integer OR client_id = 0);
```

On a `FOR ALL` policy, **PostgreSQL applies the `USING` expression to writes when
`WITH CHECK` is omitted.** The `OR client_id = 0` disjunct therefore became a *write*
permission: any tenant could insert a row with `client_id = 0`, and because that same
disjunct appears in every policy's `USING` clause, **every other tenant could then read
that row.**

The result is a broadcast channel — one tenant writes, all tenants read — rather than a
leak between two specific tenants.

### What was NOT wrong, and why it matters to you

**A tenant attempting to write a row owned by another *named* tenant was refused, even
before the fix.** This is the case most people test first. It returns a clean refusal and
suggests isolation is intact while the `client_id = 0` path remains open. **If you
verified isolation that way, your verification could not have detected this.**

### Detecting it in a running database

`rowsecurity = true` is true both for a correct policy and for the affected one.
`ENABLE`, `FORCE`, and a policy named `tenant_isolation` were all present throughout.
**A flag or catalog check cannot tell them apart. Only an attempted write can.**

Run, as a **non-superuser** role with `rolbypassrls = false`:

```
psql "postgres://<non-superuser>@<host>:<port>/<db>" -f scripts/rls-acceptance-probe.sql
```

The probe aborts if given a superuser connection, because a superuser bypasses RLS and
would report a confident false negative. It rolls back everything it does and exits
non-zero if any table accepts a cross-tenant or broadcast write.

### Fixing a database already provisioned

For each affected table, add the write check. `USING` is deliberately left wider than
`WITH CHECK`: a tenant may **read** shared (`client_id = 0`) rows but may not **write**
them.

```sql
ALTER POLICY tenant_isolation ON vibe.<table>
    USING      (client_id = current_setting('app.client_id', true)::integer OR client_id = 0)
    WITH CHECK (client_id = current_setting('app.client_id', true)::integer);
```

Tables carrying `tenant_isolation` in the affected file: `collection_schemas`,
`documents`, `encrypted_value_ownership`, `virtual_indexes`, `tier_configurations`,
`audit_logs`, `feature_usage_logs`.

After altering, re-run the probe and confirm it exits zero.

## Applying schema to a database — running the probe is a MANDATORY step

**Whenever you apply a base schema to a database — new provision or re-provision, any
environment — you must then run `scripts/rls-acceptance-probe.sql` against that database as
a non-superuser and confirm it exits zero. This is a required step of the apply, not a
recommendation. An apply without it is an incomplete apply.**

```
# 1. apply whichever base schema you are provisioning from
psql "postgres://<admin>@<host>:<port>/<db>" -f <the base schema you applied>.sql

# 2. MANDATORY — prove isolation by refusal, as a NON-SUPERUSER
psql "postgres://<non-superuser>@<host>:<port>/<db>" -f scripts/rls-acceptance-probe.sql
# exit 0  = isolation proven by refusal.
# non-zero = STOP. The database has a write-open policy. Remediate before it takes traffic.
```

Note that `scripts/base-schema.v2.sql` is **not on this branch** — it has never been on
`master`. It exists only on `fix/wire-audit-log-repository` and
`scout/188798-rls-with-check`. The step above is written for whichever schema file you
actually applied, because the hazard travels with the *checkout you provisioned from*, not
with this branch.

### Why this step is mandatory rather than advisory

The write-open pre-`4a06e03` shape of `scripts/base-schema.v2.sql` is still reachable in
public history on those two branches. That history was **deliberately accepted rather than
rewritten** (card 188798): the blob is a hazardous shape rather than a secret, `master` is
clean, and force-pushing shared public branches costs more than the residual risk.

**That acceptance is only sound because this probe actually gets run.** An old checkout
still serves the write-open schema, so the probe is the one thing standing between a stale
checkout and a live cross-tenant broadcast channel. Skipping it does not merely skip a
test — it silently converts a consciously accepted risk into an unmitigated one.

Honest limitation: **nothing enforces this in CI.** It holds only because it is written
here as mandatory and because people follow it. That is a reason to be strict about it, not
a loophole — and if anyone does automate it later, this section is the thing to point the
automation at.

### Reporting a vulnerability

Open an issue, or contact the maintainers privately if you believe the report should not
be public before a fix exists.
