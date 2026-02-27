-- =====================================================
-- Set search_path so both public and weighing schema
-- tables are accessible. Views are created in public
-- (first schema in path). SET LOCAL is transaction-scoped.
-- =====================================================
SET LOCAL search_path = public, weighing;

-- =====================================================
-- Regular Views for Active Data Filtering
-- =====================================================
-- These views provide filtered, real-time access to active data
-- by excluding soft-deleted records and applying common filters.
--
-- Benefits:
-- - Automatic filtering of deleted_at IS NULL
-- - Cleaner application queries
-- - Consistent data access patterns
-- - Real-time data (unlike materialized views)
-- =====================================================

-- =====================================================
-- 1. Active Vehicle Tags View
-- =====================================================
CREATE OR REPLACE VIEW active_vehicle_tags AS
SELECT
    vt.id,
    vt.organization_id,  -- From vehicle_tags
    vt.station_id,       -- Add station_id if available or join from stations
    vt.reg_no,
    vt.tag_type,
    vt.tag_category_id,
    tc.name AS tag_category_name,
    tc.description AS tag_category_description,
    vt.reason,
    vt.station_code,
    vt.status,
    vt.tag_photo_path,
    vt.effective_time_period,
    vt.created_by_id,
    u.user_name AS created_by_username,
    u.full_name AS created_by_full_name,
    vt.opened_at,
    CASE
        WHEN vt.effective_time_period IS NOT NULL
        THEN vt.opened_at + vt.effective_time_period
        ELSE NULL
    END AS expires_at,
    CASE
        WHEN vt.effective_time_period IS NOT NULL
        AND (vt.opened_at + vt.effective_time_period) < NOW()
        THEN TRUE
        ELSE FALSE
    END AS is_expired,
    EXTRACT(DAY FROM (NOW() - vt.opened_at)) AS days_open,
    vt.created_at,
    vt.updated_at
FROM vehicle_tags vt
INNER JOIN tag_categories tc ON tc.id = vt.tag_category_id
INNER JOIN asp_net_users u ON u."Id" = vt.created_by_id
WHERE vt.status = 'open'
AND vt.deleted_at IS NULL
AND tc.is_active = TRUE;

-- =====================================================
-- 2. Yard Status Summary View
-- =====================================================
CREATE OR REPLACE VIEW yard_status_summary AS
SELECT
    ye.id AS yard_entry_id,
    ye.organization_id,
    ye.station_id,
    ye.weighing_id,
    wt.ticket_number,
    wt.vehicle_reg_number,
    wt.vehicle_id,
    s.name AS station_name,
    s.code AS station_code,
    ye.reason AS entry_reason,
    ye.status,
    ye.entered_at,
    ye.released_at,
    CASE
        WHEN ye.released_at IS NOT NULL
        THEN EXTRACT(EPOCH FROM (ye.released_at - ye.entered_at)) / 3600
        ELSE EXTRACT(EPOCH FROM (NOW() - ye.entered_at)) / 3600
    END AS duration_hours,
    cr.id AS case_register_id,
    cr.case_no,
    cr.violation_details,
    sr.id AS special_release_id,
    rt.name AS release_type,
    sr.certificate_no AS release_memo_no,
    t.name AS transporter_name,
    t.phone AS transporter_phone,
    d.full_names AS driver_name,
    d.phone_number AS driver_phone,
    ye.created_at,
    ye.updated_at
FROM yard_entries ye
INNER JOIN weighing_transactions wt ON wt.id = ye.weighing_id AND wt.organization_id = ye.organization_id
INNER JOIN stations s ON s."Id" = ye.station_id
LEFT JOIN case_registers cr ON cr.yard_entry_id = ye.id AND cr.organization_id = ye.organization_id
LEFT JOIN special_releases sr ON sr.case_register_id = cr.id AND sr.organization_id = ye.organization_id
LEFT JOIN release_types rt ON rt.id = sr.release_type_id
LEFT JOIN vehicles v ON v.id = wt.vehicle_id
LEFT JOIN transporters t ON t.id = v.transporter_id
LEFT JOIN drivers d ON d.id = wt.driver_id
WHERE ye.deleted_at IS NULL
AND (ye.status IN ('pending', 'processing') OR ye.released_at >= NOW() - INTERVAL '7 days');

