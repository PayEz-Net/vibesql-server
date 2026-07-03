# Changelog

## 2026-07-03

### Added
- **JWT bearer authentication** — `ContainerSecretAuthMiddleware` now accepts `Authorization: Bearer {jwt}` validated against a cached JWKS from the configured IDP, alongside the existing `Authorization: Secret {key}` container-secret mode.
- **JWKS cache** — Background service that refreshes IDP signing keys every 24 hours from `Authentication:IdpBaseUrl/.well-known/jwks.json`.
- **Row-level security (RLS) tenant isolation** — `QueryExecutor` can execute queries inside a tenant-scoped transaction by setting `app.client_id` on the `VibeDbRls` connection.
- **Client ID resolution** — New `IClientIdResolver`/`ClientIdResolver` maps slug or numeric client IDs via config (`VibeSQL:ClientIdMappings:{slug}`); `DocumentsController` accepts `ClientId` as a string.
- **DML execution support** — `QueryExecutor` now handles non-returning `UPDATE/DELETE/INSERT` statements and returns affected-row counts.
- **Npgsql SQL driver** — Raw query path migrated from Devart dotConnect to Npgsql; Devart retained for EF Core/migrations only.
- **Error mapper** — `SqlStateMapper` now supports both Npgsql and Devart PostgreSQL exceptions with a new `TENANT_CONTEXT_REQUIRED` code.

### Changed
- **Query safety scanner** — Replaced regex-based comment/string stripping with a single-pass lexical scanner, eliminating false positives where `--` or `/*` inside string literals swallowed a trailing `WHERE`.
- **Query size limit** — Default `MaxQuerySizeBytes` raised from 10 KB to 256 KB (schema DDL limit remains 512 KB).
- **Health endpoint** — `/health` explicitly mapped and registered with ASP.NET health checks.
- **Docker Compose** — Updated to use `VIBESQL_CONTAINER_SECRET` and the `VibeDbRls` connection string.
- **Development configuration** — `appsettings.Development.json` sanitized to localhost/`CHANGE_ME` placeholders; no real secrets or internal IPs committed.

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
