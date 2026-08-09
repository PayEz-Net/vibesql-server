# Cross-Schema Reference Constraints ("virtual FK")

> **Aliases (so search finds this):** virtual FK · virtual foreign key · client_id gate ·
> reference constraint · cross-DB FK · tenant-key referential integrity.
>
> **One line:** how a VibeSQL column (e.g. `vibe.documents.client_id`) is constrained to
> reference a primary key that lives in **another schema or another database** — most
> importantly the IDP **clients** PK — so a write naming a client that does not exist is
> **rejected at write time** instead of surfacing months later as orphaned rows.

Status: **precedent shipped, pattern being generalized.** One instance is live
(`vibe.collection_schemas.client_id` → clients, migration V006). This document names the
pattern so we stop re-discovering it, and is the review artifact before extending it to
`vibe.documents.client_id` and beyond.

---

## Why this exists — the failure it prevents

VibeSQL is a document store, but its rows are still **tenant-keyed** by a `client_id`
integer column (`vibe.documents`, `vibe.collection_schemas`). That column had **no
referential integrity**: nothing stopped a write from parking data on a `client_id` that
has no client behind it.

The concrete damage: `client_id = 0` was used as a code-only "global" sentinel (there is
no client 0, no PK row, no principal). Because nothing enforced "client_id must be a real
client", tenant/project data drifted onto the sentinel — ~2,077 kanban/standup rows landed
at `client_id = 0`, invisible to every client-scoped read, discoverable only by accident of
a repository hardcoding `ClientId == 0`.

A referential constraint makes that class of bug **unrepresentable**: an insert with a
`client_id` that has no client is refused. You cannot silently orphan tenant data.

## The stated concept (precedent)

The pattern was written down before, for the **users** PK, in
`PayEz-Core/docs/architecture/distributed-identity.md` ("Business Entity Example"):

```sql
-- CRITICAL: Real foreign key ensures referential integrity
CONSTRAINT FK_key_custodians_user FOREIGN KEY (user_id)
    REFERENCES core_identity.asp_net_users(user_id)
    ON DELETE NO ACTION
```

A table in one schema carries a **real FK to a PK it does not own**. That is the whole
idea. This doc generalizes it from `asp_net_users(user_id)` to the **clients** PK, and from
"physical FK only" to "physical where possible, virtual where not."

---

## Two enforcement modes

The referenced PK (clients, users) is owned by the **Identity** database/schema. Whether we
can declare a *native* Postgres FK depends on **co-location**, because a Postgres FK cannot
cross databases — only schemas within one database.

### Mode A — Physical cross-schema FK (referenced table is in the SAME database)

A real DB constraint. This is the strong form: the database itself refuses the bad write.

**Live precedent — `database/migrations/V006__vss_schema_cleanup_and_constraints.sql`:**

```sql
ALTER TABLE vibe.collection_schemas
    ADD CONSTRAINT fk_collection_schemas_client
    FOREIGN KEY (client_id) REFERENCES identity.clients(client_id)
    ON DELETE CASCADE;
```

Guarded with `IF EXISTS (identity.clients)` — i.e. it only fires where the clients table is
co-located in the VibeSQL database. In Sentinel terms this is an **`M-201` AddFkConstraint**
(auto-appliable migration; see `SentinelTaxonomy.cs`).

### Mode B — Virtual FK (referenced table is in a DIFFERENT database)

When the clients/users table is **not** co-located, no native FK is possible. The constraint
is enforced one layer up, at the application/repository write path, against a **reference
entity** — an EF mapping of a table we do not own, declared solely so relationships to its
PK can be expressed and validated.

**Precedent — `PayEz-Core/.../EntityConfigurations/IdentityReference/AspNetUserConfiguration.cs`:**

```csharp
entity.ToTable("asp_net_users", "core_identity");
entity.HasKey(e => e.UserId);            // the borrowed PK
entity.Property(e => e.UserId).HasColumnName("user_id");
// FK relationships would be configured here if navigation properties exist
```

The reference entity is read-only shadow: we never write it, we only point at its PK. The
document repository validates `client_id` against it on write and refuses an unresolved id.
Same guarantee as Mode A, enforced in code instead of by the engine — hence **virtual** FK.

### How the modes combine — DECIDED (Jon, 2026-08-05)

**Mode B is the always-on path. Mode A is defense-in-depth.**

```
ALWAYS: Mode B — repository write-time validation against the reference entity.
        The guarantee is identical in every deployment, co-located or not.
   PLUS: Mode A — where the clients table IS co-located, also declare the physical
        FK (Sentinel M-201) as a second, engine-level backstop.
```

