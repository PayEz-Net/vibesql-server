-- ========================================
-- Partition Management Tables
-- Date: 2026-02-07
-- Author: DotNetPert
-- Spec: VIBE-PARTITION-ARCHITECTURE.md Section 3.1
-- ========================================

-- Track client -> partition assignment
CREATE TABLE IF NOT EXISTS vibe.partition_assignments (
    partition_assignment_id SERIAL PRIMARY KEY,
    client_id INTEGER NOT NULL UNIQUE,
    tier_level INTEGER NOT NULL DEFAULT 1 CHECK (tier_level BETWEEN 0 AND 3),  -- 0=default, 1=shared, 2=dedicated, 3=enterprise
    partition_name VARCHAR(100) NOT NULL,
    schema_name VARCHAR(63) NOT NULL DEFAULT 'vibe',
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    migrated_at TIMESTAMPTZ,  -- When data migration completed
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    document_count BIGINT DEFAULT 0,
    last_stats_update TIMESTAMPTZ,
    metadata JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_partition_assignments_partition
    ON vibe.partition_assignments(partition_name);
CREATE INDEX IF NOT EXISTS idx_partition_assignments_tier
    ON vibe.partition_assignments(tier_level, is_active);

COMMENT ON TABLE vibe.partition_assignments IS
'Maps each client_id to its assigned partition. Used for routing queries and managing tier migrations.';

-- Partition configuration and health metrics
CREATE TABLE IF NOT EXISTS vibe.partition_config (
    partition_config_id SERIAL PRIMARY KEY,
    partition_name VARCHAR(100) NOT NULL UNIQUE,
    schema_name VARCHAR(63) NOT NULL DEFAULT 'vibe',
    tier_level INTEGER NOT NULL,
    client_count INTEGER DEFAULT 0,
    document_count BIGINT DEFAULT 0,
    estimated_size_bytes BIGINT DEFAULT 0,
    last_vacuum TIMESTAMPTZ,
    vacuum_duration_seconds INTEGER,
    last_analyze TIMESTAMPTZ,
    is_accepting_new_clients BOOLEAN DEFAULT TRUE,
    max_clients INTEGER DEFAULT 1000 CHECK (max_clients > 0),  -- For shared partitions
    split_threshold_gb INTEGER DEFAULT 50 CHECK (split_threshold_gb > 0),  -- When to consider splitting
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_partition_config_tier
    ON vibe.partition_config(tier_level);
CREATE INDEX IF NOT EXISTS idx_partition_config_accepting
    ON vibe.partition_config(is_accepting_new_clients, tier_level);

COMMENT ON TABLE vibe.partition_config IS
'Partition health metrics and configuration. Used for rebalancing and monitoring.';

-- Tier upgrade queue
CREATE TABLE IF NOT EXISTS vibe.tier_upgrade_queue (
    upgrade_id SERIAL PRIMARY KEY,
    client_id INTEGER NOT NULL,
    current_tier INTEGER NOT NULL,
    target_tier INTEGER NOT NULL,
    reason VARCHAR(100) NOT NULL,  -- 'document_count', 'query_volume', 'manual_request'
    threshold_value BIGINT,  -- The metric that triggered upgrade
    requested_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    scheduled_for TIMESTAMPTZ,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    status VARCHAR(20) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'in_progress', 'completed', 'failed')),
    error_message TEXT,
    metadata JSONB
);

CREATE INDEX IF NOT EXISTS idx_tier_upgrade_queue_status
    ON vibe.tier_upgrade_queue(status, scheduled_for);
CREATE INDEX IF NOT EXISTS idx_tier_upgrade_queue_client
    ON vibe.tier_upgrade_queue(client_id);

COMMENT ON TABLE vibe.tier_upgrade_queue IS
'Queue for automatic and manual tier upgrades. Background worker processes pending upgrades.';

-- Verification query (run after migration)
-- SELECT table_name FROM information_schema.tables
-- WHERE table_schema = 'vibe'
-- AND table_name IN ('partition_assignments', 'partition_config', 'tier_upgrade_queue');
-- Expected: 3 rows
