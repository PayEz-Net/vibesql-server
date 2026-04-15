# Changelog

## 2026-04-15

### Added
- **Entry 2 — non-query DML execution path** — `QueryExecutor.ExecuteAsync` now detects `UPDATE` / `DELETE` / `INSERT` statements that do not have a `RETURNING` clause and routes them through `NpgsqlCommand.ExecuteNonQueryAsync`, returning the affected row count via `QueryExecutionResult.RowCount` with an empty `Rows` list. Previously such statements went through `ExecuteReaderAsync` and silently returned zero rows without the caller learning how many rows were actually affected. Upstreamed from the private `PayEz.VibeSql.Server.Api` implementation as part of the VibeSQL upstreaming program (see `Agents/BAPert/specs/VIBESQL-UPSTREAMING-SPEC-v0.3.md`).
- **`QueryExecutor.IsNonQueryDml(sql)` + `StripCommentsAndStringLiterals(sql)`** — internal static helpers that drive the non-query routing decision. Comments and single-quoted string literals are stripped before matching so that the word `RETURNING` appearing in a comment or inside a literal does not false-positive or false-negative the check. The regex patterns are duplicated from `QuerySafetyChecker` on purpose — cross-file refactoring to a shared `SqlTextNormalizer` is out of scope for this entry and can be revisited later.
- **Note on the OSS fix vs. private source** — the OSS implementation here is *behaviorally more correct* than the private `PayEz.VibeSql.Server.Api` source it was ported from. The private source false-negatives line-comment-containing-`RETURNING` and string-literal-containing-`RETURNING` (both incorrectly route through the reader path). The OSS port fixes both via `StripCommentsAndStringLiterals`. These two fixes are **reverse-sync candidates** alongside the reverse-drift already acknowledged on `SchemasController` (public ahead) and `VibeQueryError` class-23 surface (public ahead post-Entry-1, i.e. Finding F in the inventory). A future reverse-sync mini-program can port these improvements back into the private fork; they are NOT in scope for the one-way upstreaming program.
- **Entry 3 — query validator DX improvements** — three refinements to `QueryValidator.Validate` (all pure string-handling, no new dependencies):
  1. **Stricter schema-op detection.** The 512KB schema-query size limit now applies only to statements whose normalized prefix is `INSERT INTO COLLECTION_SCHEMAS` or `UPDATE COLLECTION_SCHEMAS`, instead of any query whose text contains `collection_schemas` anywhere. Reads from the schemas table now correctly get the 256KB default limit, and `DELETE FROM collection_schemas` is no longer treated as a schema-op. DDL statements targeting the schemas table (`CREATE TABLE collection_schemas`, `DROP TABLE collection_schemas`) also fall through to the default 256KB limit because they do not match the INSERT/UPDATE prefixes — DDL payloads are small in practice, so this is fine, but documented once to prevent re-litigation.
  2. **Oversized-query preview.** `QueryTooLarge` error responses now include the first 80 characters of the offending SQL in the error detail (`…Starts with: {preview}`), making it possible to correlate a 413 with the specific oversized query without needing to correlate against server logs.
  3. **Invalid-keyword preview.** `InvalidSQL` error responses similarly include the first 80 characters (`…Received: {preview}`) so clients can see what was actually rejected.
- **Unicode slicing choice (Entry 3)** — the 80-character preview uses a pragmatic three-line high-surrogate check: if the last code unit of the slice is a high surrogate (indicating it would split a surrogate pair in the middle of a multi-code-unit code point like an emoji), truncate to 79 characters instead. This matches the project's "Apple not Android — usability over feature count" philosophy — a user hitting an oversized-query error should not also see `\uFFFD` replacement glyphs in the preview. A future change to full rune-enumeration slicing should go through a scope discussion rather than happen as an unexamined upgrade.

### Known limitations
- **CTE-wrapped DML** (e.g. `WITH x AS (SELECT 1) UPDATE foo SET ...`) is NOT detected as non-query and routes through `ExecuteReaderAsync`, silently yielding zero rows instead of an affected row count. Behavior matches the private `PayEz.VibeSql.Server.Api` implementation exactly — this is a mechanical port, not a detection-engine upgrade. A proper fix requires SQL parsing past leading CTEs to find the actual root statement kind, which is out of scope for the upstreaming program. A pinned test case (`QueryExecutorNonQueryTests.IsNonQueryDml_CteWrappedUpdate_ReturnsFalse_KnownLimitation`) asserts the current (wrong) behavior so it cannot be silently "fixed" in a future PR without an explicit scope discussion. Workaround for callers: rewrite the SQL to avoid the leading CTE.

## 2026-04-14

