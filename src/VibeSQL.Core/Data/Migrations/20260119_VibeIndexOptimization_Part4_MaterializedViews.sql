-- ========================================
-- Vibe Database - Materialized Views (Part 4)
-- Migration: 20260119_VibeIndexOptimization_Part4_MaterializedViews
-- Date: 2026-01-19
-- Purpose: Create materialized views for dashboard analytics and reporting
-- Spec: VIBE_JSONB_OPTIMIZATION_SPEC.md Part 4
-- ========================================

-- UP MIGRATION
-- ========================================

BEGIN;

-- ========================================
-- Agent Analytics Materialized Views
-- ========================================

-- 4.1 Agent Statistics by Owner
CREATE MATERIALIZED VIEW IF NOT EXISTS vibe.mv_agent_stats AS
SELECT
  client_id,
  (data->>'owner_user_id')::int AS owner_user_id,
  data->>'is_active' AS is_active,
  COUNT(*) AS agent_count,
  MAX(created_at) AS latest_agent_created,
  MIN(created_at) AS first_agent_created
FROM vibe.documents
WHERE collection = 'agent_mail'
  AND table_name = 'agent_profiles'
  AND deleted_at IS NULL
GROUP BY client_id, (data->>'owner_user_id')::int, data->>'is_active';

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_agent_stats_pk
ON vibe.mv_agent_stats (client_id, owner_user_id, is_active);

COMMENT ON MATERIALIZED VIEW vibe.mv_agent_stats IS
'Pre-aggregated agent statistics by owner. Refresh periodically for dashboard display. Eliminates expensive COUNT queries.';

-- 4.2 Collection Statistics
CREATE MATERIALIZED VIEW IF NOT EXISTS vibe.mv_collection_stats AS
SELECT
  client_id,
  collection,
  table_name,
  COUNT(*) AS total_documents,
  COUNT(*) FILTER (WHERE deleted_at IS NULL) AS active_documents,
  COUNT(*) FILTER (WHERE deleted_at IS NOT NULL) AS deleted_documents,
  pg_size_pretty(pg_total_relation_size('vibe.documents')) AS table_size,
  MAX(created_at) AS latest_document,
  MIN(created_at) AS first_document
FROM vibe.documents
GROUP BY client_id, collection, table_name;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_collection_stats_pk
ON vibe.mv_collection_stats (client_id, collection, table_name);

COMMENT ON MATERIALIZED VIEW vibe.mv_collection_stats IS
'Collection-level statistics for admin dashboards. Shows document counts, storage size, and date ranges.';

-- 4.3 User Activity Summary
CREATE MATERIALIZED VIEW IF NOT EXISTS vibe.mv_user_activity AS
SELECT
  client_id,
  user_id,
  COUNT(*) AS total_documents,
  COUNT(DISTINCT collection) AS collections_used,
  MAX(created_at) AS last_activity,
  COUNT(*) FILTER (WHERE created_at > NOW() - INTERVAL '7 days') AS activity_7d,
  COUNT(*) FILTER (WHERE created_at > NOW() - INTERVAL '30 days') AS activity_30d
FROM vibe.documents
WHERE user_id IS NOT NULL
  AND deleted_at IS NULL
GROUP BY client_id, user_id;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_user_activity_pk
ON vibe.mv_user_activity (client_id, user_id);

CREATE INDEX IF NOT EXISTS idx_mv_user_activity_last_activity
ON vibe.mv_user_activity (last_activity DESC);

COMMENT ON MATERIALIZED VIEW vibe.mv_user_activity IS
'User activity metrics for engagement tracking. Shows recent activity counts and patterns.';

-- 4.4 Daily Document Creation Trends
CREATE MATERIALIZED VIEW IF NOT EXISTS vibe.mv_daily_trends AS
SELECT
  client_id,
  collection,
  DATE(created_at) AS activity_date,
  COUNT(*) AS documents_created,
  COUNT(DISTINCT user_id) AS unique_users
FROM vibe.documents
WHERE created_at > NOW() - INTERVAL '90 days'
  AND deleted_at IS NULL
GROUP BY client_id, collection, DATE(created_at);

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_daily_trends_pk
ON vibe.mv_daily_trends (client_id, collection, activity_date);

CREATE INDEX IF NOT EXISTS idx_mv_daily_trends_date
ON vibe.mv_daily_trends (activity_date DESC);

COMMENT ON MATERIALIZED VIEW vibe.mv_daily_trends IS
'Daily document creation trends for the last 90 days. Used for activity charts and growth tracking.';

-- ========================================
-- Refresh Functions
-- ========================================

-- 4.5 Refresh All Statistics (Concurrent)
CREATE OR REPLACE FUNCTION vibe.refresh_all_stats()
RETURNS TEXT AS $$
DECLARE
  start_time TIMESTAMPTZ;
  end_time TIMESTAMPTZ;
  duration INTERVAL;
