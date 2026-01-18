-- =====================================================
-- Weighing Transaction Table Partitioning Setup
-- =====================================================
-- This script implements monthly range partitioning for weighing_transactions
-- based on the weighed_at timestamp column.
--
-- Benefits:
-- - Improved query performance for date-range queries
-- - Easier data archival and purging of old data
-- - Better maintenance operations (VACUUM, ANALYZE)
-- - Partition pruning for faster scans
--
-- Partitioning Strategy: RANGE by weighed_at (monthly partitions)
--
-- NOTE: This script is designed for fresh installation (no data migration).
--       Partitions are created automatically via scheduled function.
-- =====================================================

-- =====================================================
-- Step 1: Create Future Partition Creation Function
-- =====================================================
-- This function creates partitions for a specified number of months ahead
CREATE OR REPLACE FUNCTION create_weighing_partitions(months_ahead INT DEFAULT 12, months_behind INT DEFAULT 1)
RETURNS TABLE(partition_name TEXT, start_date DATE, end_date DATE, action TEXT) AS $$
DECLARE
    i INT;
    partition_date DATE;
    part_name TEXT;
    p_start_date DATE;
    p_end_date DATE;
BEGIN
    -- Create partitions for past months (months_behind)
    FOR i IN REVERSE months_behind..1 LOOP
        partition_date := DATE_TRUNC('month', CURRENT_DATE - (i || ' months')::INTERVAL);
        part_name := 'weighing_transactions_' || TO_CHAR(partition_date, 'YYYY_MM');
        p_start_date := partition_date;
        p_end_date := partition_date + INTERVAL '1 month';

        IF NOT EXISTS (
            SELECT 1 FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relname = part_name
        ) THEN
            EXECUTE FORMAT(
                'CREATE TABLE IF NOT EXISTS %I PARTITION OF weighing_transactions FOR VALUES FROM (%L) TO (%L)',
                part_name, p_start_date, p_end_date
            );
            RETURN QUERY SELECT part_name, p_start_date, p_end_date, 'created'::TEXT;
        ELSE
            RETURN QUERY SELECT part_name, p_start_date, p_end_date, 'exists'::TEXT;
        END IF;
    END LOOP;

    -- Create current month partition
    partition_date := DATE_TRUNC('month', CURRENT_DATE);
    part_name := 'weighing_transactions_' || TO_CHAR(partition_date, 'YYYY_MM');
    p_start_date := partition_date;
    p_end_date := partition_date + INTERVAL '1 month';

    IF NOT EXISTS (
        SELECT 1 FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relname = part_name
    ) THEN
        EXECUTE FORMAT(
            'CREATE TABLE IF NOT EXISTS %I PARTITION OF weighing_transactions FOR VALUES FROM (%L) TO (%L)',
            part_name, p_start_date, p_end_date
        );
        RETURN QUERY SELECT part_name, p_start_date, p_end_date, 'created'::TEXT;
    ELSE
        RETURN QUERY SELECT part_name, p_start_date, p_end_date, 'exists'::TEXT;
    END IF;

    -- Create partitions for future months
    FOR i IN 1..months_ahead LOOP
        partition_date := DATE_TRUNC('month', CURRENT_DATE + (i || ' months')::INTERVAL);
        part_name := 'weighing_transactions_' || TO_CHAR(partition_date, 'YYYY_MM');
        p_start_date := partition_date;
        p_end_date := partition_date + INTERVAL '1 month';

        IF NOT EXISTS (
            SELECT 1 FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relname = part_name
        ) THEN
            EXECUTE FORMAT(
                'CREATE TABLE IF NOT EXISTS %I PARTITION OF weighing_transactions FOR VALUES FROM (%L) TO (%L)',
                part_name, p_start_date, p_end_date
            );
            RETURN QUERY SELECT part_name, p_start_date, p_end_date, 'created'::TEXT;
        ELSE
            RETURN QUERY SELECT part_name, p_start_date, p_end_date, 'exists'::TEXT;
        END IF;
    END LOOP;
