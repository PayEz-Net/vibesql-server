-- ═══════════════════════════════════════════════════════════════════════════
-- SECURITY ADVISORY — READ IF YOU PROVISIONED FROM AN EARLIER COPY OF THIS FILE
--
-- Before commit 4a06e03 the seven `tenant_isolation` policies below declared USING
-- and no WITH CHECK. PostgreSQL applies USING to writes when WITH CHECK is absent,
-- so `OR client_id = 0` became a WRITE permission: any tenant could insert a
-- client_id = 0 row, which every other tenant could then read.
--
-- UPDATING THIS FILE DOES NOT FIX A DATABASE ALREADY CREATED FROM IT. The policies
-- must be altered in place. `rowsecurity = true` is true of the broken policy too,
-- so a flag check cannot tell you whether you are affected — only an attempted
-- write can. See SECURITY.md for detection and the ALTER POLICY remediation, and
-- run scripts/rls-acceptance-probe.sql (as a NON-SUPERUSER) to verify.
-- ═══════════════════════════════════════════════════════════════════════════
--
-- ═══════════════════════════════════════════════════════════════════════════
-- VibeSQL — canonical base schema (v2, PROPOSED)
--
-- Replaces scripts/base-schema.sql, which is incomplete and inaccurate:
--   * declares 2 of the 8 product tables
--   * both of those are missing columns the ORM writes
--   * declares indexes that do not exist in a live database
--   * contains NO row-level security, while every live tenant table has it
--
-- Scope. The product schema is what VibeSQL.Core maps
-- (src/VibeSQL.Core/Data/EntityConfigurations). That is the eight tables below.
-- Any other table in a deployed `vibe` schema — page_permissions,
-- license_keys, email_preferences, skills, access_control_config — is a
-- deployment-specific modification and is deliberately NOT published here.
--
-- Verified against a live instance 2026-08-08.
-- ═══════════════════════════════════════════════════════════════════════════

CREATE SCHEMA IF NOT EXISTS vibe;

-- ───────────────────────────────────────────────────────────────────────────
-- Tenant isolation
--
-- Every tenant-scoped table below enables RLS and FORCES it. FORCE matters:
-- without it the table owner bypasses the policy, and the application commonly
-- connects as the owner — so RLS without FORCE is isolation that silently does
-- not apply to the one role that matters.
--
-- The READ rule and the WRITE rule are deliberately DIFFERENT, and the difference is
-- load-bearing. USING lets a tenant READ shared rows (client_id = 0); WITH CHECK does
-- NOT let it WRITE them. Omitting WITH CHECK is not a shorter way to say the same
-- thing: Postgres reuses USING as the write check when WITH CHECK is absent, so
-- "OR client_id = 0" silently becomes a WRITE permission. Any tenant could then insert
-- a client_id = 0 row that EVERY other tenant reads back -- a cross-tenant channel
-- built out of the isolation policy itself. Verified by refusal (card 188798).
--
-- The policy admits client_id = 0 as shared/global rows. Anything reading for
-- a single tenant must ALSO filter explicitly; the policy is defence in depth,
-- not a scoping mechanism.
-- ───────────────────────────────────────────────────────────────────────────

-- ── collection_schemas: the schema registry ────────────────────────────────
CREATE TABLE IF NOT EXISTS vibe.collection_schemas (
    collection_schema_id SERIAL       PRIMARY KEY,
    client_id            INTEGER      NOT NULL,
    collection           VARCHAR(100) NOT NULL,
    json_schema          JSONB        NOT NULL,
    version              INTEGER      NOT NULL DEFAULT 1,
    is_active            BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    created_by           INTEGER,
    updated_at           TIMESTAMPTZ,
    updated_by           INTEGER,
    is_system            BOOLEAN      NOT NULL DEFAULT FALSE,
    is_locked            BOOLEAN      NOT NULL DEFAULT FALSE,
    UNIQUE (client_id, collection, version)
);
CREATE INDEX IF NOT EXISTS idx_collection_schemas_client
    ON vibe.collection_schemas (client_id, collection);

