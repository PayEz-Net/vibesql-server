-- ============================================================================
-- VibeSQL Server — BASE SCHEMA (open source)
-- ============================================================================
-- The MINIMUM schema the server needs to run. Two tables. A cloner runs this
-- against a fresh Postgres and the server boots — no code-first migration, no
-- reverse-engineering entities.
--
-- This is the document-store ENGINE, not the PayEz production schema (which is
-- heavily customized and not open source). Everything here is generic.
--
-- HOW THE JSONB IS OPTIMIZED — the part that matters at scale:
--   1. LIST partitioning by client_id — multi-tenant isolation at the storage
--      layer. Each tenant is its own partition; queries prune to one partition.
--   2. GIN (data jsonb_path_ops) — arbitrary containment (@>) queries stay fast.
--      jsonb_path_ops is smaller and faster than the default jsonb_ops for the
--      containment queries a document store actually runs.
--   3. B-tree EXPRESSION indexes on hot JSONB keys — a query filtering on
--      data->>'user_id' hits a normal B-tree, not a JSONB scan. Promote YOUR
--      hot keys the same way (examples at the bottom).
--
-- Target: LARGE, PUBLIC, MULTI-TENANT. The three optimizations marked
-- [SCALE] below are added for that target and are the ones that stop a big
-- multi-tenant JSONB store from degrading. See notes at each.
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS vibe;

-- ── Sequence for document ids (shared across partitions) ───────────────────
CREATE SEQUENCE IF NOT EXISTS vibe.documents_document_id_seq;

-- ── collection_schemas: the schema registry ───────────────────────────────
-- Written by PUT /v1/schemas/{collection}, read by GET .../versions and by
-- Schema Sentinel (VS-SS) when it classifies a schema change.
CREATE TABLE IF NOT EXISTS vibe.collection_schemas (
    collection_schema_id SERIAL PRIMARY KEY,
    client_id            INTEGER      NOT NULL,
    collection           VARCHAR(100) NOT NULL,
    version              INTEGER      NOT NULL DEFAULT 1,
    json_schema          JSONB        NOT NULL DEFAULT '{}'::jsonb,
    is_active            BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    created_by           INTEGER,
    -- One active version per (client, collection); history kept by version.
    UNIQUE (client_id, collection, version)
);

-- Only the active row is hit on the read path; keep that lookup index-only.
CREATE INDEX IF NOT EXISTS idx_collection_schemas_active
    ON vibe.collection_schemas (client_id, collection)
    WHERE is_active;

-- ── documents: the JSONB document store, LIST-partitioned by tenant ────────
CREATE TABLE IF NOT EXISTS vibe.documents (
    document_id          INTEGER     NOT NULL DEFAULT nextval('vibe.documents_document_id_seq'::regclass),
    client_id            INTEGER     NOT NULL,
    collection           VARCHAR(100) NOT NULL,
    data                 JSONB       NOT NULL DEFAULT '{}'::jsonb,
    collection_schema_id INTEGER,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by           INTEGER,
    -- Partition key MUST be in the primary key for LIST partitioning.
    PRIMARY KEY (document_id, client_id)
) PARTITION BY LIST (client_id)
-- [SCALE #3] FILLFACTOR 90: JSONB documents are UPDATE-heavy. Leaving 10% free
-- space per page lets Postgres do HOT (heap-only tuple) updates that do NOT
-- touch every index — the single biggest anti-bloat lever for a mutable JSONB
-- store. Default 100 forces a new row version + all-index update on every edit.
WITH (fillfactor = 90);

-- Default partition catches any client without an explicit partition. In a
-- large deployment, create a partition per tenant (or HASH sub-partition a
-- busy one) instead of letting everything land here.
CREATE TABLE IF NOT EXISTS vibe.documents_default
    PARTITION OF vibe.documents DEFAULT;

ALTER TABLE vibe.documents
    ADD CONSTRAINT documents_collection_schema_id_fkey
    FOREIGN KEY (collection_schema_id)
    REFERENCES vibe.collection_schemas (collection_schema_id);

-- ── Optimization: arbitrary JSONB query ────────────────────────────────────
-- GIN with jsonb_path_ops — smaller and faster than default jsonb_ops for the
-- containment (@>) queries a document store runs. Inherited by all partitions.
CREATE INDEX IF NOT EXISTS idx_documents_data_gin
    ON vibe.documents USING GIN (data jsonb_path_ops);

-- ── [SCALE #1] The composite every multi-tenant query needs ────────────────
-- Partitioning prunes client_id, but WITHIN a large tenant's partition almost
-- every query still says "...AND collection = ?". Without this, that filter is
-- a partition scan. This is the most important index for tenant-scale reads.
CREATE INDEX IF NOT EXISTS idx_documents_client_collection
    ON vibe.documents (client_id, collection);

-- ── [SCALE #2] Index the foreign key ───────────────────────────────────────
-- An UNINDEXED FK makes every change to collection_schemas take a scan + a
-- lock on documents to check referencing rows. At scale that turns a schema
-- version bump into a stall. Index the referencing column.
CREATE INDEX IF NOT EXISTS idx_documents_collection_schema_id
    ON vibe.documents (collection_schema_id)
    WHERE collection_schema_id IS NOT NULL;

-- ── Optimization: promote HOT JSONB keys to B-tree expression indexes ──────
-- These are the GENERIC keys most document workloads filter on. A query like
-- `WHERE data->>'status' = 'active'` uses a B-tree here instead of scanning
-- JSONB. Partial (non-NULL) to stay small. Keep the ones you query; drop the
-- rest — every index is write cost.
CREATE INDEX IF NOT EXISTS idx_documents_data_user_id
    ON vibe.documents ((data->>'user_id'))    WHERE data->>'user_id' IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_documents_data_tenant_id
    ON vibe.documents ((data->>'tenant_id'))  WHERE data->>'tenant_id' IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_documents_data_status
    ON vibe.documents ((data->>'status'))     WHERE data->>'status' IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_documents_data_email
    ON vibe.documents ((data->>'email'))      WHERE data->>'email' IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_documents_data_created_at
    ON vibe.documents ((data->>'created_at')) WHERE data->>'created_at' IS NOT NULL;

-- ── EXAMPLE: collection-specific indexes (NOT part of the base engine) ──────
-- In production, PayEz adds indexes tuned to its own collections. Model your
-- own hot paths the same way; these are illustrative, commented out on purpose.
--
-- CREATE INDEX idx_documents_my_collection
--     ON vibe.documents (client_id, (data->>'my_hot_key'))
--     WHERE collection = 'my_collection';

-- ============================================================================
-- Verify: a fresh Postgres + this file + `dotnet run` should boot and let
-- PUT /v1/schemas/{c} then POST /v1/collections/{c}/tables/{t} round-trip.
-- ============================================================================
