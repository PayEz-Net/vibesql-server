-- ========================================
-- Partition Management Functions
-- Date: 2026-02-07
-- Author: DotNetPert
-- Spec: VIBE-PARTITION-ARCHITECTURE.md Sections 3.3-3.6
-- ========================================

-- ========================================
-- Function: create_shared_partition
-- Creates a shared partition for multiple clients (Tier 1)
-- P0 FIX: Added array validation (QAPert review)
-- P1 FIX: Added race condition handling (QAPert review)
-- ========================================
CREATE OR REPLACE FUNCTION vibe.create_shared_partition(
    p_partition_number INTEGER,
    p_client_ids INTEGER[]
) RETURNS TEXT AS $$
DECLARE
    v_partition_name TEXT;
    v_client_list TEXT;
    v_client_id INTEGER;
    v_unique_clients INTEGER[];
    v_partition_exists BOOLEAN;
BEGIN
    -- P0 FIX: Validate input array
    IF p_client_ids IS NULL THEN
        RAISE EXCEPTION 'client_ids cannot be NULL';
    END IF;

    IF array_length(p_client_ids, 1) IS NULL OR array_length(p_client_ids, 1) = 0 THEN
        RAISE EXCEPTION 'client_ids cannot be empty';
    END IF;

    IF array_length(p_client_ids, 1) > 1000 THEN
        RAISE EXCEPTION 'Shared partition cannot exceed 1000 clients (got %)', array_length(p_client_ids, 1);
    END IF;

    -- Remove duplicates from input array
    SELECT array_agg(DISTINCT x ORDER BY x) INTO v_unique_clients
    FROM unnest(p_client_ids) AS x;

    -- Generate partition name: documents_shared_0001, documents_shared_0002, etc.
    v_partition_name := format('documents_shared_%s', lpad(p_partition_number::text, 4, '0'));

    -- Build client list for VALUES clause
    v_client_list := array_to_string(v_unique_clients, ', ');

    -- P1 FIX: Check if partition already exists (race condition prevention)
    SELECT EXISTS (
        SELECT 1 FROM pg_tables
        WHERE schemaname = 'vibe' AND tablename = v_partition_name
    ) INTO v_partition_exists;

    IF NOT v_partition_exists THEN
        -- Create partition with VALUES list
        BEGIN
            EXECUTE format(
                'CREATE TABLE vibe.%I PARTITION OF vibe.documents_partitioned FOR VALUES IN (%s)',
                v_partition_name,
                v_client_list
            );
        EXCEPTION WHEN duplicate_table THEN
            -- Race condition: another process created the partition
            RAISE NOTICE 'Partition % already exists (concurrent creation)', v_partition_name;
        END;
    END IF;

    -- Register partition in config
    INSERT INTO vibe.partition_config (
        partition_name, schema_name, tier_level, client_count,
        is_accepting_new_clients, max_clients
    )
    VALUES (
        v_partition_name, 'vibe', 1, array_length(v_unique_clients, 1),
        array_length(v_unique_clients, 1) < 1000, 1000
    )
    ON CONFLICT (partition_name) DO UPDATE SET
        client_count = EXCLUDED.client_count,
        is_accepting_new_clients = EXCLUDED.is_accepting_new_clients,
        updated_at = NOW();

    -- Register each client in assignments
    FOREACH v_client_id IN ARRAY v_unique_clients
    LOOP
        INSERT INTO vibe.partition_assignments (
            client_id, tier_level, partition_name, schema_name
        )
        VALUES (
            v_client_id, 1, v_partition_name, 'vibe'
        )
        ON CONFLICT (client_id) DO UPDATE SET
            partition_name = EXCLUDED.partition_name,
            tier_level = 1,
            updated_at = NOW();
    END LOOP;

    RETURN format('Created shared partition %s with %s clients',
                  v_partition_name, array_length(v_unique_clients, 1));
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.create_shared_partition IS
'Creates a shared partition for multiple clients (Tier 1). Used for Free/Starter tier clients.
Validates: non-null, non-empty, max 1000 clients. Deduplicates input array.';

-- ========================================
-- Function: create_dedicated_partition
-- Creates a dedicated partition for a single client (Tier 2)
-- P1 FIX: Added race condition handling (QAPert review)
-- NOTE: This is a registration-only function. Data migration handled in Day 3.
-- ========================================
CREATE OR REPLACE FUNCTION vibe.create_dedicated_partition(
    p_client_id INTEGER
) RETURNS TEXT AS $$
DECLARE
    v_partition_name TEXT;
    v_old_partition TEXT;
    v_partition_exists BOOLEAN;