ALTER TABLE vibe.collection_schemas ENABLE ROW LEVEL SECURITY;
ALTER TABLE vibe.collection_schemas FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON vibe.collection_schemas
    USING (client_id = current_setting('app.client_id', true)::integer OR client_id = 0)
    WITH CHECK (client_id = current_setting('app.client_id', true)::integer);

-- ── documents: the JSONB store, LIST-partitioned by tenant ─────────────────
-- NOTE: partitions are NOT tenant boundaries. A partition may hold one tenant
-- (dedicated), a set of tenants (shared pool), or every unassigned tenant
-- (default). Never treat "one partition" as "one tenant".
CREATE SEQUENCE IF NOT EXISTS vibe.documents_document_id_seq;

CREATE TABLE IF NOT EXISTS vibe.documents (
    document_id          INTEGER      NOT NULL DEFAULT nextval('vibe.documents_document_id_seq'::regclass),
    client_id            INTEGER      NOT NULL,
    user_id              INTEGER,
    collection           VARCHAR(100) NOT NULL,
    table_name           VARCHAR(100) NOT NULL,
    data                 JSONB        NOT NULL DEFAULT '{}'::jsonb,
    collection_schema_id INTEGER,
    created_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    created_by           INTEGER,
    updated_at           TIMESTAMPTZ,
    updated_by           INTEGER,
    deleted_at           TIMESTAMPTZ,          -- soft delete
    PRIMARY KEY (document_id, client_id)       -- partition key must be in the PK
) PARTITION BY LIST (client_id)
WITH (fillfactor = 90);                        -- JSONB is UPDATE-heavy; leave room for HOT

CREATE TABLE IF NOT EXISTS vibe.documents_default
    PARTITION OF vibe.documents DEFAULT;

ALTER TABLE vibe.documents
    ADD CONSTRAINT documents_collection_schema_id_fkey
    FOREIGN KEY (collection_schema_id)
    REFERENCES vibe.collection_schemas (collection_schema_id);

CREATE INDEX IF NOT EXISTS idx_documents_client
    ON vibe.documents (client_id);
CREATE INDEX IF NOT EXISTS idx_documents_tenant
    ON vibe.documents (client_id, user_id);
CREATE INDEX IF NOT EXISTS idx_documents_collection_table
    ON vibe.documents (client_id, user_id, collection, table_name);

-- Containment (@>) is the characteristic query of a JSONB document store, and
-- jsonb_path_ops is smaller and faster than the default for it.
-- DIVERGENCE: the previous schema file declared this index; the live instance
-- inspected on 2026-08-08 did NOT have it. Confirm intent before publishing —
-- either it was dropped for write throughput, or it was never created.
CREATE INDEX IF NOT EXISTS idx_documents_data_gin
    ON vibe.documents USING GIN (data jsonb_path_ops);

ALTER TABLE vibe.documents ENABLE ROW LEVEL SECURITY;
ALTER TABLE vibe.documents FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON vibe.documents
    USING (client_id = current_setting('app.client_id', true)::integer OR client_id = 0)
    WITH CHECK (client_id = current_setting('app.client_id', true)::integer);