-- =====================================================
-- 3. Active Cases View
-- =====================================================
CREATE OR REPLACE VIEW active_cases AS
SELECT
    cr.id AS case_id,
    cr.organization_id,
    cr.station_id,
    cr.case_no,
    cr.weighing_id,
    wt.ticket_number,
    wt.vehicle_reg_number,
    cr.vehicle_id,
    v.reg_no,
    v.make,
    v.model,
    cr.driver_id,
    d.full_names AS driver_name,
    d.id_number AS driver_id_no,
    cr.violation_type_id,
    vty.name AS violation_type,
    vty.severity AS violation_severity,
    cr.violation_details,
    cr.act_id,
    ad.name AS act_name,
    cr.driver_ntac_no,
    cr.transporter_ntac_no,
    cr.ob_no,
    cr.court_id,
    crt.name AS court_name,
    cr.disposition_type_id,
    dt.name AS disposition_type,
    cr.case_status_id,
    cs.name AS case_status,
    cr.escalated_to_case_manager,
    cr.case_manager_id,
    cm_u.full_name AS case_manager_name,
    cr.prosecutor_id,
    cr.complainant_officer_id,
    cr.investigating_officer_id,
    cr.created_by_id,
    EXTRACT(DAY FROM (NOW() - cr.created_at)) AS days_open,
    cr.created_at,
    cr.updated_at
FROM case_registers cr
INNER JOIN case_statuses cs ON cs.id = cr.case_status_id
INNER JOIN violation_types vty ON vty.id = cr.violation_type_id
LEFT JOIN weighing_transactions wt ON wt.id = cr.weighing_id AND wt.organization_id = cr.organization_id
LEFT JOIN vehicles v ON v.id = cr.vehicle_id
LEFT JOIN drivers d ON d.id = cr.driver_id
LEFT JOIN act_definitions ad ON ad.id = cr.act_id
LEFT JOIN courts crt ON crt.id = cr.court_id
LEFT JOIN disposition_types dt ON dt.id = cr.disposition_type_id
LEFT JOIN case_managers cm ON cm.id = cr.case_manager_id
LEFT JOIN asp_net_users cm_u ON cm_u."Id" = cm.user_id
WHERE cr."DeletedAt" IS NULL
AND cs.name IN ('open', 'pending', 'escalated')
AND cr.closed_at IS NULL;

-- =====================================================
-- 4. Pending Court Hearings View
-- =====================================================
CREATE OR REPLACE VIEW pending_court_hearings AS
SELECT
    ch.id AS hearing_id,
    ch.organization_id,
    cr.station_id,       -- Join from case_registers
    ch.case_register_id,
    cr.case_no,
    ch.court_id,
    c.name AS court_name,
    c.location AS court_location,
    ch.hearing_date,
    ch.hearing_time,
    ch.hearing_type_id,
    ht.name AS hearing_type,
    ch.hearing_status_id,
    hs.name AS hearing_status,
    ch.hearing_outcome_id,
    ho.name AS hearing_outcome,
    ch.minute_notes,
    ch.next_hearing_date,
    ch.adjournment_reason,
    ch.presiding_officer,
    (ch.hearing_date - CURRENT_DATE) AS days_until_hearing,
    cr.vehicle_id,
    v.reg_no AS vehicle_reg_no,
    cr.driver_id,
    d.full_names AS driver_name,
    cr.violation_details,
    ch.created_at,
    ch.updated_at
