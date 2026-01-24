using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAxleTypeFeeAndDemeritSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "idx_axle_fee_effective_dates",
                table: "axle_type_overload_fee_schedules",
                columns: new[] { "effective_from", "effective_to" });

            migrationBuilder.CreateIndex(
                name: "idx_axle_fee_overload_range",
                table: "axle_type_overload_fee_schedules",
                columns: new[] { "overload_min_kg", "overload_max_kg", "is_active" });

            migrationBuilder.CreateIndex(
                name: "idx_demerit_legal_framework",
                table: "demerit_point_schedules",
                column: "legal_framework");

            migrationBuilder.CreateIndex(
                name: "idx_demerit_violation_overload",
                table: "demerit_point_schedules",
                columns: new[] { "violation_type", "overload_min_kg", "overload_max_kg", "is_active" });

            migrationBuilder.CreateIndex(
                name: "idx_penalty_points_range",
                table: "penalty_schedules",
                columns: new[] { "points_min", "points_max", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "axle_type_overload_fee_schedules");

            migrationBuilder.DropTable(
                name: "demerit_point_schedules");

            migrationBuilder.DropTable(
                name: "penalty_schedules");
        }
    }
}
