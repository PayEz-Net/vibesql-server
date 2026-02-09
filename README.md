# VibeSQL Server

**Production-ready PostgreSQL + JSONB server with multi-tenant architecture, schema evolution, and KV/secret managed authentication.**

---

## What is this?

VibeSQL Server is the production version of VibeSQL - a multi-tenant PostgreSQL server optimized for AI agents and microservices. While [VibeSQL Micro](https://github.com/PayEz-Net/vibesql-micro) is perfect for local development, **VibeSQL Server** is built for production deployments.

**Key Features:**
- **Multi-tenant architecture** — Isolated data per client with tier-based rate limiting
- **Schema evolution** — Automatic lazy migration on read with transform support
- **KV/secret managed auth** — Azure Key Vault integration for secure authentication
- **Virtual indexes** — JSONB query optimization without physical indexes
- **Audit logging** — Complete audit trail for compliance
- **Tier-based rate limiting** — Free, Starter, Pro, Enterprise tiers

---

## Architecture

### Projects

- **VibeSQL.Core** — Core library with repositories, services, and data access
- **VibeSQL.Server** — ASP.NET Core REST API server

### Tech Stack

- **.NET 9.0** — Modern C# with ASP.NET Core
- **PostgreSQL 16+** — Native JSONB support
- **Entity Framework Core 9.0** — Code-first migrations
- **Azure Key Vault** — Secret management (configurable)

---

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- PostgreSQL 16+ (local or remote)
- Azure Key Vault (optional, for KV/secret auth)

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

### 5. KV/Secret Managed Authentication

Secure authentication using Azure Key Vault:

```csharp
// Secrets loaded at startup
services.AddVibeAuthentication(options =>
{
    options.UseKeyVault = true;
    options.KeyVaultUrl = "https://your-vault.vault.azure.net/";
    options.SecretName = "vibesql-auth-key";
});
```

Or use environment variables for local development:

```bash
VIBESQL_AUTH_SECRET="your-secret-key"
```

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

# Authentication
VIBESQL_AUTH_SECRET="your-secret-key"
VIBESQL_USE_KEYVAULT=false

# Azure Key Vault (optional)
AZURE_KEYVAULT_URL="https://your-vault.vault.azure.net/"
VIBESQL_SECRET_NAME="vibesql-auth-key"

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

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY publish/ .
ENTRYPOINT ["dotnet", "VibeSQL.Server.dll"]
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
│   ├── Data/                   # DbContext, repositories
│   ├── Entities/               # Domain models
│   ├── Services/               # Business logic
│   ├── Interfaces/             # Abstractions
│   └── DTOs/                   # Data transfer objects
│
└── VibeSQL.Server/             # ASP.NET Core API
    ├── Controllers/            # REST endpoints
    ├── Middleware/             # Auth, rate limiting
    └── Program.cs              # Startup
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

## Comparison

| Feature | VibeSQL Micro | VibeSQL Server | VibeSQL Cloud |
|---------|---------------|----------------|---------------|
| **Use Case** | Local dev | Production self-hosted | Managed cloud |
| **Multi-tenant** | ❌ | ✅ | ✅ |
| **Auth** | ❌ | ✅ KV/secret managed | ✅ Full OAuth |
| **Schema evolution** | ❌ | ✅ Lazy migration | ✅ Lazy migration |
| **Rate limiting** | ❌ | ✅ Tier-based | ✅ Tier-based |
| **Audit logs** | ❌ | ✅ Full trail | ✅ Full trail |
| **Managed hosting** | ❌ | ❌ | ✅ |
| **Cost** | Free | Free (self-host) | Paid plans |

---

## Contributing

Contributions welcome! Please open an issue or pull request.

---

## License

Apache 2.0 License. See [LICENSE](LICENSE).

---

## Links

- **VibeSQL Micro** (local dev): [github.com/PayEz-Net/vibesql-micro](https://github.com/PayEz-Net/vibesql-micro)
- **Website**: [vibesql.online](https://vibesql.online)
- **Documentation**: [vibesql.online/docs](https://vibesql.online/docs)

---

<div align="right">
  <sub>Powered by <a href="https://idealvibe.online">IdealVibe</a></sub>
</div>