### Added
- **Constraint violation observability** — PostgreSQL integrity constraint violations (SQLSTATE class 23) now emit structured `CONSTRAINT_VIOLATION` events with constraint name, schema, table, column, `DETAIL`, `HINT`, truncated statement, and duration. A dedicated Serilog sub-logger filters these to a Postgres-style rolling flatfile (`logs/vibesql-constraints.log` by default), leaving the main Console/Graylog pipeline untouched. Toggle via `Logging:VibeSQL:ConstraintLog:Enabled` in appsettings.
- **`VibeErrorCodes` for class 23** — `UNIQUE_VIOLATION` (23505), `FOREIGN_KEY_VIOLATION` (23503), `NOT_NULL_VIOLATION` (23502), `CHECK_VIOLATION` (23514), `EXCLUSION_VIOLATION` (23P01), `CONSTRAINT_VIOLATION` (23000/23001). Each maps to an appropriate HTTP status (409 for conflicts, 400 for not-null/check) so API clients can branch on the error code instead of parsing messages.
- **`SqlStateMapper.IsConstraintViolation(sqlState)`** — helper that returns true for any SQLSTATE in class 23, used by `QueryExecutor` to route structured events.

## 2026-03-19

### Added
- **VibeSQL.Edge** — External-facing OIDC authentication gateway and reverse proxy for VibeSQL Server. Multi-provider JWT auth with runtime-configurable OIDC providers, federated identity resolution, SQL statement classification for permission enforcement (Read/Write/Schema/Admin), HMAC-signed proxy to Server, and admin APIs for managing providers, role mappings, client permission ceilings, and federated identities. Includes PostgreSQL-backed config store (`vibe_system` schema), audit logging, and auto-provisioning of new users. Full test suite with 20+ test files covering unit, integration, and middleware tests.
- **VibeSQL.Sentinel** — Standalone schema change classification library with zero EF Core dependency. 4-tier risk taxonomy: Safe (S-100–S-109), Migration (M-200–M-205), Destructive (D-300–D-312), Prohibited (P-400–P-404). Deterministic rules engine classifies structural diffs between JSON schemas. Data-aware verdict downgrading via `IDataInspector` interface — queries live PostgreSQL to determine actual risk (empty table drop = Migration, not Destructive). Cost-aware query budget (500ms total). Pipeline orchestrator composes diff → classify → inspect → verdict.
- **PostgresTableInspector** — Concrete `IDataInspector` implementation in VibeSQL.Core. Hybrid row counting (pg_class.reltuples for large tables, exact COUNT for small), per-query timeout, fail-safe on uncertainty.
- **Schema CRUD routes** — `PUT /v1/schemas/{collection}` creates versioned schema (auto-increments version, deactivates previous), `GET /v1/schemas/{collection}/versions` lists version history with metadata
- **Document insert route** — `POST /v1/collections/{collection}/tables/{table}` inserts JSONB documents with client/user/collection/schema tracking
- **Role name fix script** — `scripts/fix_canonical_role_names.sql` for normalizing RBAC role names

### Changed
- **Npgsql replaces Devart** — Replaced Devart dotConnect with Npgsql across all open-source VibeSQL projects. Devart remains in the proprietary PayEz fork; open-source consumers use the standard PostgreSQL driver.
- **Query size limit raised** — MaxQuerySize increased from 10KB to 256KB for document payloads (resumes, profiles). Schema operations mentioning `collection_schemas` get 512KB limit.
- **Kestrel HTTPS reverted** — HTTPS config added then removed; Istio handles TLS termination in the cluster.
- **Dockerfile updated** — Sentinel csproj added to restore layer for proper build caching.

## 2026-03-14

### Added
- **Agent mail relational schema** — Standalone agent mail tables (agents, teams, messages, inbox, kanban) for VibeSQL Server users who don't run vibesql-micro, with performance indexes baked in
- **Agent mail performance indexes** — Targeted partial indexes on `vibe.documents` for agent mail inbox and message lookups: inbox by agent (sorted), messages by ID, inbox by message_id (mark-as-read)

## 2026-03-13

### Changed
- **Auth: container secret only** — Replaced HMAC authentication with simple shared secret (`Authorization: Secret {key}`). VibeSQL Server is an internal service; HMAC for external clients is handled by [Vibe.Edge](https://github.com/payez-net/Vibe.Edge).
- Secret loaded from `VibeSQL:ContainerSecret` config or `VIBESQL_CONTAINER_SECRET` env var
- Vault integration planned for secret management

### Removed
- `HmacAuthMiddleware` — no longer needed (Edge handles HMAC at DMZ)
- `HmacAuthOperationFilter` — Swagger filter for HMAC headers
- `VibeSecretConfiguration` — HMAC config model
- HMAC-related config keys (`HmacSecretName`, `HmacSecret`, `DevBypassHmac`)

## 2026-02-07

### Changed
- Updated tech stack to .NET 9.0 and EF Core 9.0
- Made comparison table claims defensible for investor materials

## 2026-01-15

### Added
- Initial VibeSQL Server extraction from PayEz-Core
- POST `/v1/query` — execute SQL with safety validation
- GET `/v1/health` — health check endpoint
- HMAC authentication middleware
- Query safety checker, rate limiter, validator
- Swagger/OpenAPI documentation
