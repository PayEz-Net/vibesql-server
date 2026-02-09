-- ========================================
-- Partitioned Parent Table (Shadow Table)
-- Date: 2026-02-07
-- Author: DotNetPert
-- Spec: VIBE-PARTITION-ARCHITECTURE.md Section 3.2
-- ========================================

-- IMPORTANT: This creates a SHADOW table alongside existing vibe.documents
-- Do NOT rename or drop the existing table until migration is validated

-- Create new partitioned parent table (schema matches existing documents)
CREATE TABLE IF NOT EXISTS vibe.documents_partitioned (
    document_id INTEGER NOT NULL DEFAULT nextval('vibe.documents_document_id_seq'::regclass),
    client_id INTEGER NOT NULL,
    user_id INTEGER,
    collection VARCHAR(100) NOT NULL,
    table_name VARCHAR(100) NOT NULL,
    data JSONB NOT NULL DEFAULT '{}'::jsonb,
    collection_schema_id INTEGER,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by INTEGER,
    updated_at TIMESTAMPTZ,
    updated_by INTEGER,
    deleted_at TIMESTAMPTZ,
    -- Partition key MUST be in primary key for LIST partitioning
    PRIMARY KEY (document_id, client_id)
) PARTITION BY LIST (client_id);

-- Create default partition (catches any unassigned clients)
CREATE TABLE IF NOT EXISTS vibe.documents_default
    PARTITION OF vibe.documents_partitioned DEFAULT;

-- Add FK to collection_schemas (matching existing documents table)
ALTER TABLE vibe.documents_partitioned
    ADD CONSTRAINT documents_partitioned_collection_schema_id_fkey
    FOREIGN KEY (collection_schema_id)
    REFERENCES vibe.collection_schemas(collection_schema_id)
    ON DELETE NO ACTION;

-- Create indexes on parent table (will be inherited by new partitions)
-- P0 FIX: Standalone index on partition key for efficient routing and JOINs
CREATE INDEX IF NOT EXISTS idx_documents_partitioned_client
    ON vibe.documents_partitioned(client_id);
CREATE INDEX IF NOT EXISTS idx_documents_partitioned_tenant
    ON vibe.documents_partitioned(client_id, user_id);
CREATE INDEX IF NOT EXISTS idx_documents_partitioned_collection_table
    ON vibe.documents_partitioned(client_id, user_id, collection, table_name);

-- Register default partition in partition_config
INSERT INTO vibe.partition_config (partition_name, schema_name, tier_level, is_accepting_new_clients, max_clients)
VALUES ('documents_default', 'vibe', 0, TRUE, 999999)
ON CONFLICT (partition_name) DO NOTHING;

COMMENT ON TABLE vibe.documents_partitioned IS
'Partitioned documents table. LIST partitioned by client_id for tenant isolation.';

COMMENT ON TABLE vibe.documents_default IS
'Default partition for unassigned client_ids. Clients here should be migrated to proper partitions.';

-- Verification query (run after migration)
-- \d vibe.documents_partitioned
-- Verify composite PK on (document_id, client_id)
