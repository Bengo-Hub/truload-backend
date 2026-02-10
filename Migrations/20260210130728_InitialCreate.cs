using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "weighing");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "act_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    act_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    ChargingCurrency = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_act_definitions", x => x.id);
                    table.CheckConstraint("ck_act_definitions_act_type", "act_type IN ('EAC', 'Traffic')");
                });

            migrationBuilder.CreateTable(
                name: "application_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettingKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "text", nullable: false),
                    SettingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsEditable = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultValue = table.Column<string>(type: "text", nullable: true),
                    ValidationRules = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asp_net_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "axle_fee_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    legal_framework = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fee_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    overload_min_kg = table.Column<int>(type: "integer", nullable: false),
                    overload_max_kg = table.Column<int>(type: "integer", nullable: true),
                    fee_per_kg_usd = table.Column<decimal>(type: "numeric(10,4)", precision: 18, scale: 4, nullable: false),
                    flat_fee_usd = table.Column<decimal>(type: "numeric(10,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    demerit_points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    penalty_description = table.Column<string>(type: "text", maxLength: 500, nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_axle_fee_schedules", x => x.Id);
                    table.CheckConstraint("chk_axle_fee_schedules_dates", "\"effective_to\" IS NULL OR \"effective_to\" > \"effective_from\"");
                    table.CheckConstraint("chk_axle_fee_schedules_fee_type", "\"fee_type\" IN ('GVW', 'AXLE')");
                    table.CheckConstraint("chk_axle_fee_schedules_legal_framework", "\"legal_framework\" IN ('EAC', 'TRAFFIC_ACT')");
                    table.CheckConstraint("chk_axle_fee_schedules_overload_range", "\"overload_min_kg\" >= 0 AND (\"overload_max_kg\" IS NULL OR \"overload_max_kg\" >= \"overload_min_kg\")");
                    table.CheckConstraint("chk_fee_type", "fee_type IN ('GVW', 'AXLE')");
                    table.CheckConstraint("chk_legal_framework", "legal_framework IN ('EAC', 'TRAFFIC_ACT')");
                });

            migrationBuilder.CreateTable(
                name: "axle_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    typical_weight_kg = table.Column<int>(type: "integer", nullable: false),
                    min_spacing_feet = table.Column<decimal>(type: "numeric(4,1)", nullable: true),
                    max_spacing_feet = table.Column<decimal>(type: "numeric(4,1)", nullable: true),
                    axle_count_in_group = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_axle_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "axle_type_overload_fee_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    overload_min_kg = table.Column<int>(type: "integer", nullable: false),
                    overload_max_kg = table.Column<int>(type: "integer", nullable: true),
                    steering_axle_fee_usd = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    single_drive_axle_fee_usd = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    tandem_axle_fee_usd = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    tridem_axle_fee_usd = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    quad_axle_fee_usd = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    legal_framework = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_axle_type_overload_fee_schedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cargo_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "General"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cargo_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "case_managers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    specialization = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_managers", x => x.id);
                    table.CheckConstraint("ck_case_managers_role_type", "role_type IN ('case_manager', 'prosecutor', 'investigator')");
                });

            migrationBuilder.CreateTable(
                name: "case_review_statuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_review_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "case_statuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "closure_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_closure_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Counties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Counties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "courts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    court_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "magistrate"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courts", x => x.id);
                    table.CheckConstraint("chk_court_type", "court_type IN ('magistrate', 'high_court', 'appeal_court', 'supreme_court')");
                });

            migrationBuilder.CreateTable(
                name: "DatabaseSeedingHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeedingName = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseSeedingHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "demerit_point_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    violation_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    overload_min_kg = table.Column<int>(type: "integer", nullable: false),
                    overload_max_kg = table.Column<int>(type: "integer", nullable: true),
                    points = table.Column<int>(type: "integer", nullable: false),
                    legal_framework = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demerit_point_schedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "device_sync_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    device_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    sync_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "queued"),
                    sync_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_sync_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_sync_events", x => x.id);
                    table.CheckConstraint("chk_device_sync_attempts", "sync_attempts >= 0 AND sync_attempts <= 10");
                    table.CheckConstraint("chk_device_sync_entity_type", "entity_type IN ('weighing', 'case_register', 'yard_entry', 'vehicle_tag', 'special_release')");
                    table.CheckConstraint("chk_device_sync_operation", "operation IN ('create', 'update', 'delete')");
                    table.CheckConstraint("chk_device_sync_status", "sync_status IN ('queued', 'processing', 'synced', 'failed')");
                });

            migrationBuilder.CreateTable(
                name: "disposition_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disposition_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "drivers",
                schema: "weighing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ntsa_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    id_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    driving_license_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    full_names = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    surname = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    nationality = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    date_of_birth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    license_class = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    license_issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    license_expiry_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    license_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    is_professional_driver = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    current_demerit_points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    suspension_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    suspension_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drivers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hearing_outcomes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hearing_outcomes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hearing_statuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hearing_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hearing_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hearing_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "integration_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    encrypted_credentials = table.Column<string>(type: "text", nullable: false),
                    endpoints_json = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    webhook_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    callback_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    app_base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, defaultValue: "test"),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    credentials_rotated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_configs", x => x.id);
                    table.CheckConstraint("chk_integration_config_environment", "environment IN ('test', 'sandbox', 'production')");
                });

            migrationBuilder.CreateTable(
                name: "legal_sections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    legal_framework = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    section_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_sections", x => x.id);
                    table.CheckConstraint("CK_legal_sections_framework", "legal_framework IN ('CPC', 'PC', 'TRAFFIC_ACT', 'OTHER')");
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    org_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "origins_destinations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    location_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "city"),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "Kenya"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_origins_destinations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "penalty_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    points_min = table.Column<int>(type: "integer", nullable: false),
                    points_max = table.Column<int>(type: "integer", nullable: true),
                    penalty_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    suspension_days = table.Column<int>(type: "integer", nullable: true),
                    requires_court = table.Column<bool>(type: "boolean", nullable: false),
                    additional_fine_usd = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    additional_fine_kes = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_penalty_schedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permit_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    axle_extension_kg = table.Column<int>(type: "integer", nullable: false),
                    gvw_extension_kg = table.Column<int>(type: "integer", nullable: false),
                    validity_days = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permit_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "release_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subfile_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    example_documents = table.Column<string>(type: "text", nullable: true),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subfile_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tag_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tag_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tolerance_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    legal_framework = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tolerance_percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    tolerance_kg = table.Column<int>(type: "integer", nullable: true),
                    applies_to = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tolerance_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "transporters",
                schema: "weighing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    registration_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ntac_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transporters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tyre_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    typical_max_weight_kg = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tyre_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_makes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_makes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_owners",
                schema: "weighing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    id_no_or_passport = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ntac_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_owners", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "violation_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_violation_types", x => x.id);
                    table.CheckConstraint("ck_violation_types_severity", "severity IN ('low', 'medium', 'high', 'critical')");
                });

            migrationBuilder.CreateTable(
                name: "warrant_statuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warrant_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_shifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    total_hours_per_week = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    grace_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_shifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_asp_net_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "asp_net_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Districts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Districts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Districts_Counties_CountyId",
                        column: x => x.CountyId,
                        principalTable: "Counties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "driver_demerit_records",
                schema: "weighing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_register_id = table.Column<Guid>(type: "uuid", nullable: true),
                    weighing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    violation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    points_assigned = table.Column<int>(type: "integer", nullable: false),
                    fee_schedule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    legal_framework = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    violation_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    overload_kg = table.Column<int>(type: "integer", nullable: true),
                    penalty_amount_usd = table.Column<decimal>(type: "numeric(12,2)", nullable: false, defaultValue: 0m),
                    payment_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    points_expiry_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_expired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_demerit_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_driver_demerit_records_axle_fee_schedules_fee_schedule_id",
                        column: x => x.fee_schedule_id,
                        principalTable: "axle_fee_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_driver_demerit_records_drivers_driver_id",
                        column: x => x.driver_id,
                        principalSchema: "weighing",
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_departments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "FK_role_permissions_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "asp_net_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_registers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    case_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    weighing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    yard_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prohibition_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    violation_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    road_id = table.Column<Guid>(type: "uuid", nullable: true),
                    county_id = table.Column<Guid>(type: "uuid", nullable: true),
                    district_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subcounty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    violation_details = table.Column<string>(type: "text", nullable: true),
                    ViolationDetailsEmbedding = table.Column<Vector>(type: "vector(384)", nullable: true),
                    act_id = table.Column<Guid>(type: "uuid", nullable: true),
                    driver_ntac_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    transporter_ntac_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ob_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    court_id = table.Column<Guid>(type: "uuid", nullable: true),
                    disposition_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    case_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    escalated_to_case_manager = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    case_manager_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prosecutor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    complainant_officer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    investigating_officer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    investigating_officer_assigned_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    investigating_officer_assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closing_reason = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_registers", x => x.id);
                    table.ForeignKey(
                        name: "FK_case_registers_act_definitions_act_id",
                        column: x => x.act_id,
                        principalTable: "act_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_case_registers_case_managers_case_manager_id",
                        column: x => x.case_manager_id,
                        principalTable: "case_managers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_case_registers_case_statuses_case_status_id",
                        column: x => x.case_status_id,
                        principalTable: "case_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_case_registers_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_case_registers_disposition_types_disposition_type_id",
                        column: x => x.disposition_type_id,
                        principalTable: "disposition_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_case_registers_violation_types_violation_type_id",
                        column: x => x.violation_type_id,
                        principalTable: "violation_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shift_rotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    current_active_shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    run_duration = table.Column<int>(type: "integer", nullable: false),
                    run_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    break_duration = table.Column<int>(type: "integer", nullable: false),
                    break_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    next_change_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_rotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shift_rotations_work_shifts_current_active_shift_id",
                        column: x => x.current_active_shift_id,
                        principalTable: "work_shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "work_shift_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_time_str = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    end_time_str = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    start_time = table.Column<TimeSpan>(type: "interval", nullable: false),
                    end_time = table.Column<TimeSpan>(type: "interval", nullable: false),
                    break_hours = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    is_working_day = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_shift_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_shift_schedules_work_shifts_work_shift_id",
                        column: x => x.work_shift_id,
                        principalTable: "work_shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    road_class = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    district_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_length_km = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roads", x => x.id);
                    table.ForeignKey(
                        name: "FK_roads_Districts_district_id",
                        column: x => x.district_id,
                        principalTable: "Districts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "subcounties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    district_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subcounties", x => x.id);
                    table.ForeignKey(
                        name: "FK_subcounties_Districts_district_id",
                        column: x => x.district_id,
                        principalTable: "Districts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arrest_warrants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    case_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warrant_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issued_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    accused_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    accused_id_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    offence_description = table.Column<string>(type: "text", nullable: true),
                    warrant_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dropped_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    execution_details = table.Column<string>(type: "text", nullable: true),
                    dropped_reason = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arrest_warrants", x => x.id);
                    table.ForeignKey(
                        name: "FK_arrest_warrants_case_registers_case_register_id",
                        column: x => x.case_register_id,
                        principalTable: "case_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_arrest_warrants_warrant_statuses_warrant_status_id",
                        column: x => x.warrant_status_id,
                        principalTable: "warrant_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "case_closure_checklists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    case_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    closure_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    legal_section_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subfile_a_complete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    subfile_b_complete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    subfile_c_complete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    subfile_d_complete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    subfile_e_complete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    subfile_f_complete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    subfile_g_complete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    subfile_h_complete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    subfile_i_complete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    subfile_j_complete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    all_subfiles_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    review_status_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_requested_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_notes = table.Column<string>(type: "text", nullable: true),
                    approved_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verified_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_closure_checklists", x => x.id);
                    table.ForeignKey(
                        name: "FK_case_closure_checklists_case_registers_case_register_id",
                        column: x => x.case_register_id,
                        principalTable: "case_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_case_closure_checklists_case_review_statuses_review_status_~",
                        column: x => x.review_status_id,
                        principalTable: "case_review_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_case_closure_checklists_closure_types_closure_type_id",
                        column: x => x.closure_type_id,
                        principalTable: "closure_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_case_closure_checklists_legal_sections_legal_section_id",
                        column: x => x.legal_section_id,
                        principalTable: "legal_sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "case_subfiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    case_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subfile_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subfile_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    document_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    content = table.Column<string>(type: "text", nullable: true),
                    ContentEmbedding = table.Column<Vector>(type: "vector(384)", nullable: true),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    file_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_subfiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_case_subfiles_case_registers_case_register_id",
                        column: x => x.case_register_id,
                        principalTable: "case_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_case_subfiles_subfile_types_subfile_type_id",
                        column: x => x.subfile_type_id,
                        principalTable: "subfile_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "court_hearings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    case_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    court_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hearing_date = table.Column<DateTime>(type: "date", nullable: false),
                    hearing_time = table.Column<TimeSpan>(type: "time", nullable: true),
                    hearing_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hearing_status_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hearing_outcome_id = table.Column<Guid>(type: "uuid", nullable: true),
                    minute_notes = table.Column<string>(type: "text", nullable: true),
                    MinuteNotesEmbedding = table.Column<Vector>(type: "vector(384)", nullable: true),
                    next_hearing_date = table.Column<DateTime>(type: "date", nullable: true),
                    adjournment_reason = table.Column<string>(type: "text", nullable: true),
                    presiding_officer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_court_hearings", x => x.id);
                    table.ForeignKey(
                        name: "FK_court_hearings_case_registers_case_register_id",
                        column: x => x.case_register_id,
                        principalTable: "case_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_court_hearings_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_court_hearings_hearing_outcomes_hearing_outcome_id",
                        column: x => x.hearing_outcome_id,
                        principalTable: "hearing_outcomes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_court_hearings_hearing_statuses_hearing_status_id",
                        column: x => x.hearing_status_id,
                        principalTable: "hearing_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_court_hearings_hearing_types_hearing_type_id",
                        column: x => x.hearing_type_id,
                        principalTable: "hearing_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "special_releases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    case_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    certificate_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    release_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    overload_kg = table.Column<int>(type: "integer", nullable: true),
                    redistribution_allowed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    reweigh_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    reweigh_weighing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    compliance_achieved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    authorized_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsRejected = table.Column<bool>(type: "boolean", nullable: false),
                    RejectedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LoadCorrectionMemoId = table.Column<Guid>(type: "uuid", nullable: true),
                    ComplianceCertificateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_special_releases", x => x.id);
                    table.ForeignKey(
                        name: "FK_special_releases_case_registers_case_register_id",
                        column: x => x.case_register_id,
                        principalTable: "case_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_special_releases_release_types_release_type_id",
                        column: x => x.release_type_id,
                        principalTable: "release_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rotation_shifts",
                columns: table => new
                {
                    rotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rotation_shifts", x => new { x.rotation_id, x.work_shift_id });
                    table.ForeignKey(
                        name: "FK_rotation_shifts_shift_rotations_rotation_id",
                        column: x => x.rotation_id,
                        principalTable: "shift_rotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rotation_shifts_work_shifts_work_shift_id",
                        column: x => x.work_shift_id,
                        principalTable: "work_shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    station_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    road_id = table.Column<Guid>(type: "uuid", nullable: true),
                    county_id = table.Column<Guid>(type: "uuid", nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(10,8)", nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(11,8)", nullable: true),
                    supports_bidirectional = table.Column<bool>(type: "boolean", nullable: false),
                    bound_a_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bound_b_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stations_Counties_county_id",
                        column: x => x.county_id,
                        principalTable: "Counties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stations_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stations_roads_road_id",
                        column: x => x.road_id,
                        principalTable: "roads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asp_net_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asp_net_users_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_asp_net_users_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_asp_net_users_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "hardware_health_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    port = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    response_time_ms = table.Column<int>(type: "integer", nullable: true),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_critical = table.Column<bool>(type: "boolean", nullable: false),
                    checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    checked_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    metadata = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hardware_health_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hardware_health_logs_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "weighbridge_hardware",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    port = table.Column<int>(type: "integer", nullable: true),
                    manufacturer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_critical = table.Column<bool>(type: "boolean", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_online_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    polling_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weighbridge_hardware", x => x.Id);
                    table.ForeignKey(
                        name: "FK_weighbridge_hardware_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asp_net_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_asp_net_user_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "FK_asp_net_user_logins_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asp_net_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_asp_net_user_roles_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "asp_net_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_asp_net_user_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_tokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "FK_asp_net_user_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    RequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DenialReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequiredPermission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_logs_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "axle_configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    axle_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    axle_number = table.Column<int>(type: "integer", nullable: false),
                    gvw_permissible_kg = table.Column<int>(type: "integer", nullable: false),
                    is_standard = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    legal_framework = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "BOTH"),
                    visual_diagram_url = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_axle_configurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_axle_configurations_asp_net_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "case_assignment_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    case_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_officer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_officer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "initial"),
                    reason = table.Column<string>(type: "text", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_current = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    officer_rank = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_assignment_logs", x => x.id);
                    table.CheckConstraint("chk_case_assignment_type", "assignment_type IN ('initial', 're_assignment', 'transfer', 'handover')");
                    table.ForeignKey(
                        name: "FK_case_assignment_logs_asp_net_users_assigned_by_id",
                        column: x => x.assigned_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_case_assignment_logs_asp_net_users_new_officer_id",
                        column: x => x.new_officer_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_case_assignment_logs_asp_net_users_previous_officer_id",
                        column: x => x.previous_officer_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_case_assignment_logs_case_registers_case_register_id",
                        column: x => x.case_register_id,
                        principalTable: "case_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "case_parties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    case_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "defendant_driver"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vehicle_owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transporter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    external_id_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    external_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_currently_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    removed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_parties", x => x.id);
                    table.CheckConstraint("chk_case_party_role", "party_role IN ('investigating_officer', 'ocs', 'arresting_officer', 'prosecutor', 'defendant_driver', 'defendant_owner', 'defendant_transporter', 'witness', 'complainant')");
                    table.ForeignKey(
                        name: "FK_case_parties_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_case_parties_case_registers_case_register_id",
                        column: x => x.case_register_id,
                        principalTable: "case_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_case_parties_drivers_driver_id",
                        column: x => x.driver_id,
                        principalSchema: "weighing",
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_case_parties_transporters_transporter_id",
                        column: x => x.transporter_id,
                        principalSchema: "weighing",
                        principalTable: "transporters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_case_parties_vehicle_owners_vehicle_owner_id",
                        column: x => x.vehicle_owner_id,
                        principalSchema: "weighing",
                        principalTable: "vehicle_owners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    document_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    related_entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_documents_asp_net_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "scale_tests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    station_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bound = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    test_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    vehicle_plate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    weighing_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    test_weight_kg = table.Column<int>(type: "integer", nullable: true),
                    actual_weight_kg = table.Column<int>(type: "integer", nullable: true),
                    result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pass"),
                    deviation_kg = table.Column<int>(type: "integer", nullable: true),
                    details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    carried_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    carried_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scale_tests", x => x.id);
                    table.ForeignKey(
                        name: "FK_scale_tests_asp_net_users_carried_by_id",
                        column: x => x.carried_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_scale_tests_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_shifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shift_rotation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_shifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_shifts_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_shifts_shift_rotations_shift_rotation_id",
                        column: x => x.shift_rotation_id,
                        principalTable: "shift_rotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_shifts_work_shifts_work_shift_id",
                        column: x => x.work_shift_id,
                        principalTable: "work_shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    reg_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tag_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "automatic"),
                    tag_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    ReasonEmbedding = table.Column<Vector>(type: "vector(384)", nullable: true),
                    station_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "open"),
                    tag_photo_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    effective_time_period = table.Column<TimeSpan>(type: "interval", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    closed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    exported = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CaseRegisterId = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_tags", x => x.id);
                    table.CheckConstraint("chk_vehicle_tag_status", "status IN ('open', 'closed')");
                    table.CheckConstraint("chk_vehicle_tag_type", "tag_type IN ('automatic', 'manual')");
                    table.ForeignKey(
                        name: "FK_vehicle_tags_asp_net_users_closed_by_id",
                        column: x => x.closed_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vehicle_tags_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vehicle_tags_case_registers_CaseRegisterId",
                        column: x => x.CaseRegisterId,
                        principalTable: "case_registers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_vehicle_tags_tag_categories_tag_category_id",
                        column: x => x.tag_category_id,
                        principalTable: "tag_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "axle_weight_references",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_position = table.Column<int>(type: "integer", nullable: false),
                    axle_legal_weight_kg = table.Column<int>(type: "integer", nullable: false),
                    axle_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_grouping = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tyre_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_axle_weight_references", x => x.Id);
                    table.ForeignKey(
                        name: "FK_axle_weight_references_axle_configurations_axle_configurati~",
                        column: x => x.axle_configuration_id,
                        principalTable: "axle_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_axle_weight_references_axle_groups_axle_group_id",
                        column: x => x.axle_group_id,
                        principalTable: "axle_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_axle_weight_references_tyre_types_tyre_type_id",
                        column: x => x.tyre_type_id,
                        principalTable: "tyre_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_models",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    make_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Truck"),
                    axle_configuration_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_models", x => x.id);
                    table.CheckConstraint("chk_vehicle_category", "vehicle_category IN ('Truck', 'Trailer', 'Bus', 'Van', 'Other')");
                    table.ForeignKey(
                        name: "FK_vehicle_models_axle_configurations_axle_configuration_id",
                        column: x => x.axle_configuration_id,
                        principalTable: "axle_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vehicle_models_vehicle_makes_make_id",
                        column: x => x.make_id,
                        principalTable: "vehicle_makes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                schema: "weighing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    reg_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    vehicle_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    year_of_manufacture = table.Column<int>(type: "integer", nullable: true),
                    chassis_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    engine_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transporter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    axle_configuration_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DescriptionEmbedding = table.Column<Vector>(type: "vector(384)", nullable: true),
                    is_flagged = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.id);
                    table.ForeignKey(
                        name: "FK_vehicles_axle_configurations_axle_configuration_id",
                        column: x => x.axle_configuration_id,
                        principalTable: "axle_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vehicles_transporters_transporter_id",
                        column: x => x.transporter_id,
                        principalSchema: "weighing",
                        principalTable: "transporters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vehicles_vehicle_owners_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "weighing",
                        principalTable: "vehicle_owners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "permits",
                schema: "weighing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    permit_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permit_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_extension_kg = table.Column<int>(type: "integer", nullable: true),
                    gvw_extension_kg = table.Column<int>(type: "integer", nullable: true),
                    valid_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    issuing_authority = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permits", x => x.id);
                    table.CheckConstraint("chk_permit_dates", "\"valid_to\" > \"valid_from\"");
                    table.CheckConstraint("chk_permit_status", "\"status\" IN ('active', 'expired', 'revoked')");
                    table.ForeignKey(
                        name: "FK_permits_permit_types_permit_type_id",
                        column: x => x.permit_type_id,
                        principalTable: "permit_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permits_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalSchema: "weighing",
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "weighing_transactions",
                schema: "weighing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ticket_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_reg_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transporter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    station_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weighed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    WeighingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActId = table.Column<Guid>(type: "uuid", nullable: true),
                    Bound = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    gvw_measured_kg = table.Column<int>(type: "integer", nullable: false),
                    gvw_permissible_kg = table.Column<int>(type: "integer", nullable: false),
                    overload_kg = table.Column<int>(type: "integer", nullable: false),
                    control_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    total_fee_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    weighed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    is_sync = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsCompliant = table.Column<bool>(type: "boolean", nullable: false),
                    IsSentToYard = table.Column<bool>(type: "boolean", nullable: false),
                    violation_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ViolationReasonEmbedding = table.Column<Vector>(type: "vector(384)", nullable: true),
                    reweigh_cycle_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    original_weighing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    has_permit = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    OriginId = table.Column<Guid>(type: "uuid", nullable: true),
                    DestinationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CargoId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScaleTestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToleranceApplied = table.Column<bool>(type: "boolean", nullable: false),
                    ReweighLimit = table.Column<int>(type: "integer", nullable: false),
                    ClientLocalId = table.Column<Guid>(type: "uuid", nullable: true),
                    SyncStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CaptureSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CaptureStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AutoweighGvwKg = table.Column<int>(type: "integer", nullable: true),
                    AutoweighAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weighing_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_weighing_transactions_act_definitions_ActId",
                        column: x => x.ActId,
                        principalTable: "act_definitions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_weighing_transactions_cargo_types_CargoId",
                        column: x => x.CargoId,
                        principalTable: "cargo_types",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_weighing_transactions_drivers_driver_id",
                        column: x => x.driver_id,
                        principalSchema: "weighing",
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weighing_transactions_origins_destinations_DestinationId",
                        column: x => x.DestinationId,
                        principalTable: "origins_destinations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_weighing_transactions_origins_destinations_OriginId",
                        column: x => x.OriginId,
                        principalTable: "origins_destinations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_weighing_transactions_scale_tests_ScaleTestId",
                        column: x => x.ScaleTestId,
                        principalTable: "scale_tests",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_weighing_transactions_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weighing_transactions_transporters_transporter_id",
                        column: x => x.transporter_id,
                        principalSchema: "weighing",
                        principalTable: "transporters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weighing_transactions_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalSchema: "weighing",
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weighing_transactions_weighing_transactions_original_weighi~",
                        column: x => x.original_weighing_id,
                        principalSchema: "weighing",
                        principalTable: "weighing_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "load_correction_memos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    memo_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    case_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weighing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    overload_kg = table.Column<int>(type: "integer", nullable: false),
                    redistribution_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reweigh_scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reweigh_weighing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    compliance_achieved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReliefTruckRegNumber = table.Column<string>(type: "text", nullable: true),
                    ReliefTruckEmptyWeightKg = table.Column<int>(type: "integer", nullable: true),
                    issued_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_load_correction_memos", x => x.id);
                    table.CheckConstraint("chk_load_correction_redistribution_type", "redistribution_type IN ('offload', 'redistribute')");
                    table.ForeignKey(
                        name: "FK_load_correction_memos_asp_net_users_issued_by_id",
                        column: x => x.issued_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_load_correction_memos_case_registers_case_register_id",
                        column: x => x.case_register_id,
                        principalTable: "case_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_load_correction_memos_weighing_transactions_reweigh_weighin~",
                        column: x => x.reweigh_weighing_id,
                        principalSchema: "weighing",
                        principalTable: "weighing_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_load_correction_memos_weighing_transactions_weighing_id",
                        column: x => x.weighing_id,
                        principalSchema: "weighing",
                        principalTable: "weighing_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prohibition_orders",
                schema: "weighing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    weighing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prohibition_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    issued_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Open"),
                    reason = table.Column<string>(type: "text", nullable: false),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prohibition_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_prohibition_orders_asp_net_users_issued_by_id",
                        column: x => x.issued_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prohibition_orders_weighing_transactions_weighing_id",
                        column: x => x.weighing_id,
                        principalSchema: "weighing",
                        principalTable: "weighing_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prosecution_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    case_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weighing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prosecution_officer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    act_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gvw_overload_kg = table.Column<int>(type: "integer", nullable: false),
                    gvw_fee_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    gvw_fee_kes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    max_axle_overload_kg = table.Column<int>(type: "integer", nullable: false),
                    max_axle_fee_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    max_axle_fee_kes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    best_charge_basis = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "gvw"),
                    penalty_multiplier = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 1.0m),
                    total_fee_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_fee_kes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    forex_rate = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    certificate_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    case_notes = table.Column<string>(type: "text", nullable: true),
                    CaseNotesEmbedding = table.Column<Vector>(type: "vector(384)", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prosecution_cases", x => x.id);
                    table.CheckConstraint("chk_prosecution_case_basis", "best_charge_basis IN ('gvw', 'axle')");
                    table.CheckConstraint("chk_prosecution_case_status", "status IN ('pending', 'invoiced', 'paid', 'court')");
                    table.CheckConstraint("chk_prosecution_penalty_multiplier", "penalty_multiplier >= 1.0 AND penalty_multiplier <= 10.0");
                    table.ForeignKey(
                        name: "FK_prosecution_cases_act_definitions_act_id",
                        column: x => x.act_id,
                        principalTable: "act_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prosecution_cases_asp_net_users_prosecution_officer_id",
                        column: x => x.prosecution_officer_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prosecution_cases_case_registers_case_register_id",
                        column: x => x.case_register_id,
                        principalTable: "case_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prosecution_cases_weighing_transactions_weighing_id",
                        column: x => x.weighing_id,
                        principalSchema: "weighing",
                        principalTable: "weighing_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "weighing_axles",
                schema: "weighing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    weighing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_number = table.Column<int>(type: "integer", nullable: false),
                    measured_weight_kg = table.Column<int>(type: "integer", nullable: false),
                    permissible_weight_kg = table.Column<int>(type: "integer", nullable: false),
                    axle_configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_weight_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    axle_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_grouping = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    axle_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    axle_spacing_meters = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    pavement_damage_factor = table.Column<decimal>(type: "numeric(10,4)", nullable: false, defaultValue: 0.0000m),
                    group_aggregate_weight_kg = table.Column<int>(type: "integer", nullable: true),
                    group_permissible_weight_kg = table.Column<int>(type: "integer", nullable: true),
                    tyre_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fee_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    captured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weighing_axles", x => x.id);
                    table.ForeignKey(
                        name: "FK_weighing_axles_axle_configurations_axle_configuration_id",
                        column: x => x.axle_configuration_id,
                        principalTable: "axle_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weighing_axles_axle_groups_axle_group_id",
                        column: x => x.axle_group_id,
                        principalTable: "axle_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weighing_axles_axle_weight_references_axle_weight_reference~",
                        column: x => x.axle_weight_reference_id,
                        principalTable: "axle_weight_references",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weighing_axles_tyre_types_tyre_type_id",
                        column: x => x.tyre_type_id,
                        principalTable: "tyre_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weighing_axles_weighing_transactions_weighing_id",
                        column: x => x.weighing_id,
                        principalSchema: "weighing",
                        principalTable: "weighing_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "yard_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    weighing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    entered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    released_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_yard_entries", x => x.id);
                    table.CheckConstraint("chk_yard_entry_reason", "reason IN ('redistribution', 'gvw_overload', 'permit_check', 'offload')");
                    table.CheckConstraint("chk_yard_entry_status", "status IN ('pending', 'processing', 'released', 'escalated')");
                    table.ForeignKey(
                        name: "FK_yard_entries_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_yard_entries_weighing_transactions_weighing_id",
                        column: x => x.weighing_id,
                        principalSchema: "weighing",
                        principalTable: "weighing_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "compliance_certificates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    certificate_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    case_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weighing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    load_correction_memo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issued_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_certificates", x => x.id);
                    table.ForeignKey(
                        name: "FK_compliance_certificates_asp_net_users_issued_by_id",
                        column: x => x.issued_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_compliance_certificates_case_registers_case_register_id",
                        column: x => x.case_register_id,
                        principalTable: "case_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_compliance_certificates_load_correction_memos_load_correcti~",
                        column: x => x.load_correction_memo_id,
                        principalTable: "load_correction_memos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_compliance_certificates_weighing_transactions_weighing_id",
                        column: x => x.weighing_id,
                        principalSchema: "weighing",
                        principalTable: "weighing_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    invoice_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    case_register_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prosecution_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    weighing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount_due = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pesaflow_invoice_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pesaflow_payment_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pesaflow_checkout_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                    table.CheckConstraint("chk_invoice_amount", "amount_due >= 0");
                    table.CheckConstraint("chk_invoice_currency", "currency IN ('USD', 'KES', 'UGX', 'TZS')");
                    table.CheckConstraint("chk_invoice_status", "status IN ('pending', 'paid', 'cancelled', 'void')");
                    table.ForeignKey(
                        name: "FK_invoices_case_registers_case_register_id",
                        column: x => x.case_register_id,
                        principalTable: "case_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_invoices_prosecution_cases_prosecution_case_id",
                        column: x => x.prosecution_case_id,
                        principalTable: "prosecution_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_invoices_weighing_transactions_weighing_id",
                        column: x => x.weighing_id,
                        principalSchema: "weighing",
                        principalTable: "weighing_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    receipt_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_paid = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "cash"),
                    transaction_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    received_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    payment_channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipts", x => x.id);
                    table.CheckConstraint("chk_receipt_amount", "amount_paid > 0");
                    table.CheckConstraint("chk_receipt_currency", "currency IN ('USD', 'KES', 'UGX', 'TZS')");
                    table.CheckConstraint("chk_receipt_payment_method", "payment_method IN ('cash', 'mobile_money', 'bank_transfer', 'card', 'pesaflow')");
                    table.ForeignKey(
                        name: "FK_receipts_asp_net_users_received_by_id",
                        column: x => x.received_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_receipts_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_act_definitions_active",
                table: "act_definitions",
                column: "is_active",
                filter: "is_active = TRUE");

            migrationBuilder.CreateIndex(
                name: "idx_act_definitions_code",
                table: "act_definitions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_act_definitions_type",
                table: "act_definitions",
                column: "act_type");

            migrationBuilder.CreateIndex(
                name: "idx_arrest_warrants_accused",
                table: "arrest_warrants",
                column: "accused_id_no",
                filter: "accused_id_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_arrest_warrants_case",
                table: "arrest_warrants",
                column: "case_register_id");

            migrationBuilder.CreateIndex(
                name: "idx_arrest_warrants_status",
                table: "arrest_warrants",
                columns: new[] { "warrant_status_id", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "idx_arrest_warrants_warrant_no",
                table: "arrest_warrants",
                column: "warrant_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_roles_code",
                table: "asp_net_roles",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "asp_net_roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_asp_net_user_claims_user_id",
                table: "asp_net_user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_asp_net_user_logins_user_id",
                table: "asp_net_user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_asp_net_user_roles_role_id",
                table: "asp_net_user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "asp_net_users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "idx_users_department_id",
                table: "asp_net_users",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "idx_users_organization_id",
                table: "asp_net_users",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_users_station_id",
                table: "asp_net_users",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "asp_net_users",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_created_at",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_resource_id",
                table: "audit_logs",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_resource_type",
                table: "audit_logs",
                column: "resource_type");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_axle_configurations_active",
                table: "axle_configurations",
                columns: new[] { "is_active", "deleted_at" },
                filter: "is_active = true AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_axle_configurations_axle_number",
                table: "axle_configurations",
                column: "axle_number");

            migrationBuilder.CreateIndex(
                name: "idx_axle_configurations_code_unique",
                table: "axle_configurations",
                column: "axle_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_axle_configurations_framework",
                table: "axle_configurations",
                column: "legal_framework");

            migrationBuilder.CreateIndex(
                name: "idx_axle_configurations_standard",
                table: "axle_configurations",
                column: "is_standard",
                filter: "is_standard = true");

            migrationBuilder.CreateIndex(
                name: "IX_axle_configurations_created_by_user_id",
                table: "axle_configurations",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_axle_fee_schedule_effective",
                table: "axle_fee_schedules",
                columns: new[] { "effective_from", "effective_to" });

            migrationBuilder.CreateIndex(
                name: "idx_axle_fee_schedule_framework_type",
                table: "axle_fee_schedules",
                columns: new[] { "legal_framework", "fee_type" });

            migrationBuilder.CreateIndex(
                name: "IX_axle_fee_schedules_effective_from",
                table: "axle_fee_schedules",
                column: "effective_from");

            migrationBuilder.CreateIndex(
                name: "IX_axle_fee_schedules_fee_type",
                table: "axle_fee_schedules",
                column: "fee_type");

            migrationBuilder.CreateIndex(
                name: "IX_axle_fee_schedules_is_active",
                table: "axle_fee_schedules",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_axle_fee_schedules_legal_framework",
                table: "axle_fee_schedules",
                column: "legal_framework");

            migrationBuilder.CreateIndex(
                name: "IX_axle_fee_schedules_lookup",
                table: "axle_fee_schedules",
                columns: new[] { "legal_framework", "fee_type", "overload_min_kg", "overload_max_kg" });

            migrationBuilder.CreateIndex(
                name: "idx_axle_groups_active",
                table: "axle_groups",
                column: "is_active",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_axle_groups_code_unique",
                table: "axle_groups",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_axle_fee_effective_dates",
                table: "axle_type_overload_fee_schedules",
                columns: new[] { "effective_from", "effective_to" });

            migrationBuilder.CreateIndex(
                name: "idx_axle_fee_overload_range",
                table: "axle_type_overload_fee_schedules",
                columns: new[] { "overload_min_kg", "overload_max_kg", "is_active" });

            migrationBuilder.CreateIndex(
                name: "idx_axle_weight_ref_config_id",
                table: "axle_weight_references",
                column: "axle_configuration_id");

            migrationBuilder.CreateIndex(
                name: "idx_axle_weight_ref_config_position_unique",
                table: "axle_weight_references",
                columns: new[] { "axle_configuration_id", "axle_position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_axle_weight_ref_group_id",
                table: "axle_weight_references",
                column: "axle_group_id");

            migrationBuilder.CreateIndex(
                name: "idx_axle_weight_ref_tyre_type_id",
                table: "axle_weight_references",
                column: "tyre_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_types_code",
                table: "cargo_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cargo_types_is_active",
                table: "cargo_types",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_types_name",
                table: "cargo_types",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_case_assignment_logs_assigned_at",
                table: "case_assignment_logs",
                column: "assigned_at");

            migrationBuilder.CreateIndex(
                name: "idx_case_assignment_logs_case_id",
                table: "case_assignment_logs",
                column: "case_register_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_assignment_logs_case_timeline",
                table: "case_assignment_logs",
                columns: new[] { "case_register_id", "assigned_at" });

            migrationBuilder.CreateIndex(
                name: "idx_case_assignment_logs_current_io",
                table: "case_assignment_logs",
                columns: new[] { "case_register_id", "is_current" });

            migrationBuilder.CreateIndex(
                name: "idx_case_assignment_logs_new_officer_id",
                table: "case_assignment_logs",
                column: "new_officer_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_assignment_logs_assigned_by_id",
                table: "case_assignment_logs",
                column: "assigned_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_assignment_logs_previous_officer_id",
                table: "case_assignment_logs",
                column: "previous_officer_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_closure_checklists_case",
                table: "case_closure_checklists",
                column: "case_register_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_case_closure_checklists_closure_type_id",
                table: "case_closure_checklists",
                column: "closure_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_closure_checklists_legal_section_id",
                table: "case_closure_checklists",
                column: "legal_section_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_closure_checklists_review_status_id",
                table: "case_closure_checklists",
                column: "review_status_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_managers_role",
                table: "case_managers",
                columns: new[] { "role_type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "idx_case_managers_user",
                table: "case_managers",
                column: "user_id",
                filter: "is_active = TRUE");

            migrationBuilder.CreateIndex(
                name: "idx_case_parties_active",
                table: "case_parties",
                column: "is_currently_active");

            migrationBuilder.CreateIndex(
                name: "idx_case_parties_case_id",
                table: "case_parties",
                column: "case_register_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_parties_case_role_active",
                table: "case_parties",
                columns: new[] { "case_register_id", "party_role", "is_currently_active" });

            migrationBuilder.CreateIndex(
                name: "idx_case_parties_driver_id",
                table: "case_parties",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_parties_role",
                table: "case_parties",
                column: "party_role");

            migrationBuilder.CreateIndex(
                name: "idx_case_parties_user_id",
                table: "case_parties",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_parties_transporter_id",
                table: "case_parties",
                column: "transporter_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_parties_vehicle_owner_id",
                table: "case_parties",
                column: "vehicle_owner_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_registers_case_no",
                table: "case_registers",
                column: "case_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_case_registers_county",
                table: "case_registers",
                column: "county_id",
                filter: "county_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_case_registers_court",
                table: "case_registers",
                column: "court_id",
                filter: "court_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_case_registers_driver_ntac",
                table: "case_registers",
                column: "driver_ntac_no",
                filter: "driver_ntac_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_case_registers_escalated",
                table: "case_registers",
                column: "escalated_to_case_manager",
                filter: "escalated_to_case_manager = TRUE");

            migrationBuilder.CreateIndex(
                name: "idx_case_registers_road",
                table: "case_registers",
                column: "road_id",
                filter: "road_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_case_registers_status",
                table: "case_registers",
                columns: new[] { "case_status_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_case_registers_transporter_ntac",
                table: "case_registers",
                column: "transporter_ntac_no",
                filter: "transporter_ntac_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_case_registers_vehicle",
                table: "case_registers",
                columns: new[] { "vehicle_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_case_registers_violation_type",
                table: "case_registers",
                column: "violation_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_registers_weighing",
                table: "case_registers",
                column: "weighing_id",
                filter: "weighing_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_case_registers_act_id",
                table: "case_registers",
                column: "act_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_registers_case_manager_id",
                table: "case_registers",
                column: "case_manager_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_registers_disposition_type_id",
                table: "case_registers",
                column: "disposition_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_registers_ViolationDetailsEmbedding",
                table: "case_registers",
                column: "ViolationDetailsEmbedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_case_review_statuses_code",
                table: "case_review_statuses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_case_statuses_code",
                table: "case_statuses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_case_subfiles_case_type",
                table: "case_subfiles",
                columns: new[] { "case_register_id", "subfile_type_id" });

            migrationBuilder.CreateIndex(
                name: "idx_case_subfiles_uploaded",
                table: "case_subfiles",
                column: "uploaded_at");

            migrationBuilder.CreateIndex(
                name: "IX_case_subfiles_ContentEmbedding",
                table: "case_subfiles",
                column: "ContentEmbedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_case_subfiles_subfile_type_id",
                table: "case_subfiles",
                column: "subfile_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_closure_types_code",
                table: "closure_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_compliance_certificates_case_id",
                table: "compliance_certificates",
                column: "case_register_id");

            migrationBuilder.CreateIndex(
                name: "idx_compliance_certificates_cert_no",
                table: "compliance_certificates",
                column: "certificate_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_compliance_certificates_issued_at",
                table: "compliance_certificates",
                column: "issued_at");

            migrationBuilder.CreateIndex(
                name: "idx_compliance_certificates_weighing_id",
                table: "compliance_certificates",
                column: "weighing_id");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_certificates_issued_by_id",
                table: "compliance_certificates",
                column: "issued_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_certificates_load_correction_memo_id",
                table: "compliance_certificates",
                column: "load_correction_memo_id");

            migrationBuilder.CreateIndex(
                name: "idx_court_hearings_case_date",
                table: "court_hearings",
                columns: new[] { "case_register_id", "hearing_date" });

            migrationBuilder.CreateIndex(
                name: "idx_court_hearings_court",
                table: "court_hearings",
                columns: new[] { "court_id", "hearing_date" },
                filter: "court_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_court_hearings_status_date",
                table: "court_hearings",
                columns: new[] { "hearing_status_id", "hearing_date" });

            migrationBuilder.CreateIndex(
                name: "IX_court_hearings_hearing_outcome_id",
                table: "court_hearings",
                column: "hearing_outcome_id");

            migrationBuilder.CreateIndex(
                name: "IX_court_hearings_hearing_type_id",
                table: "court_hearings",
                column: "hearing_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_court_hearings_MinuteNotesEmbedding",
                table: "court_hearings",
                column: "MinuteNotesEmbedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_courts_code",
                table: "courts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_courts_name",
                table: "courts",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_courts_type",
                table: "courts",
                column: "court_type");

            migrationBuilder.CreateIndex(
                name: "idx_demerit_legal_framework",
                table: "demerit_point_schedules",
                column: "legal_framework");

            migrationBuilder.CreateIndex(
                name: "idx_demerit_violation_overload",
                table: "demerit_point_schedules",
                columns: new[] { "violation_type", "overload_min_kg", "overload_max_kg", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_departments_organization_id",
                table: "departments",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_device_sync_correlation",
                table: "device_sync_events",
                column: "correlation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_device_sync_device_status",
                table: "device_sync_events",
                columns: new[] { "device_id", "sync_status" });

            migrationBuilder.CreateIndex(
                name: "idx_device_sync_entity",
                table: "device_sync_events",
                columns: new[] { "entity_type", "entity_id" },
                filter: "entity_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_device_sync_status_created",
                table: "device_sync_events",
                columns: new[] { "sync_status", "created_at" },
                filter: "sync_status IN ('queued', 'failed')");

            migrationBuilder.CreateIndex(
                name: "idx_disposition_types_code",
                table: "disposition_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Districts_CountyId",
                table: "Districts",
                column: "CountyId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_document_type",
                table: "documents",
                column: "document_type");

            migrationBuilder.CreateIndex(
                name: "IX_documents_related_entity_type_related_entity_id",
                table: "documents",
                columns: new[] { "related_entity_type", "related_entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_uploaded_by_id",
                table: "documents",
                column: "uploaded_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_driver_demerit_records_driver_id",
                schema: "weighing",
                table: "driver_demerit_records",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "IX_driver_demerit_records_fee_schedule_id",
                schema: "weighing",
                table: "driver_demerit_records",
                column: "fee_schedule_id");

            migrationBuilder.CreateIndex(
                name: "IX_drivers_driving_license_no",
                schema: "weighing",
                table: "drivers",
                column: "driving_license_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_drivers_id_number",
                schema: "weighing",
                table: "drivers",
                column: "id_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_drivers_ntsa_id",
                schema: "weighing",
                table: "drivers",
                column: "ntsa_id");

            migrationBuilder.CreateIndex(
                name: "IX_hardware_health_logs_station_id",
                table: "hardware_health_logs",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "idx_hearing_outcomes_code",
                table: "hearing_outcomes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_hearing_statuses_code",
                table: "hearing_statuses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_hearing_types_code",
                table: "hearing_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_integration_configs_is_active",
                table: "integration_configs",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_integration_configs_provider_name",
                table: "integration_configs",
                column: "provider_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_invoices_case_register_id",
                table: "invoices",
                column: "case_register_id");

            migrationBuilder.CreateIndex(
                name: "idx_invoices_due_date",
                table: "invoices",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "idx_invoices_generated_at",
                table: "invoices",
                column: "generated_at");

            migrationBuilder.CreateIndex(
                name: "idx_invoices_invoice_no",
                table: "invoices",
                column: "invoice_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_invoices_pesaflow_invoice_no",
                table: "invoices",
                column: "pesaflow_invoice_number",
                filter: "pesaflow_invoice_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_invoices_prosecution_case_id",
                table: "invoices",
                column: "prosecution_case_id");

            migrationBuilder.CreateIndex(
                name: "idx_invoices_status",
                table: "invoices",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_invoices_weighing_id",
                table: "invoices",
                column: "weighing_id");

            migrationBuilder.CreateIndex(
                name: "IX_legal_sections_framework_section_no",
                table: "legal_sections",
                columns: new[] { "legal_framework", "section_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_legal_sections_is_active",
                table: "legal_sections",
                column: "is_active",
                filter: "is_active = TRUE");

            migrationBuilder.CreateIndex(
                name: "idx_load_correction_memos_case_id",
                table: "load_correction_memos",
                column: "case_register_id");

            migrationBuilder.CreateIndex(
                name: "idx_load_correction_memos_issued_at",
                table: "load_correction_memos",
                column: "issued_at");

            migrationBuilder.CreateIndex(
                name: "idx_load_correction_memos_memo_no",
                table: "load_correction_memos",
                column: "memo_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_load_correction_memos_weighing_id",
                table: "load_correction_memos",
                column: "weighing_id");

            migrationBuilder.CreateIndex(
                name: "IX_load_correction_memos_issued_by_id",
                table: "load_correction_memos",
                column: "issued_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_load_correction_memos_reweigh_weighing_id",
                table: "load_correction_memos",
                column: "reweigh_weighing_id");

            migrationBuilder.CreateIndex(
                name: "idx_organizations_code",
                table: "organizations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_origins_destinations_code",
                table: "origins_destinations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_origins_destinations_country",
                table: "origins_destinations",
                column: "country");

            migrationBuilder.CreateIndex(
                name: "IX_origins_destinations_is_active",
                table: "origins_destinations",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_origins_destinations_name",
                table: "origins_destinations",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_penalty_points_range",
                table: "penalty_schedules",
                columns: new[] { "points_min", "points_max", "is_active" });

            migrationBuilder.CreateIndex(
                name: "idx_permissions_active",
                table: "permissions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_permissions_category",
                table: "permissions",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "idx_permissions_code",
                table: "permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_permit_types_code",
                table: "permit_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permits_permit_no",
                schema: "weighing",
                table: "permits",
                column: "permit_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permits_permit_type_id",
                schema: "weighing",
                table: "permits",
                column: "permit_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_permits_status",
                schema: "weighing",
                table: "permits",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_permits_valid_from",
                schema: "weighing",
                table: "permits",
                column: "valid_from");

            migrationBuilder.CreateIndex(
                name: "IX_permits_valid_to",
                schema: "weighing",
                table: "permits",
                column: "valid_to");

            migrationBuilder.CreateIndex(
                name: "IX_permits_vehicle_id",
                schema: "weighing",
                table: "permits",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "IX_prohibition_orders_issued_by_id",
                schema: "weighing",
                table: "prohibition_orders",
                column: "issued_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_prohibition_orders_prohibition_no",
                schema: "weighing",
                table: "prohibition_orders",
                column: "prohibition_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prohibition_orders_status",
                schema: "weighing",
                table: "prohibition_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_prohibition_orders_weighing_id",
                schema: "weighing",
                table: "prohibition_orders",
                column: "weighing_id");

            migrationBuilder.CreateIndex(
                name: "idx_prosecution_cases_case_register_id",
                table: "prosecution_cases",
                column: "case_register_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_prosecution_cases_certificate_no",
                table: "prosecution_cases",
                column: "certificate_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_prosecution_cases_created_at",
                table: "prosecution_cases",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_prosecution_cases_officer_id",
                table: "prosecution_cases",
                column: "prosecution_officer_id");

            migrationBuilder.CreateIndex(
                name: "idx_prosecution_cases_status",
                table: "prosecution_cases",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_prosecution_cases_weighing_id",
                table: "prosecution_cases",
                column: "weighing_id");

            migrationBuilder.CreateIndex(
                name: "IX_prosecution_cases_act_id",
                table: "prosecution_cases",
                column: "act_id");

            migrationBuilder.CreateIndex(
                name: "IX_prosecution_cases_CaseNotesEmbedding",
                table: "prosecution_cases",
                column: "CaseNotesEmbedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_receipts_idempotency_key",
                table: "receipts",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_receipts_invoice_id",
                table: "receipts",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "idx_receipts_payment_date",
                table: "receipts",
                column: "payment_date");

            migrationBuilder.CreateIndex(
                name: "idx_receipts_receipt_no",
                table: "receipts",
                column: "receipt_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_receipts_transaction_ref",
                table: "receipts",
                column: "transaction_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipts_received_by_id",
                table: "receipts",
                column: "received_by_id");

            migrationBuilder.CreateIndex(
                name: "idx_release_types_code",
                table: "release_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roads_code",
                table: "roads",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roads_district_id",
                table: "roads",
                column: "district_id");

            migrationBuilder.CreateIndex(
                name: "IX_roads_is_active",
                table: "roads",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_roads_name",
                table: "roads",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_roads_road_class",
                table: "roads",
                column: "road_class");

            migrationBuilder.CreateIndex(
                name: "idx_role_permissions_permission",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_shifts_work_shift_id",
                table: "rotation_shifts",
                column: "work_shift_id");

            migrationBuilder.CreateIndex(
                name: "IX_scale_tests_carried_at",
                table: "scale_tests",
                column: "carried_at");

            migrationBuilder.CreateIndex(
                name: "IX_scale_tests_carried_by_id",
                table: "scale_tests",
                column: "carried_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_scale_tests_deleted_at",
                table: "scale_tests",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_scale_tests_result",
                table: "scale_tests",
                column: "result");

            migrationBuilder.CreateIndex(
                name: "IX_scale_tests_station_id",
                table: "scale_tests",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "IX_shift_rotations_current_active_shift_id",
                table: "shift_rotations",
                column: "current_active_shift_id");

            migrationBuilder.CreateIndex(
                name: "idx_special_releases_case",
                table: "special_releases",
                column: "case_register_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_special_releases_cert",
                table: "special_releases",
                column: "certificate_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_special_releases_issued",
                table: "special_releases",
                column: "issued_at");

            migrationBuilder.CreateIndex(
                name: "IX_special_releases_release_type_id",
                table: "special_releases",
                column: "release_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_stations_code",
                table: "stations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stations_county_id",
                table: "stations",
                column: "county_id");

            migrationBuilder.CreateIndex(
                name: "IX_stations_OrganizationId",
                table: "stations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_stations_road_id",
                table: "stations",
                column: "road_id");

            migrationBuilder.CreateIndex(
                name: "idx_subcounties_code",
                table: "subcounties",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_subcounties_district_id",
                table: "subcounties",
                column: "district_id");

            migrationBuilder.CreateIndex(
                name: "idx_subcounties_name",
                table: "subcounties",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_subfile_types_code",
                table: "subfile_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tag_categories_code",
                table: "tag_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tag_categories_name",
                table: "tag_categories",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_tolerance_settings_code",
                table: "tolerance_settings",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tolerance_settings_effective_dates",
                table: "tolerance_settings",
                columns: new[] { "effective_from", "effective_to" });

            migrationBuilder.CreateIndex(
                name: "IX_transporters_code",
                schema: "weighing",
                table: "transporters",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transporters_email",
                schema: "weighing",
                table: "transporters",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_transporters_is_active",
                schema: "weighing",
                table: "transporters",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_transporters_ntac_no",
                schema: "weighing",
                table: "transporters",
                column: "ntac_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transporters_phone",
                schema: "weighing",
                table: "transporters",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "IX_transporters_registration_no",
                schema: "weighing",
                table: "transporters",
                column: "registration_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tyre_types_active",
                table: "tyre_types",
                column: "is_active",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_tyre_types_code_unique",
                table: "tyre_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_shifts_shift_rotation_id",
                table: "user_shifts",
                column: "shift_rotation_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_shifts_user_id",
                table: "user_shifts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_shifts_work_shift_id",
                table: "user_shifts",
                column: "work_shift_id");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_makes_code",
                table: "vehicle_makes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_makes_country",
                table: "vehicle_makes",
                column: "country");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_makes_is_active",
                table: "vehicle_makes",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_makes_name",
                table: "vehicle_makes",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_models_category",
                table: "vehicle_models",
                column: "vehicle_category");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_models_code",
                table: "vehicle_models",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_models_is_active",
                table: "vehicle_models",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_models_make_id",
                table: "vehicle_models",
                column: "make_id");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_models_name",
                table: "vehicle_models",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_models_axle_configuration_id",
                table: "vehicle_models",
                column: "axle_configuration_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_owners_email",
                schema: "weighing",
                table: "vehicle_owners",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_owners_id_no_or_passport",
                schema: "weighing",
                table: "vehicle_owners",
                column: "id_no_or_passport",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_owners_ntac_no",
                schema: "weighing",
                table: "vehicle_owners",
                column: "ntac_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_owners_phone",
                schema: "weighing",
                table: "vehicle_owners",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_tags_category_id",
                table: "vehicle_tags",
                column: "tag_category_id");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_tags_opened_at",
                table: "vehicle_tags",
                column: "opened_at");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_tags_reg_no",
                table: "vehicle_tags",
                column: "reg_no");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_tags_reg_status",
                table: "vehicle_tags",
                columns: new[] { "reg_no", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_tags_station_code",
                table: "vehicle_tags",
                column: "station_code");

            migrationBuilder.CreateIndex(
                name: "idx_vehicle_tags_status",
                table: "vehicle_tags",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tags_CaseRegisterId",
                table: "vehicle_tags",
                column: "CaseRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tags_closed_by_id",
                table: "vehicle_tags",
                column: "closed_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tags_created_by_id",
                table: "vehicle_tags",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tags_ReasonEmbedding",
                table: "vehicle_tags",
                column: "ReasonEmbedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_axle_configuration_id",
                schema: "weighing",
                table: "vehicles",
                column: "axle_configuration_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_chassis_no",
                schema: "weighing",
                table: "vehicles",
                column: "chassis_no",
                unique: true,
                filter: "chassis_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_DescriptionEmbedding",
                schema: "weighing",
                table: "vehicles",
                column: "DescriptionEmbedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_engine_no",
                schema: "weighing",
                table: "vehicles",
                column: "engine_no");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_is_flagged",
                schema: "weighing",
                table: "vehicles",
                column: "is_flagged");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_owner_id",
                schema: "weighing",
                table: "vehicles",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_reg_no",
                schema: "weighing",
                table: "vehicles",
                column: "reg_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_transporter_id",
                schema: "weighing",
                table: "vehicles",
                column: "transporter_id");

            migrationBuilder.CreateIndex(
                name: "idx_violation_types_active",
                table: "violation_types",
                column: "is_active",
                filter: "is_active = TRUE");

            migrationBuilder.CreateIndex(
                name: "idx_violation_types_code",
                table: "violation_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_warrant_statuses_code",
                table: "warrant_statuses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weighbridge_hardware_station_id",
                table: "weighbridge_hardware",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "idx_weighing_axles_configuration",
                schema: "weighing",
                table: "weighing_axles",
                column: "axle_configuration_id");

            migrationBuilder.CreateIndex(
                name: "idx_weighing_axles_group",
                schema: "weighing",
                table: "weighing_axles",
                column: "axle_group_id");

            migrationBuilder.CreateIndex(
                name: "idx_weighing_axles_weighing",
                schema: "weighing",
                table: "weighing_axles",
                column: "weighing_id");

            migrationBuilder.CreateIndex(
                name: "idx_weighing_axles_weighing_axle_unique",
                schema: "weighing",
                table: "weighing_axles",
                columns: new[] { "weighing_id", "axle_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weighing_axles_axle_type",
                schema: "weighing",
                table: "weighing_axles",
                column: "axle_type");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_axles_axle_weight_reference_id",
                schema: "weighing",
                table: "weighing_axles",
                column: "axle_weight_reference_id");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_axles_tyre_type_id",
                schema: "weighing",
                table: "weighing_axles",
                column: "tyre_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_axles_weighing_grouping",
                schema: "weighing",
                table: "weighing_axles",
                columns: new[] { "weighing_id", "axle_grouping" });

            migrationBuilder.CreateIndex(
                name: "IX_weighing_axles_weighing_grouping_type",
                schema: "weighing",
                table: "weighing_axles",
                columns: new[] { "weighing_id", "axle_grouping", "axle_type" });

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_ActId",
                schema: "weighing",
                table: "weighing_transactions",
                column: "ActId");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_CargoId",
                schema: "weighing",
                table: "weighing_transactions",
                column: "CargoId");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_control_status",
                schema: "weighing",
                table: "weighing_transactions",
                column: "control_status");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_DestinationId",
                schema: "weighing",
                table: "weighing_transactions",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_driver_id",
                schema: "weighing",
                table: "weighing_transactions",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_original_weighing_id",
                schema: "weighing",
                table: "weighing_transactions",
                column: "original_weighing_id");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_OriginId",
                schema: "weighing",
                table: "weighing_transactions",
                column: "OriginId");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_ScaleTestId",
                schema: "weighing",
                table: "weighing_transactions",
                column: "ScaleTestId");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_station_date",
                schema: "weighing",
                table: "weighing_transactions",
                columns: new[] { "station_id", "weighed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_station_id",
                schema: "weighing",
                table: "weighing_transactions",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_station_status_date",
                schema: "weighing",
                table: "weighing_transactions",
                columns: new[] { "station_id", "control_status", "weighed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_ticket_number",
                schema: "weighing",
                table: "weighing_transactions",
                column: "ticket_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_transporter_id",
                schema: "weighing",
                table: "weighing_transactions",
                column: "transporter_id");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_vehicle_date",
                schema: "weighing",
                table: "weighing_transactions",
                columns: new[] { "vehicle_id", "weighed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_vehicle_reg_number",
                schema: "weighing",
                table: "weighing_transactions",
                column: "vehicle_reg_number");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_ViolationReasonEmbedding",
                schema: "weighing",
                table: "weighing_transactions",
                column: "ViolationReasonEmbedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_weighed_at",
                schema: "weighing",
                table: "weighing_transactions",
                column: "weighed_at");

            migrationBuilder.CreateIndex(
                name: "IX_work_shift_schedules_work_shift_id",
                table: "work_shift_schedules",
                column: "work_shift_id");

            migrationBuilder.CreateIndex(
                name: "idx_yard_entries_entered_at",
                table: "yard_entries",
                column: "entered_at");

            migrationBuilder.CreateIndex(
                name: "idx_yard_entries_station_id",
                table: "yard_entries",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "idx_yard_entries_station_status",
                table: "yard_entries",
                columns: new[] { "station_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_yard_entries_status",
                table: "yard_entries",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_yard_entries_weighing_id",
                table: "yard_entries",
                column: "weighing_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_settings");

            migrationBuilder.DropTable(
                name: "arrest_warrants");

            migrationBuilder.DropTable(
                name: "asp_net_user_claims");

            migrationBuilder.DropTable(
                name: "asp_net_user_logins");

            migrationBuilder.DropTable(
                name: "asp_net_user_roles");

            migrationBuilder.DropTable(
                name: "asp_net_user_tokens");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "axle_type_overload_fee_schedules");

            migrationBuilder.DropTable(
                name: "case_assignment_logs");

            migrationBuilder.DropTable(
                name: "case_closure_checklists");

            migrationBuilder.DropTable(
                name: "case_parties");

            migrationBuilder.DropTable(
                name: "case_subfiles");

            migrationBuilder.DropTable(
                name: "compliance_certificates");

            migrationBuilder.DropTable(
                name: "court_hearings");

            migrationBuilder.DropTable(
                name: "DatabaseSeedingHistory");

            migrationBuilder.DropTable(
                name: "demerit_point_schedules");

            migrationBuilder.DropTable(
                name: "device_sync_events");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "driver_demerit_records",
                schema: "weighing");

            migrationBuilder.DropTable(
                name: "hardware_health_logs");

            migrationBuilder.DropTable(
                name: "integration_configs");

            migrationBuilder.DropTable(
                name: "penalty_schedules");

            migrationBuilder.DropTable(
                name: "permits",
                schema: "weighing");

            migrationBuilder.DropTable(
                name: "prohibition_orders",
                schema: "weighing");

            migrationBuilder.DropTable(
                name: "receipts");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "rotation_shifts");

            migrationBuilder.DropTable(
                name: "special_releases");

            migrationBuilder.DropTable(
                name: "subcounties");

            migrationBuilder.DropTable(
                name: "tolerance_settings");

            migrationBuilder.DropTable(
                name: "user_shifts");

            migrationBuilder.DropTable(
                name: "vehicle_models");

            migrationBuilder.DropTable(
                name: "vehicle_tags");

            migrationBuilder.DropTable(
                name: "weighbridge_hardware");

            migrationBuilder.DropTable(
                name: "weighing_axles",
                schema: "weighing");

            migrationBuilder.DropTable(
                name: "work_shift_schedules");

            migrationBuilder.DropTable(
                name: "yard_entries");

            migrationBuilder.DropTable(
                name: "warrant_statuses");

            migrationBuilder.DropTable(
                name: "case_review_statuses");

            migrationBuilder.DropTable(
                name: "closure_types");

            migrationBuilder.DropTable(
                name: "legal_sections");

            migrationBuilder.DropTable(
                name: "subfile_types");

            migrationBuilder.DropTable(
                name: "load_correction_memos");

            migrationBuilder.DropTable(
                name: "hearing_outcomes");

            migrationBuilder.DropTable(
                name: "hearing_statuses");

            migrationBuilder.DropTable(
                name: "hearing_types");

            migrationBuilder.DropTable(
                name: "axle_fee_schedules");

            migrationBuilder.DropTable(
                name: "permit_types");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "asp_net_roles");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "release_types");

            migrationBuilder.DropTable(
                name: "shift_rotations");

            migrationBuilder.DropTable(
                name: "vehicle_makes");

            migrationBuilder.DropTable(
                name: "tag_categories");

            migrationBuilder.DropTable(
                name: "axle_weight_references");

            migrationBuilder.DropTable(
                name: "prosecution_cases");

            migrationBuilder.DropTable(
                name: "work_shifts");

            migrationBuilder.DropTable(
                name: "axle_groups");

            migrationBuilder.DropTable(
                name: "tyre_types");

            migrationBuilder.DropTable(
                name: "case_registers");

            migrationBuilder.DropTable(
                name: "weighing_transactions",
                schema: "weighing");

            migrationBuilder.DropTable(
                name: "case_managers");

            migrationBuilder.DropTable(
                name: "case_statuses");

            migrationBuilder.DropTable(
                name: "courts");

            migrationBuilder.DropTable(
                name: "disposition_types");

            migrationBuilder.DropTable(
                name: "violation_types");

            migrationBuilder.DropTable(
                name: "act_definitions");

            migrationBuilder.DropTable(
                name: "cargo_types");

            migrationBuilder.DropTable(
                name: "drivers",
                schema: "weighing");

            migrationBuilder.DropTable(
                name: "origins_destinations");

            migrationBuilder.DropTable(
                name: "scale_tests");

            migrationBuilder.DropTable(
                name: "vehicles",
                schema: "weighing");

            migrationBuilder.DropTable(
                name: "axle_configurations");

            migrationBuilder.DropTable(
                name: "transporters",
                schema: "weighing");

            migrationBuilder.DropTable(
                name: "vehicle_owners",
                schema: "weighing");

            migrationBuilder.DropTable(
                name: "asp_net_users");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropTable(
                name: "stations");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "roads");

            migrationBuilder.DropTable(
                name: "Districts");

            migrationBuilder.DropTable(
                name: "Counties");
        }
    }
}
