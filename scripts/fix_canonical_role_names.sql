-- =============================================================
-- Fix canonical client role names: {client_slug}_{role_suffix}
-- Run against: payez_idp database
-- =============================================================

BEGIN;

-- =============================================
-- Client 8: ideal_resume_website
-- =============================================
UPDATE core_identity.idp_client_roles
SET name = 'ideal_resume_website_admin'
WHERE idp_client_role_id = 5 AND name = 'ideal_resume_admin';

UPDATE core_identity.idp_client_roles
SET name = 'ideal_resume_website_support'
WHERE idp_client_role_id = 4 AND name = 'ideal_resume_support';

UPDATE core_identity.idp_client_roles
SET name = 'ideal_resume_website_user'
WHERE idp_client_role_id = 3 AND name = 'ideal_resume_user';

-- =============================================
-- Client 6: cryptaply_admin_web
-- =============================================
UPDATE core_identity.idp_client_roles
SET name = 'cryptaply_admin_web_admin'
WHERE idp_client_role_id = 6 AND name = 'cryptaply_admin';

UPDATE core_identity.idp_client_roles
SET name = 'cryptaply_admin_web_support'
WHERE idp_client_role_id = 7 AND name = 'cryptaply_support';

UPDATE core_identity.idp_client_roles
SET name = 'cryptaply_admin_web_user'
WHERE idp_client_role_id = 8 AND name = 'cryptaply_user';

-- =============================================
-- Client 4: cryptaply_user_web
-- =============================================
UPDATE core_identity.idp_client_roles
SET name = 'cryptaply_user_web_customer'
WHERE idp_client_role_id = 2 AND name = 'cryptaply_customer';

-- =============================================
-- Client 1: payez_payment_api
-- =============================================
UPDATE core_identity.idp_client_roles
SET name = 'payez_payment_api_user'
WHERE idp_client_role_id = 1 AND name = 'merchant_api_user';

COMMIT;

-- Verify
SELECT
  r.idp_client_role_id,
  r.name AS role_name,
  r.idp_client_id,
  c.name AS client_slug,
  CASE WHEN r.name LIKE c.name || '_%' THEN 'OK' ELSE 'MISMATCH' END AS status
FROM core_identity.idp_client_roles r
LEFT JOIN core_identity.idp_clients c ON r.idp_client_id = c.idp_client_id
ORDER BY r.idp_client_id, r.name;