Rationale: the guarantee must not vary by deployment topology. Making the physical FK
*primary* would make the constraint strong only where clients happens to be co-located —
exactly the kind of silent, environment-dependent variance that let `client_id = 0` drift
in the first place. So the application-layer validator runs everywhere and owns the
contract; the physical FK, where available, is pure belt-and-suspenders and never the sole
line of defense.

Both present the same contract to the caller: *a write naming a non-existent client fails*,
returning **one typed error** regardless of which line caught it (see review item 3).

> **Open item to resolve before extending:** confirm, per environment, whether the clients
> table is co-located in the VibeSQL DB and under which name/schema. V006 says
> `identity.clients(client_id)`; the IDP's canonical name elsewhere is
> `core_identity.idp_clients(idp_client_id)`. The doc that gates `client_id` must bind the
> real, verified name — do not copy V006's name on faith.

---

## Mode A for the document store — FK'ing a jsonb field

> ⚠️ **UNVERIFIED — designed from the schema, not yet executed.** The recipe below was
> derived by the Mac team from the prod schema (454 kanban rows audited, jsonb field, shared
> table, FK-permits-NULL semantics) but **no statement in it has been run**. Do not treat it
> as blessed until someone applies it against a real DB. A tested draft turned into a doc
> becomes everyone's assumption — so: run the two pre-checks, apply in a window, then strike
> this banner.

`vibe.documents` stores its tenant/owner keys two ways: `client_id` is a **real column**
(the easy case — Mode A is a plain FK on it). But keys like `project_id` live **inside the
`data` jsonb**, and a jsonb field can't carry a native FK. The bridge: **surface the jsonb
field as a generated column Postgres maintains, then FK the column.**

```sql
-- surface the jsonb field as a real column Postgres maintains
ALTER TABLE vibe.documents
  ADD COLUMN project_id integer
  GENERATED ALWAYS AS (NULLIF(data->>'project_id','')::integer) STORED;

-- real FK; NULL is permitted, so collections with no project are untouched
ALTER TABLE vibe.documents
  ADD CONSTRAINT documents_project_id_fk
  FOREIGN KEY (project_id) REFERENCES vibe_projects.projects(id);

-- the NOT NULL equivalent, scoped to kanban ONLY (other collections legitimately have none)
ALTER TABLE vibe.documents
  ADD CONSTRAINT kanban_requires_project_id
  CHECK (
    NOT (collection = 'vibe_agents'
         AND table_name IN ('kanban_tasks','kanban_comments','kanban_activity'))
    OR project_id IS NOT NULL
  );

CREATE INDEX IF NOT EXISTS documents_project_id_idx
  ON vibe.documents (project_id) WHERE project_id IS NOT NULL;
```

**Why each clause (this reasoning decays fast once out of context — keep it):**
- **Not a plain `NOT NULL`.** `vibe.documents` is one shared table across all collections;
  ~96,000 rows from non-project collections legitimately have no `project_id`. A table-wide
  NOT NULL would reject all of them.
- **FK permits NULL** — that's precisely what protects the other collections. Rows without a
  project store NULL and the FK ignores them; only rows that *name* a project are checked.
- **The CHECK is kanban-scoped**, so "must have a project" applies exactly where it's true
  (kanban) and nowhere it isn't. It's the NOT-NULL guarantee without the table-wide blast.

**This is the same pattern as the `client_id` gate, in its two shapes:** `client_id` is the
**already-a-real-column** case (FK it directly); `project_id` is the **jsonb** case
(generated column, then FK). Anyone extending referential integrity into the doc store hits
one of these two — document both.

### Pre-checks — run BOTH, each must return zero rows

The generated column and FK apply to **all ~96,570 rows, not just kanban**. The audit only
covered `kanban_tasks`. If any other collection carries a malformed or orphaned `project_id`,
the `ALTER` dies mid-statement.

```sql
-- (1) non-numeric project_id anywhere → the ::integer cast fails, ADD COLUMN dies
SELECT collection, table_name, data->>'project_id' AS bad_value, count(*)
FROM   vibe.documents
WHERE  data ? 'project_id'
  AND  NULLIF(data->>'project_id','') !~ '^-?[0-9]+$'
GROUP  BY 1,2,3;

-- (2) project_id pointing at a dead project anywhere → FK creation is blocked
SELECT d.collection, d.table_name, d.data->>'project_id' AS orphan, count(*)
FROM   vibe.documents d
LEFT   JOIN vibe_projects.projects p ON p.id = NULLIF(d.data->>'project_id','')::int
WHERE  d.data ? 'project_id' AND p.id IS NULL
GROUP  BY 1,2,3;
```