FROM court_hearings ch
INNER JOIN case_registers cr ON cr.id = ch.case_register_id
INNER JOIN courts c ON c.id = ch.court_id
LEFT JOIN hearing_types ht ON ht.id = ch.hearing_type_id
LEFT JOIN hearing_statuses hs ON hs.id = ch.hearing_status_id
LEFT JOIN hearing_outcomes ho ON ho.id = ch.hearing_outcome_id
LEFT JOIN vehicles v ON v.id = cr.vehicle_id
LEFT JOIN drivers d ON d.id = cr.driver_id
WHERE ch."DeletedAt" IS NULL
AND (
    ch.hearing_date >= CURRENT_DATE - INTERVAL '7 days'
    OR ch.hearing_status_id IN (SELECT id FROM hearing_statuses WHERE name = 'scheduled')
);

-- =====================================================
-- 5. Active Arrest Warrants View
-- =====================================================
CREATE OR REPLACE VIEW active_arrest_warrants AS
SELECT
    aw.id AS warrant_id,
    aw.organization_id,
    cr.station_id,       -- Join from case_registers
    aw.warrant_no,
    aw.case_register_id,
    cr.case_no,
    aw.accused_name AS issued_against,
    aw.accused_id_no,
    aw.offence_description AS reason,
    aw.issued_by AS issued_by_court_officer,
    aw.issued_at,
    aw.warrant_status_id,
    aw."IsActive" AS is_active,
    aw.executed_at,
    aw.dropped_at,
    aw.execution_details,
    EXTRACT(DAY FROM (NOW() - aw.issued_at)) AS days_since_issued,
    cr.vehicle_id,
    v.reg_no AS vehicle_reg_no,
    cr.driver_id,
    d.full_names AS driver_name,
    d.phone_number AS driver_phone,
    cr.violation_details,
    aw.created_at
FROM arrest_warrants aw
INNER JOIN case_registers cr ON cr.id = aw.case_register_id
LEFT JOIN vehicles v ON v.id = cr.vehicle_id
LEFT JOIN drivers d ON d.id = cr.driver_id
WHERE aw."DeletedAt" IS NULL
AND aw."IsActive" = TRUE
AND aw.executed_at IS NULL
AND aw.dropped_at IS NULL;

-- =====================================================
-- 6. Compliant Weighings (Recent) View
-- =====================================================
CREATE OR REPLACE VIEW recent_compliant_weighings AS
SELECT
    wt.id AS weighing_id,
    wt.organization_id,
    wt.station_id,
    wt.ticket_number,
    wt.vehicle_reg_number,
    wt.vehicle_id,
    v.make,
    v.model,
    wt.driver_id,
    d.full_names AS driver_name,
    wt.transporter_id,
    t.name AS transporter_name,
    s.name AS station_name,
    s.code AS station_code,
    wt."WeighingType" AS weighing_type,
    wt.gvw_measured_kg,
    wt.gvw_permissible_kg,
    wt.overload_kg,
    wt.control_status,
    wt."IsCompliant" AS is_compliant,
    wt."ToleranceApplied" AS tolerance_applied,
    wt.weighed_at,
    wt."OriginId" AS origin_id,
    od_origin.name AS origin_name,
    wt."DestinationId" AS destination_id,
    od_dest.name AS destination_name,
    wt."CargoId" AS cargo_id,
    ct.name AS cargo_type
FROM weighing_transactions wt
INNER JOIN stations s ON s."Id" = wt.station_id
LEFT JOIN vehicles v ON v.id = wt.vehicle_id
LEFT JOIN drivers d ON d.id = wt.driver_id
LEFT JOIN transporters t ON t.id = v.transporter_id
LEFT JOIN origins_destinations od_origin ON od_origin.id = wt."OriginId"
LEFT JOIN origins_destinations od_dest ON od_dest.id = wt."DestinationId"
LEFT JOIN cargo_types ct ON ct.id = wt."CargoId"
WHERE wt."IsCompliant" = TRUE
AND wt.weighed_at >= NOW() - INTERVAL '30 days';

