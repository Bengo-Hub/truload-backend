-- =====================================================
-- Set search_path so both public and weighing schema
-- tables are accessible. Materialized views are created
-- in public (first schema in path).
-- =====================================================
SET LOCAL search_path = public, weighing;

-- =====================================================
-- Materialized Views for Dashboard Performance
-- =====================================================
-- These materialized views pre-aggregate data for dashboards and Superset integration.
-- They significantly improve query performance by caching complex aggregations.
--
-- Refresh Strategy: Schedule periodic refreshes via cron or application code
-- =====================================================

-- =====================================================
-- 1. Daily Weighing Statistics by Station
-- =====================================================
CREATE MATERIALIZED VIEW mv_daily_weighing_stats AS
SELECT
    wt.organization_id,  -- From weighing_transactions
    wt.station_id,
    s.name AS station_name,
    DATE(wt.weighed_at) AS weighing_date,
    COUNT(*) AS total_weighings,
    COUNT(*) FILTER (WHERE wt."IsCompliant" = TRUE) AS compliant_count,
    COUNT(*) FILTER (WHERE wt."IsCompliant" = FALSE) AS non_compliant_count,
    COUNT(*) FILTER (WHERE wt."IsSentToYard" = TRUE) AS sent_to_yard_count,
    AVG(wt.gvw_measured_kg) AS avg_gvw_measured,
    AVG(wt.overload_kg) FILTER (WHERE wt.overload_kg > 0) AS avg_overload,
    MAX(wt.overload_kg) AS max_overload,
    SUM(wt.total_fee_usd) AS total_fees_collected,
    COUNT(DISTINCT wt.vehicle_id) AS unique_vehicles,
    COUNT(DISTINCT wt.transporter_id) AS unique_transporters
FROM weighing_transactions wt
INNER JOIN stations s ON s."Id" = wt.station_id
GROUP BY wt.organization_id, wt.station_id, s.name, DATE(wt.weighed_at);

-- Create indexes for fast lookups
CREATE UNIQUE INDEX idx_mv_daily_weighing_stats_unique
    ON mv_daily_weighing_stats (organization_id, station_id, weighing_date);

CREATE INDEX idx_mv_daily_weighing_stats_date
    ON mv_daily_weighing_stats (weighing_date DESC);

CREATE INDEX idx_mv_daily_weighing_stats_tenant
    ON mv_daily_weighing_stats (organization_id, station_id, weighing_date DESC);

-- =====================================================
-- 2. Charge Summaries (GVW vs Axle Best-Basis Analysis)
-- =====================================================
CREATE MATERIALIZED VIEW mv_charge_summaries AS
SELECT
    pc.id AS prosecution_case_id,
    pc.organization_id,
    cr.station_id,       -- Join from case_registers
    pc.case_register_id,
    cr.case_no,
    cr.vehicle_id,
    pc.weighing_id,
    pc.act_id,
    ad.name AS act_name,
    pc.gvw_overload_kg,
    pc.gvw_fee_usd,
    pc.max_axle_overload_kg,
    pc.max_axle_fee_usd,
    pc.best_charge_basis,
    pc.penalty_multiplier,
    pc.total_fee_usd,
    pc.total_fee_kes,
    pc.forex_rate,
    pc.status,
    pc.certificate_no,
    pc.created_at,
    CASE
        WHEN pc.best_charge_basis = 'gvw' THEN 'GVW Overload'
        WHEN pc.best_charge_basis = 'axle' THEN 'Axle Overload'
        ELSE 'Unknown'
    END AS charge_reason,
    CASE
        WHEN pc.best_charge_basis = 'gvw' THEN pc.gvw_fee_usd - pc.max_axle_fee_usd
        WHEN pc.best_charge_basis = 'axle' THEN pc.max_axle_fee_usd - pc.gvw_fee_usd
        ELSE 0
    END AS fee_difference_usd
FROM prosecution_cases pc
INNER JOIN case_registers cr ON cr.id = pc.case_register_id
INNER JOIN act_definitions ad ON ad.id = pc.act_id;

