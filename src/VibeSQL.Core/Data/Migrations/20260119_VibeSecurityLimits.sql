-- =====================================================================================
-- Vibe Database Security Limits - P0 CRITICAL
-- =====================================================================================
-- Purpose: Add resource limits to prevent exploitation and enforce tenant tier quotas
-- Date: 2026-01-19
-- Author: DotNetPert
-- QAPert Audit Response: Zero resource limits found - exploitable vulnerability
-- =====================================================================================

-- =====================================================================================
-- 1. DATABASE-LEVEL TIMEOUTS
-- =====================================================================================
-- Prevent runaway queries and lock contention

ALTER DATABASE vibe SET statement_timeout = '30s';
ALTER DATABASE vibe SET lock_timeout = '10s';
ALTER DATABASE vibe SET idle_in_transaction_session_timeout = '60s';

-- =====================================================================================
-- 2. TIER LIMITS LOOKUP TABLE
-- =====================================================================================
-- Define resource limits per tenant tier

CREATE TABLE IF NOT EXISTS vibe.tier_limits (
    tier_id INT PRIMARY KEY,
    tier_name VARCHAR(50) NOT NULL,
    max_doc_size_bytes INT NOT NULL,
    max_rows_per_collection INT NOT NULL,
    max_collections INT NOT NULL,
    max_storage_bytes BIGINT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Insert default tier limits
INSERT INTO vibe.tier_limits (tier_id, tier_name, max_doc_size_bytes, max_rows_per_collection, max_collections, max_storage_bytes)
VALUES
    (1, 'Free', 102400, 100, 5, 10485760),           -- 100KB doc, 100 rows, 5 collections, 10MB storage
    (2, 'Pro', 1048576, 1000, 50, 104857600),        -- 1MB doc, 1000 rows, 50 collections, 100MB storage
    (3, 'Enterprise', 10485760, 10000, 500, 1073741824)  -- 10MB doc, 10000 rows, 500 collections, 1GB storage
ON CONFLICT (tier_id) DO NOTHING;

-- =====================================================================================
-- 3. JSONB SIZE CONSTRAINT ON DOCUMENTS
-- =====================================================================================
-- Enforce maximum document size (10MB absolute limit - Enterprise tier max)

ALTER TABLE vibe.documents
DROP CONSTRAINT IF EXISTS chk_data_size;

ALTER TABLE vibe.documents
ADD CONSTRAINT chk_data_size
CHECK (pg_column_size(data) <= 10485760);

-- =====================================================================================
-- 4. TEXT FIELD LENGTH CONSTRAINTS
-- =====================================================================================
-- Prevent unbounded TEXT columns in audit_logs

ALTER TABLE vibe.audit_logs
DROP CONSTRAINT IF EXISTS chk_description_length;

ALTER TABLE vibe.audit_logs
DROP CONSTRAINT IF EXISTS chk_error_message_length;

ALTER TABLE vibe.audit_logs
ADD CONSTRAINT chk_description_length
CHECK (char_length(description) <= 10000);

ALTER TABLE vibe.audit_logs
ADD CONSTRAINT chk_error_message_length
CHECK (char_length(error_message) <= 5000);

-- =====================================================================================
-- 5. ROW COUNT ENFORCEMENT FUNCTION
-- =====================================================================================
-- Enforce max rows per collection per client_id+collection combination
-- NOTE: Uses Free tier (tier_id=1) as default until tier assignment is implemented
-- TODO: Integrate with tier_configurations table when client-tier mapping is available

CREATE OR REPLACE FUNCTION vibe.enforce_collection_row_limit()
RETURNS TRIGGER AS $$
DECLARE
    current_row_count INT;
    max_rows INT;
    default_tier_id INT := 1; -- Free tier (most restrictive)
BEGIN
    -- Get max rows for default tier (Free = 100 rows)
    SELECT max_rows_per_collection INTO max_rows
    FROM vibe.tier_limits
    WHERE tier_id = default_tier_id;

    -- Count current rows in this client's collection
    SELECT COUNT(*) INTO current_row_count
    FROM vibe.documents
    WHERE client_id = NEW.client_id
      AND collection = NEW.collection
      AND deleted_at IS NULL;

    -- Reject if over limit
    IF current_row_count >= max_rows THEN
        RAISE EXCEPTION 'Collection row limit exceeded. Collection "%:%" has % rows, max % allowed (tier: Free)',
            NEW.client_id, NEW.collection, current_row_count, max_rows
        USING ERRCODE = '22000'; -- data_exception
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.enforce_collection_row_limit() IS
'Prevents unbounded row growth per collection. Currently enforces Free tier limits (100 rows).
Integration with tier_configurations needed for per-client tier enforcement.';

-- =====================================================================================
-- 6. DOCUMENT SIZE ENFORCEMENT FUNCTION
-- =====================================================================================
-- Enforce max document size based on tier
-- NOTE: Table constraint enforces absolute 10MB limit; this function enforces tier-specific limits

CREATE OR REPLACE FUNCTION vibe.enforce_document_size_limit()
RETURNS TRIGGER AS $$
DECLARE
    doc_size INT;
    max_size INT;
    default_tier_id INT := 1; -- Free tier (most restrictive)
BEGIN
    -- Get document size
    doc_size := pg_column_size(NEW.data);

    -- Get max size for default tier (Free = 100KB)
    SELECT max_doc_size_bytes INTO max_size
    FROM vibe.tier_limits
    WHERE tier_id = default_tier_id;

    -- Reject if over tier limit
    IF doc_size > max_size THEN
        RAISE EXCEPTION 'Document size exceeds tier limit. Document: % bytes, Free tier max: % bytes (%.2f KB)',
            doc_size, max_size, max_size / 1024.0
        USING ERRCODE = '22000'; -- data_exception
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.enforce_document_size_limit() IS
'Enforces tier-specific document size limits. Currently uses Free tier (100KB).
Table constraint enforces absolute 10MB max. Integrate with tier_configurations for per-client limits.';

-- =====================================================================================
-- 7. COLLECTION COUNT ENFORCEMENT FUNCTION
-- =====================================================================================
-- Enforce max unique collections per client
-- NOTE: Counts distinct collection names, not collection_schemas records

CREATE OR REPLACE FUNCTION vibe.enforce_collection_count_limit()
RETURNS TRIGGER AS $$
DECLARE
    current_collection_count INT;
    max_collections_allowed INT;
    default_tier_id INT := 1; -- Free tier
BEGIN
    -- Get max collections for default tier (Free = 5 collections)
    SELECT max_collections INTO max_collections_allowed
    FROM vibe.tier_limits
    WHERE tier_id = default_tier_id;

    -- Count distinct collections for this client
    SELECT COUNT(DISTINCT collection) INTO current_collection_count
    FROM vibe.documents
    WHERE client_id = NEW.client_id
      AND deleted_at IS NULL;

    -- Check if this is a new collection name
    IF NOT EXISTS (
        SELECT 1 FROM vibe.documents
        WHERE client_id = NEW.client_id
          AND collection = NEW.collection
          AND deleted_at IS NULL
    ) THEN
        -- This would be a new collection, check if we're at limit
        IF current_collection_count >= max_collections_allowed THEN
            RAISE EXCEPTION 'Collection count limit exceeded. Client % has % collections, max % allowed (tier: Free)',
                NEW.client_id, current_collection_count, max_collections_allowed
            USING ERRCODE = '22000'; -- data_exception
        END IF;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.enforce_collection_count_limit() IS
'Prevents unlimited collection proliferation per client. Currently enforces Free tier (5 collections).
Integration with tier_configurations needed for per-client tier enforcement.';

-- =====================================================================================
-- 8. ATTACH TRIGGERS
-- =====================================================================================

-- Row count trigger on documents INSERT
DROP TRIGGER IF EXISTS trg_enforce_collection_row_limit ON vibe.documents;
CREATE TRIGGER trg_enforce_collection_row_limit
    BEFORE INSERT ON vibe.documents
    FOR EACH ROW
    EXECUTE FUNCTION vibe.enforce_collection_row_limit();

-- Document size trigger on documents INSERT/UPDATE
DROP TRIGGER IF EXISTS trg_enforce_document_size_limit ON vibe.documents;
CREATE TRIGGER trg_enforce_document_size_limit
    BEFORE INSERT OR UPDATE ON vibe.documents
    FOR EACH ROW
    EXECUTE FUNCTION vibe.enforce_document_size_limit();

-- Collection count trigger on documents INSERT (checks before creating new collection)
DROP TRIGGER IF EXISTS trg_enforce_collection_count_limit ON vibe.documents;
CREATE TRIGGER trg_enforce_collection_count_limit
    BEFORE INSERT ON vibe.documents
    FOR EACH ROW
    EXECUTE FUNCTION vibe.enforce_collection_count_limit();

-- =====================================================================================
-- MIGRATION COMPLETE
-- =====================================================================================
-- Security limits enforced:
-- 1. ✅ Database timeouts: 30s statement, 10s lock, 60s idle
-- 2. ✅ JSONB max size: 10MB (absolute table constraint)
-- 3. ✅ Row count: Free tier (100 rows per collection) enforced via trigger
-- 4. ✅ TEXT fields: 10K description, 5K error_message on audit_logs
-- 5. ✅ Tier limits: Free/Pro/Enterprise lookup table created
-- 6. ✅ Document size: Free tier (100KB) enforced via trigger
-- 7. ✅ Collection count: Free tier (5 collections) enforced via trigger
--
-- NEXT STEPS (Post-P0):
-- - Wire tier_configurations to clients for per-client tier assignment
-- - Update enforcement functions to query client's actual tier
-- - Add tier upgrade/downgrade logic in VibeApiService
-- - Implement tier limit caching for performance
-- =====================================================================================