-- =====================================================
-- 7. Pending Special Releases View
-- =====================================================
CREATE OR REPLACE VIEW pending_special_releases AS
SELECT
    sr.id AS special_release_id,
    sr.organization_id,
    cr.station_id,       -- Join from case_registers
    sr.certificate_no AS release_memo_no,
    sr.case_register_id,
    cr.case_no,
    rt.name AS release_type,
    sr.authorized_by_id AS requested_by_id,
    u_req.full_name AS requested_by_name,
    sr."ApprovedById" AS approved_by_id,
    u_app.full_name AS approved_by_name,
    NULL::uuid AS approver_role_id,
    CASE
        WHEN sr."IsApproved" = TRUE THEN 'approved'
        WHEN sr."IsRejected" = TRUE THEN 'rejected'
        ELSE 'pending'
    END AS status,
    sr.reason,
    sr.issued_at AS requested_at,
    sr."ApprovedAt" AS approved_at,
    EXTRACT(DAY FROM (NOW() - sr.issued_at)) AS days_pending,
    cr.vehicle_id,
    v.reg_no AS vehicle_reg_no,
    cr.driver_id,
    d.full_names AS driver_name,
    cr.violation_details,
    cr.weighing_id,
    wt.ticket_number
FROM special_releases sr
INNER JOIN case_registers cr ON cr.id = sr.case_register_id AND cr.organization_id = sr.organization_id
INNER JOIN release_types rt ON rt.id = sr.release_type_id
INNER JOIN asp_net_users u_req ON u_req."Id" = sr.authorized_by_id
LEFT JOIN asp_net_users u_app ON u_app."Id" = sr."ApprovedById"
LEFT JOIN vehicles v ON v.id = cr.vehicle_id
LEFT JOIN drivers d ON d.id = cr.driver_id
LEFT JOIN weighing_transactions wt ON wt.id = cr.weighing_id AND wt.organization_id = sr.organization_id
WHERE sr."DeletedAt" IS NULL
AND sr."IsApproved" = FALSE
AND sr."IsRejected" = FALSE;

-- =====================================================
-- 8. Active Permits View
-- =====================================================
CREATE OR REPLACE VIEW active_permits AS
SELECT
    p.id AS permit_id,
    p.organization_id,
    NULL::uuid AS station_id, -- Permits are typically org-wide
    p.permit_no,
    p.vehicle_id,
    v.reg_no,
    v.make,
    v.model,
    p.permit_type_id,
    pt.name AS permit_type,
    pt.description AS permit_type_description,
    p.gvw_extension_kg,
    p.axle_extension_kg,
    p.issuing_authority,
    p.valid_from AS issue_date,
    p.valid_to AS expiry_date,
    EXTRACT(DAY FROM (p.valid_to - NOW())) AS days_until_expiry,
    p.valid_to <= NOW() + INTERVAL '7 days' AS is_expiring_soon,
    p.created_at,
    p."UpdatedAt" AS updated_at
FROM permits p
INNER JOIN vehicles v ON v.id = p.vehicle_id
INNER JOIN permit_types pt ON pt.id = p.permit_type_id
WHERE p."DeletedAt" IS NULL
AND p.valid_to >= NOW()
AND pt.is_active = TRUE;

-- Grant permissions to all views
GRANT SELECT ON active_vehicle_tags TO PUBLIC;
GRANT SELECT ON yard_status_summary TO PUBLIC;
GRANT SELECT ON active_cases TO PUBLIC;
GRANT SELECT ON pending_court_hearings TO PUBLIC;
GRANT SELECT ON active_arrest_warrants TO PUBLIC;
GRANT SELECT ON recent_compliant_weighings TO PUBLIC;
GRANT SELECT ON pending_special_releases TO PUBLIC;
GRANT SELECT ON active_permits TO PUBLIC;

-- =====================================================
-- Usage Instructions
-- =====================================================
-- Query active vehicle tags:
--   SELECT * FROM active_vehicle_tags WHERE reg_no = 'ABC123';
--
-- Query yard status:
--   SELECT * FROM yard_status_summary WHERE station_code = 'STA001';
--
-- Query active cases:
--   SELECT * FROM active_cases WHERE days_open > 30;
--
-- Query upcoming hearings:
--   SELECT * FROM pending_court_hearings WHERE days_until_hearing <= 7;
-- =====================================================