-- Create indexes
CREATE UNIQUE INDEX idx_mv_charge_summaries_unique
    ON mv_charge_summaries (prosecution_case_id);

CREATE INDEX idx_mv_charge_summaries_tenant
    ON mv_charge_summaries (organization_id, station_id);

-- =====================================================
-- 3. Axle Group Violation Patterns
-- =====================================================
CREATE MATERIALIZED VIEW mv_axle_group_violations AS
SELECT
    wt.organization_id,
    wt.station_id,
    wa.axle_grouping,
    tt.name AS tyre_type,
    COUNT(*) AS total_weighings,
    COUNT(*) FILTER (WHERE (wa.measured_weight_kg - wa.permissible_weight_kg) > 0) AS violations,
    ROUND(100.0 * COUNT(*) FILTER (WHERE (wa.measured_weight_kg - wa.permissible_weight_kg) > 0) / COUNT(*), 2) AS violation_rate_pct,
    AVG(wa.measured_weight_kg) AS avg_measured_weight,
    AVG(wa.permissible_weight_kg) AS avg_permissible_weight,
    AVG(wa.measured_weight_kg - wa.permissible_weight_kg) FILTER (WHERE (wa.measured_weight_kg - wa.permissible_weight_kg) > 0) AS avg_overload,
    MAX(wa.measured_weight_kg - wa.permissible_weight_kg) AS max_overload,
    SUM(wa.fee_usd) AS total_fees_generated,
    COUNT(DISTINCT wt.station_id) AS stations_with_violations,
    ARRAY_AGG(DISTINCT s.name) FILTER (WHERE (wa.measured_weight_kg - wa.permissible_weight_kg) > 0) AS violating_stations
FROM weighing_axles wa
INNER JOIN weighing_transactions wt ON wt.id = wa.weighing_id
INNER JOIN stations s ON s."Id" = wt.station_id
LEFT JOIN tyre_types tt ON tt."Id" = wa.tyre_type_id
GROUP BY wt.organization_id, wt.station_id, wa.axle_grouping, tt.name;

-- Create indexes
CREATE UNIQUE INDEX idx_mv_axle_group_violations_unique
    ON mv_axle_group_violations (organization_id, station_id, axle_grouping, tyre_type);

-- =====================================================
-- 4. Driver Demerit Rankings
-- =====================================================
CREATE MATERIALIZED VIEW mv_driver_demerit_rankings AS
SELECT
    cr.organization_id,
    d.id AS driver_id,
    d.id_number AS id_no_or_passport,
    d.full_names AS full_name,
    d.phone_number AS phone,
    d.email,
    COUNT(DISTINCT cr.id) AS total_cases,
    COUNT(DISTINCT cr.id) FILTER (WHERE cr.case_status_id IN (
        SELECT id FROM case_statuses WHERE name = 'closed'
    )) AS closed_cases,
    COUNT(DISTINCT cr.id) FILTER (WHERE cr.case_status_id IN (
        SELECT id FROM case_statuses WHERE name = 'open'
    )) AS open_cases,
    SUM(wt.overload_kg) AS total_overload_kg,
    SUM(wt.total_fee_usd) AS total_fees_charged,
    MAX(cr.created_at) AS last_violation_date,
    MAX(wt.overload_kg) AS max_single_overload_kg,
    COUNT(DISTINCT cr.id) FILTER (
        WHERE cr.created_at >= CURRENT_DATE - INTERVAL '12 months'
    ) > 1 AS is_repeat_offender,
    COUNT(DISTINCT aw.id) FILTER (WHERE aw."IsActive" = TRUE) AS active_warrants
FROM drivers d
LEFT JOIN case_registers cr ON cr.driver_id = d.id
LEFT JOIN weighing_transactions wt ON wt.id = cr.weighing_id
LEFT JOIN arrest_warrants aw ON aw.case_register_id = cr.id
GROUP BY cr.organization_id, d.id, d.id_number, d.full_names, d.phone_number, d.email
HAVING COUNT(DISTINCT cr.id) > 0;

