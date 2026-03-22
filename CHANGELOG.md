# Changelog

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
