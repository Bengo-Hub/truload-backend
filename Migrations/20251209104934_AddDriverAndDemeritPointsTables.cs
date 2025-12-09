using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace truload_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverAndDemeritPointsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_roles_code",
                table: "roles");

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

            migrationBuilder.CreateIndex(
                name: "idx_roles_code",
                table: "roles",
                column: "code");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "driver_demerit_records");

            migrationBuilder.DropTable(
                name: "drivers");

            migrationBuilder.DropIndex(
                name: "idx_roles_code",
                table: "roles");

            migrationBuilder.CreateIndex(
                name: "idx_roles_code",
                table: "roles",
                column: "code",
                unique: true);
        }
    }
}
