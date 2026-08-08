-- ═══════════════════════════════════════════════════════════════════════════
-- Audit log integrity — make a missing field fail loudly instead of writing
-- a row that says nothing.
--
-- WHY
-- vibe.audit_logs is the Req 10 / CC7 evidence table. Its repository was never
-- registered, so nothing wrote to it (see the wiring commit). As instrumentation
-- is added, a nullable column is a gap that INSERTS SUCCESSFULLY — the row lands,
-- the control reports healthy, and the missing field is discovered during an
-- audit rather than during development.
--
-- Measured on a live instance 2026-08-08 across the 99 historical rows:
--   ip_address     0/99 null     <- always captured
--   request_path   0/99 null     <- always captured
--   http_method    0/99 null     <- always captured
--   user_agent    88/99 null
--   target_type   82/99 null
--   target_id     82/99 null
--   admin_email   99/99 null     <- never once recorded, on an ADMIN audit table
--   previous_value 99/99 null
--   metadata      99/99 null
--   error_message 99/99 null
--
-- The old writer recorded that a request arrived from an IP, and almost nothing
-- about who acted or what they touched. That is not an audit trail.
--
-- SENTINEL
-- Historical rows are backfilled with 'unrecorded' rather than '' or 'unknown'.
-- An empty string is indistinguishable from a caller that supplied a blank, and
-- 'unknown' is a plausible real value. 'unrecorded' is greppable and self-
-- identifying: any row carrying it predates instrumentation, and an auditor can
-- tell that apart from a field the application actively chose not to fill.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── Tier 1: already 100% populated — constrain, no backfill needed ─────────
-- If any of these ever were null the ALTER fails, which is the correct outcome:
-- it means the measurement above no longer holds and this migration should be
-- re-examined rather than forced through.
ALTER TABLE vibe.audit_logs ALTER COLUMN ip_address   SET NOT NULL;
ALTER TABLE vibe.audit_logs ALTER COLUMN request_path SET NOT NULL;
ALTER TABLE vibe.audit_logs ALTER COLUMN http_method  SET NOT NULL;

-- ── Tier 2: should always be knowable at audit time — backfill, then constrain
-- These are the fields whose absence makes a row useless: who acted, and on what.
UPDATE vibe.audit_logs SET admin_email = 'unrecorded' WHERE admin_email IS NULL;
UPDATE vibe.audit_logs SET target_type = 'unrecorded' WHERE target_type IS NULL;
UPDATE vibe.audit_logs SET target_id   = 'unrecorded' WHERE target_id   IS NULL;
UPDATE vibe.audit_logs SET user_agent  = 'unrecorded' WHERE user_agent  IS NULL;

ALTER TABLE vibe.audit_logs ALTER COLUMN admin_email SET NOT NULL;
ALTER TABLE vibe.audit_logs ALTER COLUMN target_type SET NOT NULL;
ALTER TABLE vibe.audit_logs ALTER COLUMN target_id   SET NOT NULL;
ALTER TABLE vibe.audit_logs ALTER COLUMN user_agent  SET NOT NULL;

-- ── Tier 3: genuinely conditional — NOT NULL would be a lie ────────────────
-- previous_value has no meaning on a create; new_value has none on a read or a
-- delete; metadata is an optional extension point. Forcing a value here would
-- push callers to write '{}' everywhere, which is a null wearing a costume.
--
-- error_message is conditional too, but its condition is knowable, so state it
-- as a constraint instead: a FAILED audit event that cannot say why is exactly
-- the gap this migration exists to close.
ALTER TABLE vibe.audit_logs
    ADD CONSTRAINT chk_failure_has_reason
    CHECK (is_success OR error_message IS NOT NULL);

COMMIT;

-- ── Rollback ───────────────────────────────────────────────────────────────
-- Constraints only; no data is destroyed. The 'unrecorded' sentinels remain and
-- are intentionally left in place — they are the honest record of what those
-- rows contained.
--
-- BEGIN;
-- ALTER TABLE vibe.audit_logs DROP CONSTRAINT chk_failure_has_reason;
-- ALTER TABLE vibe.audit_logs ALTER COLUMN user_agent   DROP NOT NULL;
-- ALTER TABLE vibe.audit_logs ALTER COLUMN target_id    DROP NOT NULL;
-- ALTER TABLE vibe.audit_logs ALTER COLUMN target_type  DROP NOT NULL;
-- ALTER TABLE vibe.audit_logs ALTER COLUMN admin_email  DROP NOT NULL;
-- ALTER TABLE vibe.audit_logs ALTER COLUMN http_method  DROP NOT NULL;
-- ALTER TABLE vibe.audit_logs ALTER COLUMN request_path DROP NOT NULL;
-- ALTER TABLE vibe.audit_logs ALTER COLUMN ip_address   DROP NOT NULL;
-- COMMIT;
