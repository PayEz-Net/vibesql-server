-- =====================================================================================
-- Vibe System Collections - is_system and is_locked support
-- =====================================================================================
-- Purpose: Add system collection flags to exempt from tier limits and lock writes
-- Date: 2026-01-21
-- Author: DotNetPert
-- Spec: BAPert RE: System Collections Spec (Mail ID 2550)
-- =====================================================================================

-- =====================================================================================
-- 1. ADD COLUMNS TO collection_schemas
-- =====================================================================================

ALTER TABLE vibe.collection_schemas
ADD COLUMN IF NOT EXISTS is_system BOOLEAN NOT NULL DEFAULT false,
ADD COLUMN IF NOT EXISTS is_locked BOOLEAN NOT NULL DEFAULT false;

COMMENT ON COLUMN vibe.collection_schemas.is_system IS 'Exempt from tier limits (billing). System collections grow without quota checks.';
COMMENT ON COLUMN vibe.collection_schemas.is_locked IS 'Prevent all DML (INSERT/UPDATE/DELETE). For integrity-critical collections.';

-- =====================================================================================
-- 2. UPDATE ROW LIMIT TRIGGER - Skip is_system collections
-- =====================================================================================

CREATE OR REPLACE FUNCTION vibe.enforce_collection_row_limit()
RETURNS TRIGGER AS $$
DECLARE
    current_row_count INT;
    max_rows INT;
    default_tier_id INT := 1;
    collection_is_system BOOLEAN;
BEGIN
    -- Check if this collection is a system collection (exempt from limits)
    SELECT cs.is_system INTO collection_is_system
    FROM vibe.collection_schemas cs
    WHERE cs.collection_schema_id = NEW.collection_schema_id;

    -- Skip limit check for system collections
    IF collection_is_system = true THEN
        RETURN NEW;
    END IF;

    -- Get max rows for default tier
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
        USING ERRCODE = '22000';
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.enforce_collection_row_limit() IS
'Prevents unbounded row growth per collection. Skips is_system collections.
Currently enforces Free tier limits (100 rows) for non-system collections.';

-- =====================================================================================
-- 3. UPDATE DOCUMENT SIZE TRIGGER - Skip is_system collections
-- =====================================================================================

CREATE OR REPLACE FUNCTION vibe.enforce_document_size_limit()
RETURNS TRIGGER AS $$
DECLARE
    doc_size INT;
    max_size INT;
    default_tier_id INT := 1;
    collection_is_system BOOLEAN;
BEGIN
    -- Check if this collection is a system collection (exempt from limits)
    SELECT cs.is_system INTO collection_is_system
    FROM vibe.collection_schemas cs
    WHERE cs.collection_schema_id = NEW.collection_schema_id;

    -- Skip limit check for system collections
    IF collection_is_system = true THEN
        RETURN NEW;
    END IF;

    -- Get document size
    doc_size := pg_column_size(NEW.data);

    -- Get max size for default tier
    SELECT max_doc_size_bytes INTO max_size
    FROM vibe.tier_limits
    WHERE tier_id = default_tier_id;

    -- Reject if over tier limit
    IF doc_size > max_size THEN
        RAISE EXCEPTION 'Document size exceeds tier limit. Document: % bytes, Free tier max: % bytes (%.2f KB)',
            doc_size, max_size, max_size / 1024.0
        USING ERRCODE = '22000';
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.enforce_document_size_limit() IS
'Enforces tier-specific document size limits. Skips is_system collections.
Currently uses Free tier (100KB) for non-system collections.';

-- =====================================================================================
-- 4. UPDATE COLLECTION COUNT TRIGGER - Skip is_system collections
-- =====================================================================================

CREATE OR REPLACE FUNCTION vibe.enforce_collection_count_limit()
RETURNS TRIGGER AS $$
DECLARE
    current_collection_count INT;
    max_collections_allowed INT;
    default_tier_id INT := 1;
    collection_is_system BOOLEAN;
BEGIN
    -- Check if this collection is a system collection (exempt from limits)
    SELECT cs.is_system INTO collection_is_system
    FROM vibe.collection_schemas cs
    WHERE cs.collection_schema_id = NEW.collection_schema_id;

    -- Skip limit check for system collections
    IF collection_is_system = true THEN
        RETURN NEW;
    END IF;

    -- Get max collections for default tier
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
            USING ERRCODE = '22000';
        END IF;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.enforce_collection_count_limit() IS
'Prevents unlimited collection proliferation per client. Skips is_system collections.
Currently enforces Free tier (5 collections) for non-system collections.';

-- =====================================================================================
-- 5. ADD WRITE PROTECTION TRIGGER FOR LOCKED COLLECTIONS
-- =====================================================================================

CREATE OR REPLACE FUNCTION vibe.enforce_collection_lock()
RETURNS TRIGGER AS $$
DECLARE
    collection_is_locked BOOLEAN;
BEGIN
    -- For DELETE, use OLD; for INSERT/UPDATE use NEW
    IF TG_OP = 'DELETE' THEN
        SELECT cs.is_locked INTO collection_is_locked
        FROM vibe.collection_schemas cs
        WHERE cs.collection_schema_id = OLD.collection_schema_id;

        IF collection_is_locked = true THEN
            RAISE EXCEPTION 'Collection is locked. Cannot DELETE from locked collection (client_id: %, collection: %)',
                OLD.client_id, OLD.collection
            USING ERRCODE = '42501'; -- insufficient_privilege
        END IF;
        RETURN OLD;
    ELSE
        SELECT cs.is_locked INTO collection_is_locked
        FROM vibe.collection_schemas cs
        WHERE cs.collection_schema_id = NEW.collection_schema_id;

        IF collection_is_locked = true THEN
            RAISE EXCEPTION 'Collection is locked. Cannot % locked collection (client_id: %, collection: %)',
                TG_OP, NEW.client_id, NEW.collection
            USING ERRCODE = '42501'; -- insufficient_privilege
        END IF;
        RETURN NEW;
    END IF;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.enforce_collection_lock() IS
'Prevents all DML on locked collections. Admin can unlock via direct SQL update to is_locked.';

-- Attach lock trigger
DROP TRIGGER IF EXISTS trg_enforce_collection_lock ON vibe.documents;
CREATE TRIGGER trg_enforce_collection_lock
    BEFORE INSERT OR UPDATE OR DELETE ON vibe.documents
    FOR EACH ROW
    EXECUTE FUNCTION vibe.enforce_collection_lock();

-- =====================================================================================
-- 6. FLAG SYSTEM COLLECTIONS
-- =====================================================================================

-- Flag vibe_app as system (exempt from limits) and locked (no writes)
UPDATE vibe.collection_schemas
SET is_system = true, is_locked = true
WHERE collection = 'vibe_app';

-- =====================================================================================
-- VERIFICATION
-- =====================================================================================

SELECT
    collection_schema_id,
    client_id,
    collection,
    is_system,
    is_locked
FROM vibe.collection_schemas
WHERE is_system = true OR is_locked = true
ORDER BY collection, client_id;

-- =====================================================================================
-- MIGRATION COMPLETE
-- =====================================================================================
-- Changes:
-- 1. Added is_system column (exempt from tier limits)
-- 2. Added is_locked column (block all DML)
-- 3. Updated enforce_collection_row_limit() to skip is_system
-- 4. Updated enforce_document_size_limit() to skip is_system
-- 5. Updated enforce_collection_count_limit() to skip is_system
-- 6. Added enforce_collection_lock() trigger for is_locked
-- 7. Flagged vibe_app collections as is_system=true, is_locked=true
-- =====================================================================================
