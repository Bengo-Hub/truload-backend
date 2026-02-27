-- =====================================================
-- Step 0: Convert weighing_transactions to Partitioned Table
-- =====================================================
-- Rename the existing table created by the base migration
ALTER TABLE weighing.weighing_transactions RENAME TO weighing_transactions_old;

-- Drop the old table cascade to clear PK, FKs and other constraints/indexes
DROP TABLE weighing.weighing_transactions_old CASCADE;

-- Recreate the table as a partitioned table
CREATE TABLE weighing.weighing_transactions (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    organization_id uuid NOT NULL,
    ticket_number character varying(50) NOT NULL,
    vehicle_id uuid NOT NULL,
    vehicle_reg_number character varying(20) NOT NULL,
    driver_id uuid,
    transporter_id uuid,
    weighed_by_user_id uuid NOT NULL,
    "WeighingType" character varying(20) NOT NULL,
    "ActId" uuid,
    "Bound" character varying(10),
    gvw_measured_kg integer NOT NULL,
    gvw_permissible_kg integer NOT NULL,
    overload_kg integer NOT NULL,
    control_status character varying(50) NOT NULL DEFAULT 'Pending',
    total_fee_usd numeric(18,2) NOT NULL DEFAULT 0,
    weighed_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    is_sync boolean NOT NULL DEFAULT false,
    "IsCompliant" boolean NOT NULL,
    "IsSentToYard" boolean NOT NULL,
    violation_reason character varying(1000) NOT NULL,
    "ViolationReasonEmbedding" vector(384),
    reweigh_cycle_no integer NOT NULL DEFAULT 0,
    original_weighing_id uuid,
    has_permit boolean NOT NULL DEFAULT false,
    "OriginId" uuid,
    "DestinationId" uuid,
    "CargoId" uuid,
    "ScaleTestId" uuid,
    "ToleranceApplied" boolean NOT NULL,
    "ReweighLimit" integer NOT NULL,
    "ClientLocalId" uuid,
    "SyncStatus" character varying(20) NOT NULL,
    "SyncAt" timestamp with time zone,
    "CaptureSource" character varying(20) NOT NULL,
    "CaptureStatus" character varying(20) NOT NULL,
    "AutoweighGvwKg" integer,
    "AutoweighAt" timestamp with time zone,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "DeletedAt" timestamp with time zone,
    station_id uuid NOT NULL,
    CONSTRAINT "PK_weighing_transactions" PRIMARY KEY (id, organization_id)
) PARTITION BY LIST (organization_id);

-- Create HNSW index for vector column on the partitioned parent
CREATE INDEX IF NOT EXISTS "idx_weighing_trans_vector" 
ON weighing.weighing_transactions USING hnsw ("ViolationReasonEmbedding" vector_cosine_ops);

-- Create other performance indices on the partitioned parent
-- NOTE: UNIQUE index on partitioned table MUST include the partition key (organization_id)
CREATE UNIQUE INDEX IF NOT EXISTS "idx_weighing_trans_ticket_org" 
ON weighing.weighing_transactions (ticket_number, organization_id);

CREATE INDEX IF NOT EXISTS "idx_weighing_trans_weighed_at" 
ON weighing.weighing_transactions (weighed_at);

CREATE INDEX IF NOT EXISTS "idx_weighing_trans_vehicle_id" 
ON weighing.weighing_transactions (vehicle_id);

-- =====================================================
-- Step 1: Create DEFAULT Partition
-- =====================================================
-- This catches any inserts for organizations that don't have a specific partition yet
CREATE TABLE IF NOT EXISTS weighing.weighing_transactions_default 
PARTITION OF weighing.weighing_transactions DEFAULT;

-- =====================================================
-- Step 2: Create Partition Creation Function
-- =====================================================
CREATE OR REPLACE FUNCTION weighing.create_organization_partition(org_id uuid)
RETURNS text AS $$
DECLARE
    part_name text;
    org_name_safe text;
BEGIN
    -- Create a safe name for the partition
    part_name := 'weighing_transactions_org_' || REPLACE(org_id::text, '-', '_');
    
    IF NOT EXISTS (
        SELECT 1 FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'weighing' AND c.relname = part_name
    ) THEN
        EXECUTE FORMAT(
            'CREATE TABLE weighing.%I PARTITION OF weighing.weighing_transactions FOR VALUES IN (%L)',
            part_name, org_id
        );
        -- Optionally copy indexes or add specific ones if needed (though they are inherited)
        RETURN 'created';
    ELSE
        RETURN 'exists';
    END IF;
END;
$$ LANGUAGE plpgsql;

-- =====================================================
-- Step 3: Create Partitions for Existing Organizations
-- =====================================================
DO $$
DECLARE
    org_record RECORD;
BEGIN
    FOR org_record IN SELECT "Id" FROM public.organizations LOOP
        PERFORM weighing.create_organization_partition(org_record."Id");
    END LOOP;
END $$;

-- =====================================================
-- Step 4: Create Trigger for Automatic Partition Creation
-- =====================================================
CREATE OR REPLACE FUNCTION weighing.trg_create_org_partition()
RETURNS TRIGGER AS $$
BEGIN
    PERFORM weighing.create_organization_partition(NEW."Id");
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_create_weighing_partition_on_org ON public.organizations;
CREATE TRIGGER trg_create_weighing_partition_on_org
AFTER INSERT ON public.organizations
FOR EACH ROW
EXECUTE FUNCTION weighing.trg_create_org_partition();

-- =====================================================
-- Step 5: Monitoring Views
-- =====================================================
CREATE OR REPLACE VIEW weighing.weighing_partition_stats AS
SELECT
    nmsp_parent.nspname AS parent_schema,
    parent.relname      AS parent_name,
    nmsp_child.nspname  AS child_schema,
    child.relname       AS partition_name,
    pg_size_pretty(pg_total_relation_size(child.oid)) AS total_size,
    pg_stat_get_live_tuples(child.oid) AS live_rows
FROM pg_inherits
JOIN pg_class parent      ON pg_inherits.inhparent = parent.oid
JOIN pg_class child       ON pg_inherits.inhrelid  = child.oid
JOIN pg_namespace nmsp_parent ON nmsp_parent.oid  = parent.relnamespace
JOIN pg_namespace nmsp_child  ON nmsp_child.oid   = child.relnamespace
WHERE parent.relname = 'weighing_transactions';

-- Grant permissions
GRANT SELECT ON weighing.weighing_partition_stats TO PUBLIC;