-- ── encrypted_value_ownership: binds ciphertext to tenant and key ──────────
-- Restore documents without this and the ciphertext may be unclaimable.
CREATE TABLE IF NOT EXISTS vibe.encrypted_value_ownership (
    id              SERIAL      PRIMARY KEY,
    ciphertext_hash VARCHAR(64) NOT NULL,
    client_id       INTEGER     NOT NULL,
    key_id          INTEGER     NOT NULL,
    created_at      TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS ix_encrypted_value_ownership_client
    ON vibe.encrypted_value_ownership (client_id);
CREATE INDEX IF NOT EXISTS ix_encrypted_value_ownership_hash
    ON vibe.encrypted_value_ownership (ciphertext_hash);

ALTER TABLE vibe.encrypted_value_ownership ENABLE ROW LEVEL SECURITY;
ALTER TABLE vibe.encrypted_value_ownership FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON vibe.encrypted_value_ownership
    USING (client_id = current_setting('app.client_id', true)::integer OR client_id = 0)
    WITH CHECK (client_id = current_setting('app.client_id', true)::integer);

-- ── virtual_indexes: declared indexes over JSONB paths ─────────────────────
CREATE TABLE IF NOT EXISTS vibe.virtual_indexes (
    virtual_index_id      SERIAL       PRIMARY KEY,
    client_id             INTEGER      NOT NULL,
    collection            VARCHAR(100) NOT NULL,
    table_name            VARCHAR(100) NOT NULL,
    index_name            VARCHAR(200) NOT NULL,
    physical_index_name   VARCHAR(200) NOT NULL,
    index_definition      JSONB        NOT NULL,
    partition_name        VARCHAR(100) NOT NULL,
    created_at            TIMESTAMPTZ  DEFAULT NOW(),
    created_by            INTEGER,
    dropped_at            TIMESTAMPTZ,
    UNIQUE (client_id, collection, index_name)
);
CREATE INDEX IF NOT EXISTS idx_virtual_indexes_client
    ON vibe.virtual_indexes (client_id, collection);
CREATE INDEX IF NOT EXISTS idx_virtual_indexes_partition
    ON vibe.virtual_indexes (partition_name) WHERE dropped_at IS NULL;

ALTER TABLE vibe.virtual_indexes ENABLE ROW LEVEL SECURITY;
ALTER TABLE vibe.virtual_indexes FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON vibe.virtual_indexes
    USING (client_id = current_setting('app.client_id', true)::integer OR client_id = 0)
    WITH CHECK (client_id = current_setting('app.client_id', true)::integer);

-- ── tier_configurations / tier_features ────────────────────────────────────
-- tier_features has NO client_id but is NOT global: it is a child of
-- tier_configurations and is therefore tenant data reached by join. Anything
-- scoping a tenant by client_id alone will miss it entirely.
CREATE TABLE IF NOT EXISTS vibe.tier_configurations (
    tier_configuration_id INTEGER      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    client_id             INTEGER      NOT NULL,
    tier_key              VARCHAR(50)  NOT NULL,
    display_name          VARCHAR(100) NOT NULL,
    description           TEXT,
    sort_order            INTEGER      NOT NULL DEFAULT 0,
    is_default            BOOLEAN      NOT NULL DEFAULT FALSE,
    is_active             BOOLEAN      NOT NULL DEFAULT TRUE,
    monthly_price_cents   INTEGER      NOT NULL DEFAULT 0,
    stripe_price_id       VARCHAR(100),
    metadata              JSONB,
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    created_by            INTEGER,
    updated_at            TIMESTAMPTZ,
    updated_by            INTEGER
);
CREATE INDEX IF NOT EXISTS idx_tier_configurations_client
    ON vibe.tier_configurations (client_id);
CREATE INDEX IF NOT EXISTS idx_tier_configurations_client_tier_key
    ON vibe.tier_configurations (client_id, tier_key);

ALTER TABLE vibe.tier_configurations ENABLE ROW LEVEL SECURITY;
ALTER TABLE vibe.tier_configurations FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON vibe.tier_configurations
    USING (client_id = current_setting('app.client_id', true)::integer OR client_id = 0)
    WITH CHECK (client_id = current_setting('app.client_id', true)::integer);

CREATE TABLE IF NOT EXISTS vibe.tier_features (
    tier_feature_id       INTEGER      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tier_configuration_id INTEGER      NOT NULL
        REFERENCES vibe.tier_configurations (tier_configuration_id) ON DELETE CASCADE,
    feature_key           VARCHAR(100) NOT NULL,
    feature_name          VARCHAR(200) NOT NULL,
    is_enabled            BOOLEAN      NOT NULL DEFAULT TRUE,
    limit_value           INTEGER      NOT NULL DEFAULT 0,
    limit_period          VARCHAR(20),
    description           TEXT,
    sort_order            INTEGER      NOT NULL DEFAULT 0,
    metadata              JSONB,
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    created_by            INTEGER,
    updated_at            TIMESTAMPTZ,
    updated_by            INTEGER
);
CREATE INDEX IF NOT EXISTS idx_tier_features_tier_configuration
    ON vibe.tier_features (tier_configuration_id);
CREATE INDEX IF NOT EXISTS idx_tier_features_feature_key
    ON vibe.tier_features (feature_key);
CREATE INDEX IF NOT EXISTS idx_tier_features_tier_feature_key
    ON vibe.tier_features (tier_configuration_id, feature_key);

-- NOTE: no RLS. It has no client_id to filter on, and it is reachable by join
-- from tenant-scoped data. Whether that is acceptable is an open question —
-- isolation here depends entirely on callers always joining through
-- tier_configurations.

-- ── audit_logs ─────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS vibe.audit_logs (
    audit_log_id    BIGINT       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    client_id       INTEGER      NOT NULL,
    admin_user_id   INTEGER      NOT NULL,
    admin_email     VARCHAR(255),
    category        VARCHAR(50)  NOT NULL,
    action          VARCHAR(100) NOT NULL,
    target_type     VARCHAR(50),
    target_id       VARCHAR(100),
    description     TEXT         NOT NULL,
    previous_value  JSONB,
    new_value       JSONB,
    metadata        JSONB,
    ip_address      VARCHAR(45),
    user_agent      VARCHAR(500),
    request_path    VARCHAR(500),
    http_method     VARCHAR(10),
    is_success      BOOLEAN      NOT NULL DEFAULT TRUE,
    error_message   TEXT,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_description_length   CHECK (char_length(description) <= 10000),
    CONSTRAINT chk_error_message_length CHECK (char_length(error_message) <= 5000)
);
CREATE INDEX IF NOT EXISTS idx_audit_logs_client          ON vibe.audit_logs (client_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_client_created  ON vibe.audit_logs (client_id, created_at);
CREATE INDEX IF NOT EXISTS idx_audit_logs_client_category ON vibe.audit_logs (client_id, category);
CREATE INDEX IF NOT EXISTS idx_audit_logs_client_target   ON vibe.audit_logs (client_id, target_type, target_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_admin_user      ON vibe.audit_logs (admin_user_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_created_at      ON vibe.audit_logs (created_at);

ALTER TABLE vibe.audit_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE vibe.audit_logs FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON vibe.audit_logs
    USING (client_id = current_setting('app.client_id', true)::integer OR client_id = 0)
    WITH CHECK (client_id = current_setting('app.client_id', true)::integer);

-- ── feature_usage_logs ─────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS vibe.feature_usage_logs (
    feature_usage_log_id BIGINT       GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    client_id            INTEGER      NOT NULL,
    user_id              INTEGER,
    feature_key          VARCHAR(100) NOT NULL,
    period_type          VARCHAR(20)  NOT NULL DEFAULT 'monthly',
    period_start         TIMESTAMPTZ  NOT NULL,
    period_end           TIMESTAMPTZ  NOT NULL,
    usage_count          BIGINT       DEFAULT 0,
    period_limit         INTEGER      DEFAULT -1,
    limit_exceeded       BOOLEAN      DEFAULT FALSE,
    first_usage_at       TIMESTAMPTZ,
    last_usage_at        TIMESTAMPTZ,
    metadata             JSONB,
    created_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS idx_feature_usage_logs_client_period
    ON vibe.feature_usage_logs (client_id, period_start);
CREATE INDEX IF NOT EXISTS idx_feature_usage_logs_client_user_feature_period
    ON vibe.feature_usage_logs (client_id, user_id, feature_key, period_start);
CREATE INDEX IF NOT EXISTS idx_feature_usage_logs_feature_period
    ON vibe.feature_usage_logs (feature_key, period_start);
CREATE INDEX IF NOT EXISTS idx_feature_usage_logs_exceeded
    ON vibe.feature_usage_logs (limit_exceeded) WHERE limit_exceeded = TRUE;

ALTER TABLE vibe.feature_usage_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE vibe.feature_usage_logs FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON vibe.feature_usage_logs
    USING (client_id = current_setting('app.client_id', true)::integer OR client_id = 0)
    WITH CHECK (client_id = current_setting('app.client_id', true)::integer);