-- Create indexes
CREATE UNIQUE INDEX idx_mv_driver_demerit_rankings_unique
    ON mv_driver_demerit_rankings (organization_id, driver_id);

-- =====================================================
-- 5. Vehicle Violation History Summary
-- =====================================================
CREATE MATERIALIZED VIEW mv_vehicle_violation_history AS
SELECT
    wt.organization_id,
    v.id AS vehicle_id,
    v.reg_no,
    v.make,
    v.model,
    v.vehicle_type,
    vo.full_name AS owner_name,
    t.name AS transporter_name,
    COUNT(DISTINCT wt.id) AS total_weighings,
    COUNT(DISTINCT wt.id) FILTER (WHERE wt."IsCompliant" = FALSE) AS violations,
    ROUND(100.0 * COUNT(DISTINCT wt.id) FILTER (WHERE wt."IsCompliant" = FALSE) / COUNT(DISTINCT wt.id), 2) AS violation_rate_pct,
    SUM(wt.overload_kg) FILTER (WHERE wt.overload_kg > 0) AS total_overload_kg,
    SUM(wt.total_fee_usd) AS total_fees_charged,
    MAX(wt.weighed_at) AS last_weighing_date,
    MAX(wt.overload_kg) AS max_overload_kg,
    EXISTS(
        SELECT 1 FROM vehicle_tags vt
        WHERE vt.reg_no = v.reg_no
        AND vt.status = 'open'
        AND vt.organization_id = wt.organization_id
    ) AS is_currently_tagged,
    EXISTS(
        SELECT 1 FROM yard_entries ye
        JOIN weighing_transactions wt2 ON wt2.id = ye.weighing_id
        WHERE wt2.vehicle_id = v.id
        AND ye.status IN ('pending', 'processing')
        AND ye.organization_id = wt.organization_id
    ) AS is_in_yard
FROM vehicles v
LEFT JOIN vehicle_owners vo ON vo.id = v.owner_id
LEFT JOIN transporters t ON t.id = v.transporter_id
LEFT JOIN weighing_transactions wt ON wt.vehicle_id = v.id
GROUP BY wt.organization_id, v.id, v.reg_no, v.make, v.model, v.vehicle_type, vo.full_name, t.name
HAVING COUNT(DISTINCT wt.id) > 0;

-- Create indexes
CREATE UNIQUE INDEX idx_mv_vehicle_violation_history_unique
    ON mv_vehicle_violation_history (organization_id, vehicle_id);

-- =====================================================
-- 6. Station Performance Scorecard
-- =====================================================
CREATE MATERIALIZED VIEW mv_station_performance_scorecard AS
SELECT
    s.organization_id,
    s."Id" AS station_id,
    s.code AS station_code,
    s.name AS station_name,
    s.station_type,
    r.name AS road_name,
    c."Name" AS county_name,
    COUNT(DISTINCT wt.id) AS total_weighings,
    COUNT(DISTINCT wt.id) FILTER (WHERE wt.weighed_at >= CURRENT_DATE - INTERVAL '30 days') AS weighings_last_30_days,
    COUNT(DISTINCT wt.id) FILTER (WHERE wt.weighed_at >= CURRENT_DATE - INTERVAL '7 days') AS weighings_last_7_days,
    ROUND(100.0 * COUNT(*) FILTER (WHERE wt."IsCompliant" = TRUE) / NULLIF(COUNT(*), 0), 2) AS compliance_rate_pct,
    SUM(wt.total_fee_usd) AS total_revenue_usd,
    SUM(wt.total_fee_usd) FILTER (WHERE wt.weighed_at >= CURRENT_DATE - INTERVAL '30 days') AS revenue_last_30_days,
    COUNT(DISTINCT wt.vehicle_id) AS unique_vehicles,
    COUNT(DISTINCT wt.transporter_id) AS unique_transporters,
    COUNT(DISTINCT ye.id) AS total_yard_entries,
    COUNT(DISTINCT ye.id) FILTER (WHERE ye.status IN ('pending', 'processing')) AS active_yard_entries,
    COUNT(DISTINCT cr.id) AS total_cases_generated,
    MAX(st.carried_at) AS last_scale_test_date,
    COUNT(DISTINCT st.id) FILTER (WHERE st.result = 'pass') AS passed_scale_tests,
    COUNT(DISTINCT st.id) FILTER (WHERE st.result = 'fail') AS failed_scale_tests
