-- ========================================
-- Real-Time Sync Trigger
-- Date: 2026-02-07
-- Author: DotNetPert
-- Purpose: Sync INSERT/UPDATE/DELETE from vibe.documents to vibe.documents_partitioned
-- Note: This trigger is used during migration phase and will be removed after cutover
-- ========================================

-- Create sync function
CREATE OR REPLACE FUNCTION vibe.sync_to_partitioned()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        -- Insert into partitioned table
        INSERT INTO vibe.documents_partitioned (
            document_id, client_id, user_id, collection, table_name,
            data, collection_schema_id, created_at, created_by,
            updated_at, updated_by, deleted_at
        )
        VALUES (
            NEW.document_id, NEW.client_id, NEW.user_id, NEW.collection, NEW.table_name,
            NEW.data, NEW.collection_schema_id, NEW.created_at, NEW.created_by,
            NEW.updated_at, NEW.updated_by, NEW.deleted_at
        )
        ON CONFLICT (document_id, client_id) DO NOTHING;

        RETURN NEW;

    ELSIF TG_OP = 'UPDATE' THEN
        -- Update in partitioned table
        UPDATE vibe.documents_partitioned
        SET
            user_id = NEW.user_id,
            collection = NEW.collection,
            table_name = NEW.table_name,
            data = NEW.data,
            collection_schema_id = NEW.collection_schema_id,
            updated_at = NEW.updated_at,
            updated_by = NEW.updated_by,
            deleted_at = NEW.deleted_at
        WHERE document_id = NEW.document_id
          AND client_id = NEW.client_id;

        RETURN NEW;

    ELSIF TG_OP = 'DELETE' THEN
        -- Delete from partitioned table
        DELETE FROM vibe.documents_partitioned
        WHERE document_id = OLD.document_id
          AND client_id = OLD.client_id;

        RETURN OLD;
    END IF;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.sync_to_partitioned IS
'Syncs changes from vibe.documents to vibe.documents_partitioned in real-time. Used during migration phase. Will be removed after Week 4 cutover.';

-- Create trigger on vibe.documents
DROP TRIGGER IF EXISTS trg_sync_partitioned ON vibe.documents;

CREATE TRIGGER trg_sync_partitioned
AFTER INSERT OR UPDATE OR DELETE ON vibe.documents
FOR EACH ROW
EXECUTE FUNCTION vibe.sync_to_partitioned();

COMMENT ON TRIGGER trg_sync_partitioned ON vibe.documents IS
'Real-time sync to partitioned table during migration. Will be removed after cutover.';

-- Verification: Check trigger exists
-- SELECT tgname, tgrelid::regclass, tgenabled FROM pg_trigger WHERE tgname = 'trg_sync_partitioned';
