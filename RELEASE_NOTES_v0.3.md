# VibeSQL Server Update — PayEz Vibe API Capabilities Upstreamed

**Date:** 2026-04-15  
**Program:** VibeSQL Upstreaming Program v0.3  
**Scope:** Private `PayEz.VibeSql.Server.Api` → Public `vibe/VibeSQL-Server` (one-way)

---

## Summary

We have completed the v0.3 upstreaming bundle for VibeSQL Server. Three capabilities that were living only in the private PayEz fork have now been ported into the open-source server, bringing feature parity to the generic surface and establishing the reference pattern for all future upstreaming work.

**Merge:** PR #1 (`3f18658`) — Entries 2 + 3  
**Test scaffold:** `tests/VibeSQL.Core.Tests` seeded and green  
**Reference pattern:** 5-point acceptance checklist now enforced on every upstream row

---

## What’s New

### 1. Non-Query DML Execution Path
Plain `UPDATE`, `DELETE`, and `INSERT` statements that do **not** include a `RETURNING` clause now route through `NpgsqlCommand.ExecuteNonQueryAsync` and return the affected row count in `QueryExecutionResult.RowCount`.

- **Before:** These statements went through `ExecuteReaderAsync` and silently yielded zero rows, leaving callers with no visibility into whether anything actually changed.
- **Now:** Callers get an accurate affected-row count and an empty `Rows` list.
- **Edge-case guard:** Comments and string literals are stripped before checking for `RETURNING`, so the word appearing in a comment or inside a quoted literal does not false-positive the detector. *(Note: the OSS port is actually more correct here than the private source — a future reverse-sync can backport this improvement.)*
- **Known limitation:** CTE-wrapped DML (e.g., `WITH ... UPDATE`) is not yet detected and will still route through the reader path. This is pinned with an explicit test so it cannot change by accident.

### 2. Query Validator DX Improvements
Three lightweight, string-only refinements to `QueryValidator.Validate` that reduce abuse surface and speed up debugging:

- **Stricter schema-op detection.** The elevated 512KB query-size limit now applies **only** to statements whose normalized prefix is `INSERT INTO COLLECTION_SCHEMAS` or `UPDATE COLLECTION_SCHEMAS`. Reads (`SELECT`), `DELETE`, and DDL statements targeting that table fall back to the standard 256KB limit.
- **Oversized-query preview.** `QueryTooLarge` errors now include the first 80 characters of the offending SQL in the response body (`…Starts with: {preview}`), so you can correlate a 413 with the actual query without digging through server logs.
- **Invalid-keyword preview.** `InvalidSQL` errors similarly include the first 80 characters (`…Received: {preview}`), making it obvious what was rejected.
- **Unicode-safe slicing.** Preview truncation guards against splitting a surrogate pair, so emojis and other multi-code-unit characters do not turn into replacement glyphs in the error JSON.

### 3. Constraint Violation Observability *(Entry 1, landed 2026-04-14)*
PostgreSQL integrity constraint violations (SQLSTATE class 23) are now fully observable:

- **Structured events** for `UNIQUE_VIOLATION`, `FOREIGN_KEY_VIOLATION`, `NOT_NULL_VIOLATION`, `CHECK_VIOLATION`, and `EXCLUSION_VIOLATION` with constraint name, schema, table, column, `DETAIL`, `HINT`, truncated statement, and duration.
- **Dedicated constraint log** — a Serilog sub-logger writes Postgres-style rolling flatfile output (`logs/vibesql-constraints.log` by default) without touching your main Console/Graylog pipeline. Toggle via `Logging:VibeSQL:ConstraintLog:Enabled`.
- **Branchable error codes** — API clients can switch on the error code instead of parsing PostgreSQL messages. HTTP status follows semantics: `409` for conflicts, `400` for not-null/check violations.

---

## Test Coverage

We stood up `tests/VibeSQL.Core.Tests` as the permanent home for Tier 1 contract regression tests. The v0.3 bundle includes:

- Non-query DML happy path (UPDATE, DELETE, INSERT without RETURNING)
- SELECT and `INSERT ... RETURNING` still route through `ExecuteReaderAsync`
- Comment/string-literal guards on RETURNING detection
- CTE-wrapped DML known-limitation pin
- Schema-op precision (512KB lift for writes only)
- Oversized/invalid-keyword preview inclusion
- Backfilled constraint-observability tests

All green on `dotnet test`.

---

## Reference Pattern Established

Every future upstreamed feature must pass this 5-point checklist before merge:

1. **Driver layer abstracted** — standard `System.Data` / `Npgsql` APIs only; no Devart-specific hacks.
2. **Config externalized** — settings live in `appsettings.json` with documented defaults.
3. **Observability additive** — sub-loggers with filters, never mutating the main pipeline.
4. **Documentation travels with code** — `CHANGELOG.md` + README subsection in the same commit.
5. **Unit test coverage** — Tier 1 contract regression tests in `tests/VibeSQL.Core.Tests` covering happy path, error path, and at least one edge case.

---

## What This Means

- **Open-source users** now get the same core query-pipeline behavior that runs in production at PayEz.
- **Integrators** can run raw DML with confidence and debug validation failures faster.
- **Ops** get structured constraint logs for compliance and incident response.
- **Future upstreaming** has a repeatable, enforceable playbook.

---

## Docs & Links

- `CHANGELOG.md` — full technical notes for 2026-04-14 and 2026-04-15
- `README.md` — Sections 6 (Constraint Violation Observability) and 7 (Query Validation)
- Spec: `Agents/BAPert/specs/VIBESQL-UPSTREAMING-SPEC-v0.3.md`
- Inventory: `Agents/BAPert/specs/VIBESQL-UPSTREAMING-INVENTORY.md`

---

*Questions or feedback? Route them to the strike team (BAPert / DotNetPert / NextPert / Aurum / QAPert).*
