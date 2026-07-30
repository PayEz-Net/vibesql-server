-- VibeSQL Schema Sentinel PostgreSQL Functions
-- Run these against your VibeSQL database to enable the Schema Sentinel service.

-- ============================================================
-- validate_schema_json(jsonb)
-- Validates a JSON schema definition and returns metadata.
-- Returns: table_count, is_valid, error_message
-- ============================================================
CREATE OR REPLACE FUNCTION vibe.validate_schema_json(p_schema jsonb)
RETURNS TABLE (
    table_count int,
    is_valid boolean,
    error_message text
) AS $$
DECLARE
    v_table_count int := 0;
    v_key text;
BEGIN
    -- Null check
    IF p_schema IS NULL THEN
        RETURN QUERY SELECT 0, false, 'Schema is null'::text;
        RETURN;
    END IF;

    -- Must be a JSON object
    IF jsonb_typeof(p_schema) != 'object' THEN
        RETURN QUERY SELECT 0, false, 'Schema must be a JSON object'::text;
        RETURN;
    END IF;

    -- Count tables by iterating top-level keys and excluding metadata
    FOR v_key IN SELECT jsonb_object_keys(p_schema)
    LOOP
        IF v_key NOT IN ('$schema', 'title', 'description', 'tableGroup', 'x-vibe-migrations') THEN
            v_table_count := v_table_count + 1;
        END IF;
    END LOOP;

    RETURN QUERY SELECT v_table_count, true, null::text;
END;
$$ LANGUAGE plpgsql IMMUTABLE;

COMMENT ON FUNCTION vibe.validate_schema_json(jsonb) IS
    'Validates a VibeSQL JSON schema and returns table count, validity, and any error message.';


-- ============================================================
-- cleanup_corrupted_schemas(dry_run boolean, max_table_threshold int)
-- Identifies and optionally repairs corrupted schema versions.
-- A schema is corrupted when validate_schema_json returns invalid
-- or when table count exceeds max_table_threshold.
-- Returns: action, collection_schema_id, client_id, collection,
--          version, table_count, details
-- ============================================================
CREATE OR REPLACE FUNCTION vibe.cleanup_corrupted_schemas(
    p_dry_run boolean DEFAULT true,
    p_max_table_threshold int DEFAULT 100
)
RETURNS TABLE (
    action text,
    collection_schema_id int,
    client_id int,
    collection text,
    version int,
    table_count int,
    details text
) AS $$
DECLARE
    r record;
    v_clean_version_id int;
    v_clean_version int;
    v_new_version int;
BEGIN
    FOR r IN
        SELECT
            cs.collection_schema_id,
            cs.client_id,
            cs.collection,
            cs.version,
            cs.is_system,
            cs.is_locked,
            v.table_count,
            v.is_valid,
            v.error_message
        FROM vibe.collection_schemas cs
        CROSS JOIN LATERAL vibe.validate_schema_json(cs.json_schema) v
        WHERE cs.is_active = true
          AND (NOT v.is_valid OR v.table_count > p_max_table_threshold)
        ORDER BY cs.client_id, cs.collection, cs.version DESC
    LOOP
        -- Find the most recent clean version for this collection
        SELECT cs2.collection_schema_id, cs2.version
        INTO v_clean_version_id, v_clean_version
        FROM vibe.collection_schemas cs2
        CROSS JOIN LATERAL vibe.validate_schema_json(cs2.json_schema) v2
        WHERE cs2.client_id = r.client_id
          AND cs2.collection = r.collection
          AND v2.is_valid = true
          AND v2.table_count <= p_max_table_threshold
          AND cs2.version < r.version
        ORDER BY cs2.version DESC
        LIMIT 1;

        IF v_clean_version_id IS NOT NULL THEN
            action := CASE WHEN p_dry_run THEN 'FLAG_ROLLBACK' ELSE 'ROLLBACK' END;
            details := format(
                'Corrupted version %s (tables: %s, valid: %s). Clean version %s available.',
                r.version, r.table_count, r.is_valid, v_clean_version
            );

            IF NOT p_dry_run THEN
                -- Deactivate corrupted version
                UPDATE vibe.collection_schemas
                SET is_active = false,
                    updated_at = now()
                WHERE collection_schema_id = r.collection_schema_id;

                -- Compute next version number
                SELECT COALESCE(MAX(version), 0) + 1
                INTO v_new_version
                FROM vibe.collection_schemas
                WHERE client_id = r.client_id
                  AND collection = r.collection;

                -- Create new active version from clean schema
                INSERT INTO vibe.collection_schemas (
                    client_id,
                    collection,
                    json_schema,
                    version,
                    is_active,
                    is_system,
                    is_locked,
                    created_at,
                    created_by
                )
                SELECT
                    r.client_id,
                    r.collection,
                    cs3.json_schema,
                    v_new_version,
                    true,
                    r.is_system,
                    r.is_locked,
                    now(),
                    NULL
                FROM vibe.collection_schemas cs3
                WHERE cs3.collection_schema_id = v_clean_version_id;
            END IF;
        ELSE
            action := 'FLAG_NO_CLEAN_VERSION';
            details := format(
                'Corrupted version %s (tables: %s, valid: %s). No clean version available.',
                r.version, r.table_count, r.is_valid
            );
        END IF;

        collection_schema_id := r.collection_schema_id;
        client_id := r.client_id;
        collection := r.collection;
        version := r.version;
        table_count := r.table_count;
        RETURN NEXT;
    END LOOP;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.cleanup_corrupted_schemas(boolean, int) IS
    'Identifies corrupted schema versions and optionally rolls them back to the last known good version.';
