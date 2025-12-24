using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace truload_backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentityMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_framework = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fee_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    overload_min_kg = table.Column<int>(type: "integer", nullable: false),
                    overload_max_kg = table.Column<int>(type: "integer", nullable: true),
                    fee_per_kg_usd = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    flat_fee_usd = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    demerit_points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    penalty_description = table.Column<string>(type: "text", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_axle_fee_schedules", x => x.Id);
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
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_axle_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ntsa_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    id_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    driving_license_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    full_names = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    surname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    nationality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, defaultValue: "Kenya"),
                    date_of_birth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    license_class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    license_issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    license_expiry_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    license_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    is_professional_driver = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    current_demerit_points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    suspension_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    suspension_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drivers", x => x.Id);
                    table.CheckConstraint("chk_driver_license_status", "license_status IN ('active', 'suspended', 'revoked', 'expired')");
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_permit_types", x => x.Id);
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
                name: "tyre_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    typical_max_weight_kg = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tyre_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "work_shifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    shift_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    shift_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                name: "asp_net_role_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asp_net_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_asp_net_role_claims_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "asp_net_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "driver_demerit_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_demerit_records", x => x.Id);
                    table.CheckConstraint("chk_demerit_legal_framework", "legal_framework IN ('EAC', 'TRAFFIC_ACT')");
                    table.CheckConstraint("chk_demerit_payment_status", "payment_status IN ('pending', 'paid', 'waived')");
                    table.CheckConstraint("chk_demerit_points_range", "points_assigned >= 0 AND points_assigned <= 20");
                    table.CheckConstraint("chk_demerit_violation_type", "violation_type IN ('GVW_OVERLOAD', 'AXLE_OVERLOAD', 'PERMIT_VIOLATION', 'OTHER')");
                    table.ForeignKey(
                        name: "FK_driver_demerit_records_axle_fee_schedules_fee_schedule_id",
                        column: x => x.fee_schedule_id,
                        principalTable: "axle_fee_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_driver_demerit_records_drivers_driver_id",
                        column: x => x.driver_id,
                        principalTable: "drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "stations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    station_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    station_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(10,8)", nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(11,8)", nullable: true),
                    supports_bidirectional = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stations_organizations_OrganizationId",
                        column: x => x.OrganizationId,
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
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
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
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "weighing_axles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    weighing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_number = table.Column<int>(type: "integer", nullable: false),
                    measured_weight_kg = table.Column<int>(type: "integer", nullable: false),
                    permissible_weight_kg = table.Column<int>(type: "integer", nullable: false),
                    axle_configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_weight_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    axle_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_grouping = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tyre_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fee_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    captured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weighing_axles", x => x.Id);
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
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_weighing_axles_tyre_types_tyre_type_id",
                        column: x => x.tyre_type_id,
                        principalTable: "tyre_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_asp_net_role_claims_role_id",
                table: "asp_net_role_claims",
                column: "role_id");

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
                name: "IX_departments_organization_id",
                table: "departments",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_demerit_records_driver_date",
                table: "driver_demerit_records",
                columns: new[] { "driver_id", "violation_date" });

            migrationBuilder.CreateIndex(
                name: "idx_demerit_records_expiry_date",
                table: "driver_demerit_records",
                column: "points_expiry_date",
                filter: "is_expired = false");

            migrationBuilder.CreateIndex(
                name: "idx_demerit_records_payment_driver",
                table: "driver_demerit_records",
                columns: new[] { "payment_status", "driver_id" });

            migrationBuilder.CreateIndex(
                name: "IX_driver_demerit_records_fee_schedule_id",
                table: "driver_demerit_records",
                column: "fee_schedule_id");

            migrationBuilder.CreateIndex(
                name: "idx_drivers_id_number_unique",
                table: "drivers",
                column: "id_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_drivers_license_no_unique",
                table: "drivers",
                column: "driving_license_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_drivers_license_status_active",
                table: "drivers",
                columns: new[] { "license_status", "is_active" });

            migrationBuilder.CreateIndex(
                name: "idx_drivers_ntsa_id_unique",
                table: "drivers",
                column: "ntsa_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_drivers_suspension_dates",
                table: "drivers",
                columns: new[] { "suspension_start_date", "suspension_end_date" },
                filter: "suspension_start_date IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_organizations_code",
                table: "organizations",
                column: "code",
                unique: true);

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
                name: "idx_role_permissions_permission",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_shifts_work_shift_id",
                table: "rotation_shifts",
                column: "work_shift_id");

            migrationBuilder.CreateIndex(
                name: "IX_shift_rotations_current_active_shift_id",
                table: "shift_rotations",
                column: "current_active_shift_id");

            migrationBuilder.CreateIndex(
                name: "idx_stations_code",
                table: "stations",
                column: "station_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_stations_code_alias",
                table: "stations",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "IX_stations_OrganizationId",
                table: "stations",
                column: "OrganizationId");

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
                name: "idx_weighing_axles_configuration",
                table: "weighing_axles",
                column: "axle_configuration_id");

            migrationBuilder.CreateIndex(
                name: "idx_weighing_axles_group",
                table: "weighing_axles",
                column: "axle_group_id");

            migrationBuilder.CreateIndex(
                name: "idx_weighing_axles_weighing",
                table: "weighing_axles",
                column: "weighing_id");

            migrationBuilder.CreateIndex(
                name: "idx_weighing_axles_weighing_axle_unique",
                table: "weighing_axles",
                columns: new[] { "weighing_id", "axle_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weighing_axles_axle_weight_reference_id",
                table: "weighing_axles",
                column: "axle_weight_reference_id");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_axles_tyre_type_id",
                table: "weighing_axles",
                column: "tyre_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_shift_schedules_work_shift_id",
                table: "work_shift_schedules",
                column: "work_shift_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asp_net_role_claims");

            migrationBuilder.DropTable(
                name: "asp_net_user_claims");

            migrationBuilder.DropTable(
                name: "asp_net_user_logins");

            migrationBuilder.DropTable(
                name: "asp_net_user_roles");

            migrationBuilder.DropTable(
                name: "asp_net_user_tokens");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "driver_demerit_records");

            migrationBuilder.DropTable(
                name: "permit_types");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "rotation_shifts");

            migrationBuilder.DropTable(
                name: "tolerance_settings");

            migrationBuilder.DropTable(
                name: "user_shifts");

            migrationBuilder.DropTable(
                name: "weighing_axles");

            migrationBuilder.DropTable(
                name: "work_shift_schedules");

            migrationBuilder.DropTable(
                name: "axle_fee_schedules");

            migrationBuilder.DropTable(
                name: "drivers");

            migrationBuilder.DropTable(
                name: "asp_net_roles");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "shift_rotations");

            migrationBuilder.DropTable(
                name: "axle_weight_references");

            migrationBuilder.DropTable(
                name: "work_shifts");

            migrationBuilder.DropTable(
                name: "axle_configurations");

            migrationBuilder.DropTable(
                name: "axle_groups");

            migrationBuilder.DropTable(
                name: "tyre_types");

            migrationBuilder.DropTable(
                name: "asp_net_users");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropTable(
                name: "stations");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}