END;
$$ LANGUAGE plpgsql;

-- =====================================================
-- Step 2: Create Yearly Partition Maintenance Function
-- =====================================================
-- This function ensures partitions exist for the entire current year
-- plus the next year (24 months coverage from January)
CREATE OR REPLACE FUNCTION ensure_yearly_partitions()
RETURNS TABLE(partition_name TEXT, start_date DATE, end_date DATE, action TEXT) AS $$
DECLARE
    current_year INT;
    next_year INT;
    month_num INT;
    partition_date DATE;
    part_name TEXT;
    p_start_date DATE;
    p_end_date DATE;
BEGIN
    current_year := EXTRACT(YEAR FROM CURRENT_DATE)::INT;
    next_year := current_year + 1;

    -- Create all 12 partitions for current year
    FOR month_num IN 1..12 LOOP
        partition_date := MAKE_DATE(current_year, month_num, 1);
        part_name := 'weighing_transactions_' || TO_CHAR(partition_date, 'YYYY_MM');
        p_start_date := partition_date;
        p_end_date := partition_date + INTERVAL '1 month';

        IF NOT EXISTS (
            SELECT 1 FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relname = part_name
        ) THEN
            EXECUTE FORMAT(
                'CREATE TABLE IF NOT EXISTS %I PARTITION OF weighing_transactions FOR VALUES FROM (%L) TO (%L)',
                part_name, p_start_date, p_end_date
            );
            RETURN QUERY SELECT part_name, p_start_date, p_end_date, 'created'::TEXT;
        ELSE
            RETURN QUERY SELECT part_name, p_start_date, p_end_date, 'exists'::TEXT;
        END IF;
    END LOOP;

    -- Create all 12 partitions for next year
    FOR month_num IN 1..12 LOOP
        partition_date := MAKE_DATE(next_year, month_num, 1);
        part_name := 'weighing_transactions_' || TO_CHAR(partition_date, 'YYYY_MM');
        p_start_date := partition_date;
        p_end_date := partition_date + INTERVAL '1 month';

        IF NOT EXISTS (
            SELECT 1 FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relname = part_name
        ) THEN
            EXECUTE FORMAT(
                'CREATE TABLE IF NOT EXISTS %I PARTITION OF weighing_transactions FOR VALUES FROM (%L) TO (%L)',
                part_name, p_start_date, p_end_date
            );
            RETURN QUERY SELECT part_name, p_start_date, p_end_date, 'created'::TEXT;
        ELSE
            RETURN QUERY SELECT part_name, p_start_date, p_end_date, 'exists'::TEXT;
        END IF;
    END LOOP;
END;
$$ LANGUAGE plpgsql;

-- =====================================================
-- Step 3: Create Archive Old Partitions Function
-- =====================================================
-- Detaches and optionally archives partitions older than specified months
CREATE OR REPLACE FUNCTION archive_old_weighing_partitions(months_to_keep INT DEFAULT 24, do_drop BOOLEAN DEFAULT FALSE)
RETURNS TABLE(partition_name TEXT, partition_date DATE, action TEXT) AS $$
DECLARE
    partition_record RECORD;
    cutoff_date DATE;
    part_year INT;
    part_month INT;
    part_date DATE;