BEGIN
    -- Validate input
    IF p_client_id IS NULL OR p_client_id <= 0 THEN
        RAISE EXCEPTION 'client_id must be a positive integer';
    END IF;

    -- Generate partition name: documents_dedicated_c10001
    v_partition_name := format('documents_dedicated_c%s', p_client_id);

    -- Check if client exists in another partition
    SELECT partition_name INTO v_old_partition
    FROM vibe.partition_assignments
    WHERE client_id = p_client_id AND is_active = TRUE;

    -- Detach client from old partition if exists
    -- NOTE: This is registration-only. Data migration to new partition handled in Day 3.
    IF v_old_partition IS NOT NULL THEN
        -- Mark old assignment inactive
        UPDATE vibe.partition_assignments
        SET is_active = FALSE, updated_at = NOW()
        WHERE client_id = p_client_id AND partition_name = v_old_partition;

        -- Decrement client count on old partition
        UPDATE vibe.partition_config
        SET client_count = client_count - 1,
            is_accepting_new_clients = TRUE,
            updated_at = NOW()
        WHERE partition_name = v_old_partition;
    END IF;

    -- P1 FIX: Check if partition already exists (race condition prevention)
    SELECT EXISTS (
        SELECT 1 FROM pg_tables
        WHERE schemaname = 'vibe' AND tablename = v_partition_name
    ) INTO v_partition_exists;

    IF NOT v_partition_exists THEN
        -- Create dedicated partition
        BEGIN
            EXECUTE format(
                'CREATE TABLE vibe.%I PARTITION OF vibe.documents_partitioned FOR VALUES IN (%s)',
                v_partition_name,
                p_client_id
            );
        EXCEPTION WHEN duplicate_table THEN
            -- Race condition: another process created the partition
            RAISE NOTICE 'Partition % already exists (concurrent creation)', v_partition_name;
        END;
    END IF;

    -- Register partition in config
    INSERT INTO vibe.partition_config (
        partition_name, schema_name, tier_level, client_count,
        is_accepting_new_clients, max_clients
    )
    VALUES (
        v_partition_name, 'vibe', 2, 1, FALSE, 1
    )
    ON CONFLICT (partition_name) DO UPDATE SET
        tier_level = 2,
        updated_at = NOW();

    -- Register client assignment
    INSERT INTO vibe.partition_assignments (
        client_id, tier_level, partition_name, schema_name
    )
    VALUES (
        p_client_id, 2, v_partition_name, 'vibe'
    )
    ON CONFLICT (client_id) DO UPDATE SET
        partition_name = EXCLUDED.partition_name,
        tier_level = 2,
        is_active = TRUE,
        updated_at = NOW();

    RETURN format('Created dedicated partition %s for client %s',
                  v_partition_name, p_client_id);
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.create_dedicated_partition IS
'Creates a dedicated partition for a single client (Tier 2). Used for Pro tier clients.
NOTE: Registration-only. Data migration from old partition handled separately in Day 3.';

