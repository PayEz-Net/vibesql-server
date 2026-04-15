# VibeSQL Server

**Production-ready PostgreSQL + JSONB server with multi-tenant architecture, schema evolution, and container secret authentication.**

---

## What is this?

VibeSQL Server is the production version of VibeSQL - a multi-tenant PostgreSQL server optimized for AI agents and microservices. While [VibeSQL Micro](https://github.com/PayEz-Net/vibesql-micro) is perfect for local development, **VibeSQL Server** is built for production deployments.

**Key Features:**
- **Multi-tenant architecture** — Isolated data per client with tier-based rate limiting
- **Schema evolution** — Automatic lazy migration on read with transform support
- **Container secret auth** — Simple shared secret for internal service-to-service calls (vault integration planned)
- **Virtual indexes** — JSONB query optimization without physical indexes
- **Audit logging** — Complete audit trail for compliance
- **Tier-based rate limiting** — Free, Starter, Pro, Enterprise tiers

---

## Architecture

### Projects

| Project | Description |
|---------|-------------|
| **VibeSQL.Server** | ASP.NET Core REST API — query execution, schema CRUD, document storage |
| **VibeSQL.Core** | Core library — repositories, query engine, validators, data access |
| **VibeSQL.Sentinel** | Schema change classifier — 4-tier risk taxonomy (Safe/Migration/Destructive/Prohibited), data-aware verdict downgrading. Zero EF Core dependency. |
| **VibeSQL.Edge** | External-facing OIDC gateway — multi-provider JWT auth, federated identity, SQL permission enforcement, HMAC-signed proxy to Server |

### Tech Stack

- **.NET 9.0** — Modern C# with ASP.NET Core
- **PostgreSQL 16+** — Native JSONB support, Npgsql driver
- **Azure Key Vault** — Secret management (planned)

---

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- PostgreSQL 16+ (local or remote)

### Build

```bash
git clone https://github.com/PayEz-Net/vibesql-server.git
cd vibesql-server
dotnet restore
dotnet build
```

### Run

```bash
cd src/VibeSQL.Server
dotnet run
# → Running at http://localhost:5000
```

### Docker

```bash
docker build -t vibesql-server -f docker/Dockerfile .
docker run -p 5000:80 \
  -e DATABASE_CONNECTION="Host=localhost;Database=vibesql;..." \
  vibesql-server
```

---

## Features

### 1. Multi-Tenant Architecture

Each client gets isolated data with configurable tier limits:

```json
{
  "clientId": 123,
  "tier": "Pro",
  "limits": {
    "maxCollections": 100,
    "maxDocuments": 1000000,
    "maxSchemaSize": 102400
  }
}
```

**Tiers:**
- **Free** — 10 collections, 10K documents
- **Starter** — 50 collections, 100K documents
- **Pro** — 100 collections, 1M documents
- **Enterprise** — Unlimited

### 2. Schema Evolution

Automatic lazy migration on read with declarative transforms:

```json
{
  "x-vibe-migrations": {
    "1_to_2": [
      {
        "field": "price",
        "transform": "multiply",
        "args": 100,
        "reason": "Convert dollars to cents"
      },
      {
        "field": "status",
        "transform": "map",
        "args": {
          "active": "enabled",
          "inactive": "disabled"
        }
      }
    ]
  }
}
```

**Supported transforms:**
- `multiply` / `divide` — Numeric transformations
- `map` — Value mapping (enums, status codes)
- `cast` — Type conversions
- `rename` — Field renaming
- `default` — Default values for missing fields

### 3. Virtual Indexes

Optimize JSONB queries without creating physical indexes:

```sql
-- Traditional approach (slow)
SELECT * FROM vibe_documents
WHERE data->>'user_id' = '123';

-- With virtual index (fast)
CREATE VIRTUAL INDEX idx_user_id ON users(user_id);
-- Transparently uses GIN index on jsonb column
```

### 4. Audit Logging

Complete audit trail for compliance:

```csharp
// Every operation logged
{
  "auditLogId": 456,
  "clientId": 123,
  "operation": "CreateDocument",
  "collection": "users",
  "documentId": "abc-123",
  "changes": { ... },
  "userId": "user-789",
  "timestamp": "2026-02-08T10:30:00Z"
}
```

### 5. Authentication (Container Secret)

VibeSQL Server is an **internal service** that uses container secret authentication for service-to-service calls. HMAC authentication for external clients is handled by **Vibe.Edge** at the DMZ layer.

```bash
# appsettings.json or environment
VibeSQL__ContainerSecret="your-shared-secret"
# or env var: VIBESQL_CONTAINER_SECRET
```

Callers send a single header:
- `Authorization: Secret {your-shared-secret}`

Auth is bypassed for `/health` and `/swagger` paths. The optional `X-Vibe-Client-Tier` header is supported for tier-based timeout configuration.

### 6. Constraint Violation Observability

PostgreSQL integrity constraint violations (SQLSTATE class 23 — unique, foreign key, not-null, check, exclusion) are surfaced as structured `CONSTRAINT_VIOLATION` events with constraint name, schema, table, column, `DETAIL`, `HINT`, truncated statement, and duration. A dedicated Serilog sub-logger filters these to a Postgres-style rolling flatfile, leaving your main Console/Graylog pipeline untouched:

```
2026-04-14 22:47:01.234 +00:00 [42] WARN :  CONSTRAINT_VIOLATION [23505]: duplicate key value violates unique constraint "idx_users_email_unique" (constraint=idx_users_email_unique, table=vibe.documents, column=-)
	DETAIL:  Key (email)=(foo@example.com) already exists.
	STATEMENT:  INSERT INTO vibe.documents (...) VALUES (...)
```

Configure in `appsettings.json`:

```json
{
  "Logging": {
    "VibeSQL": {
      "ConstraintLog": {
        "Enabled": true,
        "FilePath": "logs/vibesql-constraints.log",
        "RollingInterval": "Day",
        "RetainedFileCountLimit": 14
      }
    }
  }
}
```

API clients can branch on the error code (`UNIQUE_VIOLATION`, `FOREIGN_KEY_VIOLATION`, `NOT_NULL_VIOLATION`, `CHECK_VIOLATION`, `EXCLUSION_VIOLATION`, `CONSTRAINT_VIOLATION`) instead of parsing PostgreSQL messages. HTTP status follows the semantics: 409 for conflicts, 400 for not-null and check violations.

### 7. Query Validation

`QueryValidator` runs lightweight checks on every SQL query before it reaches PostgreSQL:

- **Non-empty** — missing or whitespace-only queries return `MISSING_REQUIRED_FIELD`.
- **Size limit** — standard DML is capped at 256KB. Schema writes against the `collection_schemas` table — queries whose normalized prefix is `INSERT INTO COLLECTION_SCHEMAS` or `UPDATE COLLECTION_SCHEMAS` — get a higher 512KB allowance because schema rows carry full JSON definitions. `SELECT`, `DELETE`, and DDL statements targeting that table fall through to the 256KB default: `DELETE` is not a schema write, and DDL payloads are small enough for the default budget.
- **Keyword prefix** — queries must start with a recognized keyword (`SELECT`, `INSERT`, `UPDATE`, `DELETE`, `CREATE`, `DROP`, `ALTER`, `TRUNCATE`).

#### Error previews

Validation errors include the first 80 characters of the offending SQL in the error detail so clients can identify which query was rejected without cross-referencing server logs:

```json
{
  "success": false,
  "error": {
    "code": "QUERY_TOO_LARGE",
    "message": "Query too large",
    "detail": "SQL query exceeds the maximum allowed size of 256KB. Starts with: INSERT INTO users (name, email, bio, avatar_url, created_at) VALUES..."
  }
}
```

```json
{
  "success": false,
  "error": {
    "code": "INVALID_SQL",
    "message": "Invalid SQL syntax",
    "detail": "Query must start with a valid SQL keyword (SELECT, INSERT, UPDATE, DELETE, CREATE, DROP). Received: EXEC sp_who..."
  }
}
```

Preview slicing uses UTF-16 code units with a high-surrogate guard: if truncation would split a surrogate pair at the boundary (e.g. an emoji whose high surrogate lands at code unit 79), the preview shortens to 79 characters so the JSON error body contains no replacement characters or half-surrogates.

---

## API Examples

### Create Collection

```bash
POST /api/v1/collections
{
  "clientId": 123,
  "collection": "users",
  "schema": {
    "tables": {
      "users": {
        "properties": {
          "name": { "type": "string" },
          "email": { "type": "string" },
          "age": { "type": "integer" }
        }
      }
    }
  }
}
```

### Insert Document

```bash
POST /api/v1/documents
{
  "clientId": 123,
  "collection": "users",
  "data": {
    "name": "Alice",
    "email": "alice@example.com",
    "age": 30
  }
}
```

### Query Documents

```bash
GET /api/v1/documents?clientId=123&collection=users&filter=age>25
```

### Evolve Schema

```bash
PUT /api/v1/schemas/{schemaId}
{
  "schema": {
    "tables": {
      "users": {
        "properties": {
          "name": { "type": "string" },
          "email": { "type": "string" },
          "age": { "type": "integer" },
          "status": { "type": "string" }  // New field
        }
      }
    },
    "x-vibe-migrations": {
      "1_to_2": [
        {
          "field": "status",
          "transform": "default",
          "args": "active"
        }
      ]
    }
  }
}
```

---

## Configuration

### Environment Variables

```bash
# Database
DATABASE_CONNECTION="Host=localhost;Database=vibesql;Username=postgres;Password=..."

# Authentication (container secret)
VIBESQL_CONTAINER_SECRET="your-shared-secret"

# Rate Limiting
VIBESQL_DEFAULT_TIER="Free"
VIBESQL_ENABLE_RATE_LIMITING=true

# Logging
SERILOG_MINIMUM_LEVEL="Information"
```

### appsettings.json

```json
{
  "ConnectionStrings": {
    "VibeDatabase": "Host=localhost;Database=vibesql;..."
  },
  "VibeSQL": {
    "EnableMultiTenancy": true,
    "EnableSchemaEvolution": true,
    "EnableAuditLogging": true,
    "DefaultTier": "Free",
    "Tiers": {
      "Free": {
        "MaxCollections": 10,
        "MaxDocuments": 10000,
        "MaxSchemaSize": 10240
      },
      "Pro": {
        "MaxCollections": 100,
        "MaxDocuments": 1000000,
        "MaxSchemaSize": 102400
      }
    }
  }
}
```

---

## Database Schema

### Core Tables

- **vibe_documents** — JSONB document storage
- **vibe_collection_schemas** — Schema versioning
- **vibe_audit_logs** — Audit trail
- **tier_configurations** — Tier limits
- **virtual_indexes** — Virtual index definitions
- **feature_usage_logs** — Usage tracking

### Migrations

```bash
# Create migration
cd src/VibeSQL.Core
dotnet ef migrations add InitialCreate --startup-project ../VibeSQL.Server

# Update database
dotnet ef database update --startup-project ../VibeSQL.Server
```

---

## Deployment

### Docker

```bash
docker build -t vibesql-server -f docker/Dockerfile .
docker run -p 52411:8080 \
  -e DATABASE_CONNECTION="Host=db;Database=vibesql;..." \
  -e VIBESQL_CONTAINER_SECRET="your-secret" \
  vibesql-server
```

### Azure App Service

```bash
# Build and publish
dotnet publish -c Release -o publish

# Deploy to Azure
az webapp deploy --name vibesql-server \
  --resource-group vibesql-rg \
  --src-path publish.zip
```

### Kubernetes

See `docker/k8s/` for Kubernetes manifests.

---

## Development

### Project Structure

```
src/
├── VibeSQL.Core/               # Core library
│   ├── Data/                   # Repositories, migrations
│   ├── Query/                  # QueryExecutor, QueryValidator, safety checks
│   └── Sentinel/               # PostgresTableInspector (data inspection)
│
├── VibeSQL.Server/             # ASP.NET Core API
│   ├── Controllers/V1/         # Query, Schemas, Documents controllers
│   ├── Middleware/             # Auth, rate limiting
│   └── Program.cs              # Startup
│
├── VibeSQL.Sentinel/           # Schema change classification (standalone)
│   ├── SchemaDiffEngine.cs     # Structural diff between JSON schemas
│   ├── ChangeClassifier.cs     # Deterministic rules engine
│   ├── SentinelTaxonomy.cs     # S/M/D/P code definitions
│   └── SentinelPipeline.cs     # Orchestrator: diff → classify → inspect → verdict
│
└── VibeSQL.Edge/               # OIDC gateway (external-facing)
    ├── Authentication/         # Multi-provider JWT, dynamic scheme registration
    ├── Authorization/          # Permission resolver, SQL statement classifier
    ├── Identity/               # Federated identity, auto-provisioning
    ├── Admin/                  # Provider, role, client management APIs
    └── Proxy/                  # HMAC-signed reverse proxy to Server

tests/
└── VibeSQL.Edge.Tests/         # Unit + integration tests for Edge
```

### Testing

```bash
dotnet test
```

---

## Production Use

**VibeSQL Server is production-ready and battle-tested for:**
- **Multi-tenant SaaS platforms**
- **AI agent data persistence**
- **Microservices with evolving schemas**
- **Edge computing with schema evolution**
- **Compliance-heavy industries** (audit logging)

**Not included (see VibeSQL Cloud):**
- Managed hosting
- Automatic backups
- Global CDN
- 99.99% SLA

---

## Ecosystem

| Component | Description | Repo |
|-----------|-------------|------|
| **VibeSQL Micro** | Embedded Go binary with PostgreSQL 16.1 for local dev | [vibesql-micro](https://github.com/PayEz-Net/vibesql-micro) |
| **VibeSQL Server** | Production .NET 9 server with multi-tenant architecture | This repo |
| **VibeSQL Edge** | OIDC gateway with federated auth and SQL permission enforcement | This repo (`src/VibeSQL.Edge`) |
| **VibeSQL Sentinel** | Schema change classification and safety analysis | This repo (`src/VibeSQL.Sentinel`) |
| **vsql CLI** | Zero-dep TypeScript CLI for query, schema management, rollback | [vsql](https://github.com/PayEz-Net/vsql) |
| **vibesql-mail** | Agent-to-agent messaging MCP server | [vibesql-mail-mcp](https://github.com/PayEz-Net/vibesql-mail-mcp) |

## Comparison

| Feature | VibeSQL Micro | VibeSQL Server | VibeSQL Server + Edge |
|---------|---------------|----------------|----------------------|
| **Use Case** | Local dev | Internal services | External / multi-provider |
| **Multi-tenant** | - | ✅ | ✅ |
| **Auth** | None | Container Secret | OIDC JWT (multi-provider) |
| **Schema evolution** | - | ✅ Lazy migration | ✅ Lazy migration |
| **Schema safety** | - | ✅ Sentinel | ✅ Sentinel |
| **Permission enforcement** | - | - | ✅ SQL classification |
| **Rate limiting** | - | ⚠️ Tier timeouts | ✅ Tier-based |
| **Audit logs** | - | ✅ Full trail | ✅ Full trail |
| **Cost** | Free | Free (self-host) | Free (self-host) |

---

## Contributing

Contributions welcome! Please open an issue or pull request.

---

## License

Apache 2.0 License. See [LICENSE](LICENSE).

---

## Links

- **VibeSQL Micro** (local dev): [github.com/PayEz-Net/vibesql-micro](https://github.com/PayEz-Net/vibesql-micro)
- **vsql CLI**: [github.com/PayEz-Net/vsql](https://github.com/PayEz-Net/vsql)
- **Agent Mail MCP**: [github.com/PayEz-Net/vibesql-mail-mcp](https://github.com/PayEz-Net/vibesql-mail-mcp)
- **Website**: [vibesql.online](https://vibesql.online)
- **Documentation**: [vibesql.online/docs](https://vibesql.online/docs)

---

<div align="right">
  <sub>Powered by <a href="https://idealvibe.online">IdealVibe</a></sub>
</div>
