# Changelog

## 2026-03-14

### Added
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