BEGIN
  start_time := clock_timestamp();

  -- Refresh all materialized views concurrently (no locks)
  REFRESH MATERIALIZED VIEW CONCURRENTLY vibe.mv_agent_stats;
  REFRESH MATERIALIZED VIEW CONCURRENTLY vibe.mv_collection_stats;
  REFRESH MATERIALIZED VIEW CONCURRENTLY vibe.mv_user_activity;
  REFRESH MATERIALIZED VIEW CONCURRENTLY vibe.mv_daily_trends;

  end_time := clock_timestamp();
  duration := end_time - start_time;

  RETURN 'All materialized views refreshed successfully in ' || duration::text;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION vibe.refresh_all_stats IS
'Refreshes all dashboard materialized views concurrently. Call this periodically (e.g., every 15 minutes) or after bulk data changes.';

-- 4.6 Schedule Auto-Refresh (requires pg_cron extension)
-- Uncomment if pg_cron is installed
-- Example: SELECT cron.schedule refresh task every 15 minutes

-- Log migration completion
DO $$
BEGIN
    RAISE NOTICE '=================================================';
    RAISE NOTICE 'Migration 20260119_VibeIndexOptimization_Part4_MaterializedViews completed';
    RAISE NOTICE '=================================================';
    RAISE NOTICE 'Created 4 materialized views for dashboard analytics';
    RAISE NOTICE 'Created refresh_all_stats() function for periodic updates';
    RAISE NOTICE 'Run SELECT vibe.refresh_all_stats(); to populate views';
    RAISE NOTICE 'Recommended refresh schedule: every 15 minutes';
END $$;

COMMIT;


-- DOWN MIGRATION (Rollback)
-- ========================================

/*
BEGIN;

-- Drop materialized views
DROP MATERIALIZED VIEW IF EXISTS vibe.mv_agent_stats;
DROP MATERIALIZED VIEW IF EXISTS vibe.mv_collection_stats;
DROP MATERIALIZED VIEW IF EXISTS vibe.mv_user_activity;
DROP MATERIALIZED VIEW IF EXISTS vibe.mv_daily_trends;

-- Drop refresh function
DROP FUNCTION IF EXISTS vibe.refresh_all_stats;

RAISE NOTICE 'Migration 20260119_VibeIndexOptimization_Part4_MaterializedViews rolled back successfully';

COMMIT;
*/


-- VERIFICATION QUERIES
-- ========================================

-- List materialized views
SELECT
    schemaname,
    matviewname,
    pg_size_pretty(pg_total_relation_size(schemaname || '.' || matviewname)) as size,
    last_refresh
FROM pg_matviews
WHERE schemaname = 'vibe'
ORDER BY matviewname;

-- Initial refresh (populate materialized views)
SELECT vibe.refresh_all_stats();

-- Check agent stats
SELECT * FROM vibe.mv_agent_stats ORDER BY agent_count DESC LIMIT 10;

-- Check collection stats
SELECT * FROM vibe.mv_collection_stats ORDER BY active_documents DESC;

-- Check user activity
SELECT * FROM vibe.mv_user_activity ORDER BY last_activity DESC LIMIT 10;

-- Check daily trends
SELECT * FROM vibe.mv_daily_trends
WHERE activity_date > CURRENT_DATE - INTERVAL '30 days'
ORDER BY activity_date DESC, documents_created DESC
LIMIT 20;


-- USAGE EXAMPLES
-- ========================================

/*
-- Dashboard query: Agent counts by owner
SELECT
  owner_user_id,
  SUM(agent_count) FILTER (WHERE is_active = 'true') AS active_agents,
  SUM(agent_count) FILTER (WHERE is_active != 'true') AS inactive_agents
FROM vibe.mv_agent_stats
WHERE client_id = 1
GROUP BY owner_user_id
ORDER BY active_agents DESC;

-- Dashboard query: Collection growth
SELECT
  collection,
  activity_date,
  SUM(documents_created) OVER (
    PARTITION BY collection
    ORDER BY activity_date
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
  ) AS cumulative_documents
FROM vibe.mv_daily_trends
WHERE client_id = 1
  AND activity_date > CURRENT_DATE - INTERVAL '30 days'
ORDER BY collection, activity_date;

-- Dashboard query: Most active users
SELECT
  user_id,
  total_documents,
  collections_used,
  last_activity,
  activity_7d,
  activity_30d
FROM vibe.mv_user_activity
WHERE client_id = 1
ORDER BY activity_30d DESC
LIMIT 10;

-- Manual refresh (after bulk data changes)
SELECT vibe.refresh_all_stats();
*/


-- MAINTENANCE NOTES
-- ========================================

/*
1. Refresh Strategy:
   - Auto-refresh every 15 minutes with pg_cron (recommended)
   - Or manual refresh after bulk operations
   - CONCURRENTLY option prevents table locks during refresh

2. Refresh Performance:
   - Concurrent refresh requires UNIQUE index (already created)
   - First refresh populates data (can be slow for large tables)
   - Subsequent refreshes are incremental (fast)

3. Storage Impact:
   - Materialized views store pre-computed data
   - Typical overhead: 1-5% of base table size
   - Massive query performance improvement for dashboards

4. When to Use:
   - Dashboard queries run frequently (every page load)
   - Aggregation queries are expensive
   - Data doesn't need real-time accuracy (15-min lag acceptable)

5. When NOT to Use:
   - Real-time data requirements
   - Rarely-accessed analytics
   - Very frequently changing data (refresh overhead)
*/