-- ========================================
-- Function: get_documents_table
-- Returns routing info for a client
-- ========================================
CREATE OR REPLACE FUNCTION vibe.get_documents_table(
    p_client_id INTEGER
) RETURNS TABLE (
    schema_name VARCHAR(63),
    table_name VARCHAR(100),
    tier_level INTEGER
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        pa.schema_name,
        pa.partition_name::VARCHAR(100) as table_name,
        pa.tier_level
    FROM vibe.partition_assignments pa
    WHERE pa.client_id = p_client_id
      AND pa.is_active = TRUE
    LIMIT 1;

    -- If no assignment found, return default partition
    IF NOT FOUND THEN
        RETURN QUERY
        SELECT
            'vibe'::VARCHAR(63) as schema_name,
            'documents_default'::VARCHAR(100) as table_name,
            0 as tier_level;
    END IF;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.get_documents_table IS
'Returns the partition routing info for a given client_id. Used for query routing.';

-- ========================================
-- Function: check_tier_upgrade_needed
-- Checks if a client should be upgraded to a higher tier
-- ========================================
CREATE OR REPLACE FUNCTION vibe.check_tier_upgrade_needed(
    p_client_id INTEGER
) RETURNS TABLE (
    needs_upgrade BOOLEAN,
    current_tier INTEGER,
    suggested_tier INTEGER,
    reason TEXT,
    document_count BIGINT
) AS $$
DECLARE
    v_doc_count BIGINT;
    v_current_tier INTEGER;
BEGIN
    -- Get current document count
    SELECT COUNT(*) INTO v_doc_count
    FROM vibe.documents_partitioned
    WHERE client_id = p_client_id AND deleted_at IS NULL;

    -- Get current tier
    SELECT pa.tier_level INTO v_current_tier
    FROM vibe.partition_assignments pa
    WHERE pa.client_id = p_client_id AND pa.is_active = TRUE;

    -- Default to Tier 1 if not found
    v_current_tier := COALESCE(v_current_tier, 1);

    -- Check upgrade conditions
    IF v_current_tier = 1 AND v_doc_count >= 10000 THEN
        -- Tier 1 -> Tier 2: Pro upgrade at 10K documents
        RETURN QUERY SELECT
            TRUE as needs_upgrade,
            v_current_tier as current_tier,
            2 as suggested_tier,
            'document_count >= 10000' as reason,
            v_doc_count as document_count;
    ELSIF v_current_tier = 2 AND v_doc_count >= 1000000 THEN
        -- Tier 2 -> Tier 3: Enterprise upgrade at 1M documents
        RETURN QUERY SELECT
            TRUE as needs_upgrade,
            v_current_tier as current_tier,
            3 as suggested_tier,
            'document_count >= 1000000' as reason,
            v_doc_count as document_count;
    ELSE
        -- No upgrade needed
        RETURN QUERY SELECT
            FALSE as needs_upgrade,
            v_current_tier as current_tier,
            v_current_tier as suggested_tier,
            NULL::TEXT as reason,
            v_doc_count as document_count;
    END IF;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.check_tier_upgrade_needed IS
'Checks if a client should be upgraded to a higher tier based on document count thresholds.';

-- ========================================
-- Function: vacuum_partition
-- Vacuums a specific partition and updates metrics
-- ========================================
CREATE OR REPLACE FUNCTION vibe.vacuum_partition(p_partition_name TEXT)
RETURNS TEXT AS $$
DECLARE
    v_start TIMESTAMPTZ;
    v_duration INTEGER;
BEGIN
    v_start := clock_timestamp();

    EXECUTE format('VACUUM ANALYZE vibe.%I', p_partition_name);

    v_duration := EXTRACT(EPOCH FROM (clock_timestamp() - v_start))::INTEGER;

    UPDATE vibe.partition_config
    SET last_vacuum = NOW(),
        last_analyze = NOW(),
        vacuum_duration_seconds = v_duration,
        updated_at = NOW()
    WHERE partition_name = p_partition_name;

    RETURN format('Vacuumed %s in %s seconds', p_partition_name, v_duration);
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.vacuum_partition IS
'Vacuums a specific partition and records duration in partition_config.';

-- ========================================
-- Function: vacuum_all_partitions
-- Vacuums all partitions that need maintenance
-- ========================================
CREATE OR REPLACE FUNCTION vibe.vacuum_all_partitions()
RETURNS TABLE (partition_name TEXT, result TEXT) AS $$
DECLARE
    v_partition RECORD;
BEGIN
    FOR v_partition IN
        SELECT pc.partition_name
        FROM vibe.partition_config pc
        WHERE pc.tier_level IN (1, 2)  -- Shared and dedicated partitions only
          AND (pc.last_vacuum IS NULL
               OR pc.last_vacuum < NOW() - INTERVAL '1 day')
        ORDER BY pc.last_vacuum NULLS FIRST
    LOOP
        partition_name := v_partition.partition_name;
        result := vibe.vacuum_partition(v_partition.partition_name);
        RETURN NEXT;
    END LOOP;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.vacuum_all_partitions IS
'Vacuums all partitions that have not been vacuumed in the last 24 hours.';

-- ========================================
-- View: v_partition_health
-- Partition health monitoring view
-- ========================================
CREATE OR REPLACE VIEW vibe.v_partition_health AS
SELECT
    pc.partition_name,
    pc.tier_level,
    CASE pc.tier_level
        WHEN 0 THEN 'Default'
        WHEN 1 THEN 'Shared'
        WHEN 2 THEN 'Dedicated'
        WHEN 3 THEN 'Enterprise'
    END as tier_name,
    pc.client_count,
    pc.document_count,
    pg_size_pretty(pg_total_relation_size('vibe.' || pc.partition_name)) as total_size,
    pg_size_pretty(pg_table_size('vibe.' || pc.partition_name)) as table_size,
    pg_size_pretty(pg_indexes_size('vibe.' || pc.partition_name)) as index_size,
    pc.last_vacuum,
    pc.vacuum_duration_seconds,
    CASE
        WHEN pc.last_vacuum IS NULL THEN 'Never'
        WHEN pc.last_vacuum < NOW() - INTERVAL '3 days' THEN 'Overdue'
        WHEN pc.last_vacuum < NOW() - INTERVAL '2 days' THEN 'Due Soon'
        ELSE 'OK'
    END as vacuum_status,
    pc.is_accepting_new_clients,
    pc.created_at
FROM vibe.partition_config pc
ORDER BY
    pc.tier_level,
    pg_total_relation_size('vibe.' || pc.partition_name) DESC;

COMMENT ON VIEW vibe.v_partition_health IS
'Partition health metrics including size, vacuum status, and client counts.';

-- Verification queries (run after migration)
-- SELECT vibe.create_shared_partition(1, ARRAY[1,2,3,4,5,6,7,8,9]);
-- SELECT tablename FROM pg_tables WHERE schemaname = 'vibe' AND tablename = 'documents_shared_0001';
-- SELECT * FROM vibe.get_documents_table(1);
-- SELECT * FROM vibe.v_partition_health;
