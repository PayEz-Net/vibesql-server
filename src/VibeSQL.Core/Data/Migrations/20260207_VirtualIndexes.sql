-- ========================================
-- Virtual Indexes for User-Declarable JSONB Indexes
-- Date: 2026-02-07
-- Author: Zencoder AI Assistant
-- Spec: vibesql-platform-scaling-spec.md Problem 3
-- ========================================

-- Track user-declared virtual indexes on JSONB fields
CREATE TABLE IF NOT EXISTS vibe.virtual_indexes (
    virtual_index_id SERIAL PRIMARY KEY,
    client_id INTEGER NOT NULL,
    collection VARCHAR(100) NOT NULL,
    table_name VARCHAR(100) NOT NULL,
    index_name VARCHAR(200) NOT NULL,
    physical_index_name VARCHAR(200) NOT NULL,
    index_definition JSONB NOT NULL,
    partition_name VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    created_by INTEGER,
    dropped_at TIMESTAMPTZ,
    UNIQUE (client_id, collection, index_name)
);

COMMENT ON TABLE vibe.virtual_indexes IS 
'Tracks user-declared virtual indexes. Syncs with physical PostgreSQL indexes on partitions.';

COMMENT ON COLUMN vibe.virtual_indexes.virtual_index_id IS 'Primary key';
COMMENT ON COLUMN vibe.virtual_indexes.client_id IS 'Client who owns this index';
COMMENT ON COLUMN vibe.virtual_indexes.collection IS 'Collection name';
COMMENT ON COLUMN vibe.virtual_indexes.table_name IS 'Table within collection';
COMMENT ON COLUMN vibe.virtual_indexes.index_name IS 'Logical index name (user-facing)';
COMMENT ON COLUMN vibe.virtual_indexes.physical_index_name IS 'Physical PostgreSQL index name with hash suffix';
COMMENT ON COLUMN vibe.virtual_indexes.index_definition IS 'JSON definition: {fields: [...], partial: "...", unique: bool}';
COMMENT ON COLUMN vibe.virtual_indexes.partition_name IS 'Partition where physical index exists (e.g., documents_shared_0001)';
COMMENT ON COLUMN vibe.virtual_indexes.created_at IS 'When index was created';
COMMENT ON COLUMN vibe.virtual_indexes.created_by IS 'User ID who created the index';
COMMENT ON COLUMN vibe.virtual_indexes.dropped_at IS 'When index was dropped (NULL if active)';

-- Indexes on virtual_indexes table
CREATE INDEX IF NOT EXISTS idx_virtual_indexes_client 
    ON vibe.virtual_indexes(client_id, collection);

CREATE INDEX IF NOT EXISTS idx_virtual_indexes_partition 
    ON vibe.virtual_indexes(partition_name) 
    WHERE dropped_at IS NULL;

-- ========================================
-- Extend tier_limits for virtual index quotas
-- ========================================

-- Add max_virtual_indexes column if it doesn't exist
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'vibe' 
        AND table_name = 'tier_limits' 
        AND column_name = 'max_virtual_indexes'
    ) THEN
        ALTER TABLE vibe.tier_limits
        ADD COLUMN max_virtual_indexes INTEGER DEFAULT 5;
    END IF;
END $$;

COMMENT ON COLUMN vibe.tier_limits.max_virtual_indexes IS 
'Maximum number of virtual indexes allowed per collection for this tier';

-- Update existing tiers with index limits
UPDATE vibe.tier_limits 
SET max_virtual_indexes = 5 
WHERE tier_name = 'Free' AND max_virtual_indexes IS NULL;

UPDATE vibe.tier_limits 
SET max_virtual_indexes = 20 
WHERE tier_name = 'Pro' AND max_virtual_indexes IS NULL;

UPDATE vibe.tier_limits 
SET max_virtual_indexes = 100 
WHERE tier_name = 'Enterprise' AND max_virtual_indexes IS NULL;

-- Insert Starter tier if it doesn't exist
INSERT INTO vibe.tier_limits (
    tier_id,
    tier_name, 
    max_doc_size_bytes, 
    max_rows_per_collection, 
    max_collections, 
    max_storage_bytes,
    max_virtual_indexes
)
SELECT 
    4,         -- tier_id (Free=1, Pro=2, Enterprise=3, Starter=4)
    'Starter',
    524288,    -- 512KB max doc size
    2000,      -- 2000 rows per collection
    20,        -- 20 collections
    52428800,  -- 50MB storage
    20         -- 20 virtual indexes
WHERE NOT EXISTS (
    SELECT 1 FROM vibe.tier_limits WHERE tier_name = 'Starter'
);

-- ========================================
-- Verification Queries
-- ========================================

-- Verify virtual_indexes table exists
-- SELECT table_name FROM information_schema.tables 
-- WHERE table_schema = 'vibe' AND table_name = 'virtual_indexes';
-- Expected: 1 row

-- Verify tier_limits updated
-- SELECT tier_id, tier_name, max_virtual_indexes, max_collections, max_rows_per_collection 
-- FROM vibe.tier_limits 
-- ORDER BY tier_id;
-- Expected: 4 rows (Free=5, Pro=20, Enterprise=100, Starter=20)

-- Verify indexes on virtual_indexes
-- SELECT indexname FROM pg_indexes 
-- WHERE schemaname = 'vibe' AND tablename = 'virtual_indexes';
-- Expected: virtual_indexes_pkey, virtual_indexes_client_id_collection_index_name_key, 
--           idx_virtual_indexes_client, idx_virtual_indexes_partition