### Locking — this is a maintenance-window change

`ADD COLUMN … GENERATED ALWAYS AS … STORED` **rewrites the table** and holds
`ACCESS EXCLUSIVE` for the duration — a full block on **every collection in the doc store**,
not just kanban. On ~180MB it's short, but it is not a casual change. For the FK half, use
`ADD CONSTRAINT … NOT VALID` then a separate `VALIDATE CONSTRAINT` to keep the exclusive-lock
window short (validation takes a weaker lock).

---

## Handling the global sentinel (`client_id = 0`)

A naive `NOT NULL` FK breaks legitimately-global rows (e.g. global agent config), because
`0` has no client to reference. The clean resolution — **NULL is global**:

- **`client_id IS NULL`** = genuinely global, not owned by any tenant. A Postgres FK ignores
  NULL, so global rows are allowed without a fake parent.
- **`client_id = <n>`** = must resolve to a real client. `0` and every other phantom id are
  refused.

Migration order to adopt on an existing table:

1. Move mis-parked tenant rows `0 → real client_id` (derive from `project_id` in the payload).
2. Move legitimately-global rows `0 → NULL`.
3. Make the column nullable.
4. Add the constraint (Mode A) or turn on repository validation (Mode B).

After this, `client_id = 0` is impossible, global is `NULL`, every tenant row points at a
real client. **Never create a real "client 0" row to satisfy the FK** — that legitimizes the
exact misuse this removes.

---

## Where it lives in code

| Concern | Location |
|---|---|
| Sentinel classification of FK adds/drops | `VibeSQL.Core/Sentinel/SentinelTaxonomy.cs` — `M-201`, `M-205`, `D-308`, `P-404` |
| Sentinel apply/verdict pipeline | `VibeSQL.Core/Sentinel/SentinelPipeline.cs` |
| Document column mapping (extension point for `client_id`) | `VibeSQL.Core/Data/EntityConfigurations/Vibe/VibeDocumentConfiguration.cs` |
| Schema column mapping (live precedent target) | `VibeSQL.Core/Data/EntityConfigurations/Vibe/VibeCollectionSchemaConfiguration.cs` |
| Write path where Mode B validation belongs | `VibeSQL.Core/Data/Repositories/VibeDocumentRepository.cs` |
| Physical-FK precedent | `PayEz-Core/database/migrations/V006__vss_schema_cleanup_and_constraints.sql` |
| Reference-entity precedent | `PayEz-Core/.../EntityConfigurations/IdentityReference/AspNetUserConfiguration.cs` |
| Concept, prose | `PayEz-Core/docs/architecture/distributed-identity.md` (Business Entity Example) |

---

## Current instance vs. planned

- **Live:** `vibe.collection_schemas.client_id` → clients (Mode A, V006, where co-located).
- **Planned next:** `vibe.documents.client_id` — where the kanban/standup drift happened.
  This is the gate that would have prevented the `client_id = 0` incident outright.
- **Candidates after that:** any VibeSQL table carrying a tenant `client_id` or an owner
  `user_id` that today has no referential integrity.

## Review questions

1. **One mechanism or two? — DECIDED (Jon, 2026-08-05): Mode-B-always, physical FK as
   defense-in-depth.** The repository validator (Mode B) runs in every deployment and owns
   the guarantee; the physical FK (Mode A) is an additional engine-level backstop only where
   the clients table is co-located, never the sole enforcement. See "How the modes combine".
2. **Where does Mode B live** — one shared `IClientReferenceValidator` the repositories call,
   or per-repository checks? Shared is DRY and single-source; per-repository is explicit.
3. **Failure shape.** A physical FK throws a Postgres FK-violation. Mode B should return the
   *same* typed error so callers (API, vsql-cli) cannot tell the modes apart. Define that
   error once.
4. **Sentinel authorship.** Adding these FKs is an `M-201`; the Sentinel can auto-generate
   the DDL. Should sentinel *emit* these constraints as part of schema provisioning so new
   tenants get the gate for free, rather than each table adding it by hand?
5. **NULL-vs-sentinel semantics** must be stated in the collection schema itself, not just
   here, so the browser/CLI render "global" correctly instead of showing a blank owner.

_Last reviewed: 2026-08-05._