BEGIN
    cutoff_date := DATE_TRUNC('month', CURRENT_DATE - (months_to_keep || ' months')::INTERVAL);

    FOR partition_record IN
        SELECT c.relname AS tablename
        FROM pg_class c
        JOIN pg_inherits i ON i.inhrelid = c.oid
        JOIN pg_class p ON p.oid = i.inhparent
        WHERE p.relname = 'weighing_transactions'
        AND c.relname LIKE 'weighing_transactions_20%'
        AND c.relname <> 'weighing_transactions_default'
    LOOP
        -- Extract date from partition name (weighing_transactions_YYYY_MM)
        BEGIN
            part_year := SUBSTRING(partition_record.tablename FROM 23 FOR 4)::INT;
            part_month := SUBSTRING(partition_record.tablename FROM 28 FOR 2)::INT;
            part_date := MAKE_DATE(part_year, part_month, 1);

            IF part_date < cutoff_date THEN
                -- Detach partition
                EXECUTE FORMAT('ALTER TABLE weighing_transactions DETACH PARTITION %I', partition_record.tablename);

                IF do_drop THEN
                    EXECUTE FORMAT('DROP TABLE %I', partition_record.tablename);
                    RETURN QUERY SELECT partition_record.tablename, part_date, 'dropped'::TEXT;
                ELSE
                    -- Move to archive schema if it exists
                    IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'archive') THEN
                        EXECUTE FORMAT('ALTER TABLE %I SET SCHEMA archive', partition_record.tablename);
                        RETURN QUERY SELECT partition_record.tablename, part_date, 'archived'::TEXT;
                    ELSE
                        RETURN QUERY SELECT partition_record.tablename, part_date, 'detached'::TEXT;
                    END IF;
                END IF;
            END IF;
        EXCEPTION WHEN OTHERS THEN
            RETURN QUERY SELECT partition_record.tablename, NULL::DATE, ('error: ' || SQLERRM)::TEXT;
        END;
    END LOOP;
END;
$$ LANGUAGE plpgsql;

-- =====================================================
-- Step 4: Create Monitoring View for Partition Statistics
-- =====================================================
CREATE OR REPLACE VIEW weighing_partition_stats AS
SELECT
    c.relname AS partition_name,
    pg_size_pretty(pg_total_relation_size(c.oid)) AS total_size,
    pg_size_pretty(pg_relation_size(c.oid)) AS table_size,
    pg_size_pretty(pg_indexes_size(c.oid)) AS indexes_size,
    s.n_tup_ins AS rows_inserted,
    s.n_live_tup AS live_rows,
    s.n_dead_tup AS dead_rows,
    s.last_vacuum,
    s.last_autovacuum,
    s.last_analyze,
    s.last_autoanalyze,
    -- Extract year and month from partition name
    CASE
        WHEN c.relname ~ 'weighing_transactions_\d{4}_\d{2}$'
        THEN MAKE_DATE(
            SUBSTRING(c.relname FROM 23 FOR 4)::INT,
            SUBSTRING(c.relname FROM 28 FOR 2)::INT,
            1
        )
        ELSE NULL
    END AS partition_month
FROM pg_class c
JOIN pg_inherits i ON i.inhrelid = c.oid
JOIN pg_class p ON p.oid = i.inhparent
LEFT JOIN pg_stat_user_tables s ON s.relname = c.relname
WHERE p.relname = 'weighing_transactions'
ORDER BY c.relname;

-- =====================================================
-- Step 5: Create Summary View for Quick Status Check
-- =====================================================
CREATE OR REPLACE VIEW weighing_partition_summary AS
SELECT
    COUNT(*) AS total_partitions,
    COUNT(*) FILTER (WHERE partition_month >= DATE_TRUNC('year', CURRENT_DATE)) AS current_year_partitions,
    COUNT(*) FILTER (WHERE partition_month >= DATE_TRUNC('year', CURRENT_DATE + INTERVAL '1 year')) AS next_year_partitions,
    MIN(partition_month) AS oldest_partition,
    MAX(partition_month) AS newest_partition,
    pg_size_pretty(SUM(pg_total_relation_size(partition_name::regclass))) AS total_size
FROM weighing_partition_stats
WHERE partition_month IS NOT NULL;

-- Grant permissions
GRANT SELECT ON weighing_partition_stats TO PUBLIC;
GRANT SELECT ON weighing_partition_summary TO PUBLIC;

-- =====================================================
-- Step 6: pg_cron Scheduling (requires pg_cron extension)
-- =====================================================
-- Run this section only if pg_cron extension is available
-- This automatically creates partitions for the next year on January 1st