FROM stations s
LEFT JOIN roads r ON r.id = s.road_id
LEFT JOIN "Counties" c ON c."Id" = s.county_id
LEFT JOIN weighing_transactions wt ON wt.station_id = s."Id"
LEFT JOIN yard_entries ye ON ye.station_id = s."Id"
LEFT JOIN case_registers cr ON cr.weighing_id = wt.id
LEFT JOIN scale_tests st ON st.station_id = s."Id"
GROUP BY s.organization_id, s."Id", s.code, s.name, s.station_type, r.name, c."Name";

-- Create indexes
CREATE UNIQUE INDEX idx_mv_station_performance_scorecard_unique
    ON mv_station_performance_scorecard (organization_id, station_id);

-- =====================================================
-- Refresh Functions
-- =====================================================

-- Create function to refresh all materialized views
CREATE OR REPLACE FUNCTION refresh_all_materialized_views()
RETURNS VOID AS $$
BEGIN
    RAISE NOTICE 'Refreshing all materialized views...';

    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_daily_weighing_stats;
    RAISE NOTICE 'Refreshed: mv_daily_weighing_stats';

    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_charge_summaries;
    RAISE NOTICE 'Refreshed: mv_charge_summaries';

    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_axle_group_violations;
    RAISE NOTICE 'Refreshed: mv_axle_group_violations';

    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_driver_demerit_rankings;
    RAISE NOTICE 'Refreshed: mv_driver_demerit_rankings';

    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_vehicle_violation_history;
    RAISE NOTICE 'Refreshed: mv_vehicle_violation_history';

    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_station_performance_scorecard;
    RAISE NOTICE 'Refreshed: mv_station_performance_scorecard';

    RAISE NOTICE 'All materialized views refreshed successfully!';
END;
$$ LANGUAGE plpgsql;

-- Create function to refresh views incrementally (last 7 days only for large views)
CREATE OR REPLACE FUNCTION refresh_recent_materialized_views()
RETURNS VOID AS $$
BEGIN
    RAISE NOTICE 'Refreshing recent data in materialized views...';

    -- Full refresh for smaller views
    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_daily_weighing_stats;
    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_charge_summaries;

    -- These are aggregates so full refresh is needed
    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_axle_group_violations;
    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_driver_demerit_rankings;
    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_vehicle_violation_history;
    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_station_performance_scorecard;

    RAISE NOTICE 'Recent materialized views refreshed successfully!';
END;
$$ LANGUAGE plpgsql;

-- Grant permissions
GRANT SELECT ON mv_daily_weighing_stats TO PUBLIC;
GRANT SELECT ON mv_charge_summaries TO PUBLIC;
GRANT SELECT ON mv_axle_group_violations TO PUBLIC;
GRANT SELECT ON mv_driver_demerit_rankings TO PUBLIC;
GRANT SELECT ON mv_vehicle_violation_history TO PUBLIC;
GRANT SELECT ON mv_station_performance_scorecard TO PUBLIC;

-- =====================================================
-- Usage Instructions
-- =====================================================
-- To refresh all materialized views:
--   SELECT refresh_all_materialized_views();
--
-- To refresh only recent data:
--   SELECT refresh_recent_materialized_views();
--
-- To refresh a specific view:
--   REFRESH MATERIALIZED VIEW CONCURRENTLY mv_daily_weighing_stats;
--
-- To set up automatic refresh (run as superuser):
--   -- Option 1: Using pg_cron extension
--   SELECT cron.schedule('refresh-mv', '0 2 * * *', 'SELECT refresh_all_materialized_views()');
--
--   -- Option 2: Schedule via application code or external scheduler
-- =====================================================