DO $$
BEGIN
    -- Check if pg_cron extension exists
    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_cron') THEN
        -- Schedule yearly partition creation: runs on January 1st at 00:05
        PERFORM cron.schedule(
            'create-yearly-weighing-partitions',
            '5 0 1 1 *',  -- At 00:05 on January 1st
            'SELECT * FROM ensure_yearly_partitions()'
        );

        -- Schedule monthly partition maintenance: runs on 1st of every month at 00:10
        -- This ensures we always have partitions for 12 months ahead
        PERFORM cron.schedule(
            'maintain-weighing-partitions',
            '10 0 1 * *',  -- At 00:10 on 1st of every month
            'SELECT * FROM create_weighing_partitions(12, 1)'
        );

        -- Schedule archive check: runs quarterly (1st of Jan, Apr, Jul, Oct) at 01:00
        PERFORM cron.schedule(
            'archive-old-weighing-partitions',
            '0 1 1 1,4,7,10 *',  -- At 01:00 on 1st of Jan, Apr, Jul, Oct
            'SELECT * FROM archive_old_weighing_partitions(24, FALSE)'
        );

        RAISE NOTICE 'pg_cron jobs scheduled successfully for partition management';
    ELSE
        RAISE NOTICE 'pg_cron extension not installed. Partition management must be done manually or via application scheduler.';
        RAISE NOTICE 'To install: CREATE EXTENSION pg_cron; (requires postgresql.conf changes)';
        RAISE NOTICE 'Alternative: Schedule these calls via application or external cron:';
        RAISE NOTICE '  - Monthly: SELECT * FROM create_weighing_partitions(12, 1);';
        RAISE NOTICE '  - Yearly: SELECT * FROM ensure_yearly_partitions();';
        RAISE NOTICE '  - Quarterly: SELECT * FROM archive_old_weighing_partitions(24, FALSE);';
    END IF;
END $$;

-- =====================================================
-- Usage Instructions
-- =====================================================
--
-- 1. CREATE PARTITIONS (run once or via scheduler):
--    -- Create partitions for next 12 months:
--    SELECT * FROM create_weighing_partitions(12, 1);
--
--    -- Create partitions for current year + next year (24 months):
--    SELECT * FROM ensure_yearly_partitions();
--
-- 2. VIEW PARTITION STATUS:
--    -- Detailed stats per partition:
--    SELECT * FROM weighing_partition_stats;
--
--    -- Quick summary:
--    SELECT * FROM weighing_partition_summary;
--
-- 3. ARCHIVE OLD PARTITIONS:
--    -- Detach partitions older than 24 months (keeps them as standalone tables):
--    SELECT * FROM archive_old_weighing_partitions(24, FALSE);
--
--    -- Detach AND drop partitions older than 36 months:
--    SELECT * FROM archive_old_weighing_partitions(36, TRUE);
--
-- 4. LIST ALL PARTITIONS:
--    SELECT tablename FROM pg_tables
--    WHERE tablename LIKE 'weighing_transactions_%'
--    ORDER BY tablename;
--
-- 5. MANUAL PARTITION CREATION (if needed):
--    CREATE TABLE weighing_transactions_2027_01
--    PARTITION OF weighing_transactions
--    FOR VALUES FROM ('2027-01-01') TO ('2027-02-01');
--
-- =====================================================
-- Scheduling Options (if pg_cron not available)
-- =====================================================
--
-- Option A: Application-level scheduler (recommended)
--   Add to your application startup or a background service:
--   - On startup: Call create_weighing_partitions(12, 1)
--   - Monthly job: Call create_weighing_partitions(12, 1)
--
-- Option B: OS-level cron (Linux)
--   Add to crontab:
--   10 0 1 * * psql -d truload -c "SELECT create_weighing_partitions(12, 1);"
--   0 1 1 1 * psql -d truload -c "SELECT ensure_yearly_partitions();"
--
-- Option C: Windows Task Scheduler
--   Create scheduled task running:
--   psql -d truload -c "SELECT create_weighing_partitions(12, 1);"
--
-- =====================================================
