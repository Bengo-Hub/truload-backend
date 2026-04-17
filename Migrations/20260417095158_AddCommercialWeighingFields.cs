using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialWeighingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_weighing_transactions_transporter_id",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.AddColumn<int>(
                name: "adjusted_net_weight_kg",
                schema: "weighing",
                table: "weighing_transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "consignment_no",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "expected_net_weight_kg",
                schema: "weighing",
                table: "weighing_transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "first_weight_at",
                schema: "weighing",
                table: "weighing_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "first_weight_kg",
                schema: "weighing",
                table: "weighing_transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_weight_type",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "gross_weight_kg",
                schema: "weighing",
                table: "weighing_transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "industry_metadata",
                schema: "weighing",
                table: "weighing_transactions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "net_weight_kg",
                schema: "weighing",
                table: "weighing_transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "order_reference",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quality_deduction_kg",
                schema: "weighing",
                table: "weighing_transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remarks",
                schema: "weighing",
                table: "weighing_transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seal_numbers",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "second_weight_at",
                schema: "weighing",
                table: "weighing_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "second_weight_kg",
                schema: "weighing",
                table: "weighing_transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "second_weight_type",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tare_source",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tare_weight_kg",
                schema: "weighing",
                table: "weighing_transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "trailer_reg_no",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "weighing_mode",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "enforcement");

            migrationBuilder.AddColumn<int>(
                name: "weight_discrepancy_kg",
                schema: "weighing",
                table: "weighing_transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "default_tare_weight_kg",
                schema: "weighing",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_tare_weighed_at",
                schema: "weighing",
                table: "vehicles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "last_tare_weight_kg",
                schema: "weighing",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tare_expiry_days",
                schema: "weighing",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "portal_account_email",
                schema: "weighing",
                table: "transporters",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "portal_account_id",
                schema: "weighing",
                table: "transporters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "commercial_tolerance_settings",
                schema: "weighing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tolerance_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tolerance_value = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    max_tolerance_kg = table.Column<int>(type: "integer", nullable: true),
                    cargo_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_tolerance_settings", x => x.id);
                    table.CheckConstraint("chk_tolerance_type", "tolerance_type IN ('percentage', 'absolute')");
                    table.ForeignKey(
                        name: "FK_commercial_tolerance_settings_cargo_types_cargo_type_id",
                        column: x => x.cargo_type_id,
                        principalTable: "cargo_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_commercial_tolerance_settings_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_commercial_tolerance_settings_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "vehicle_tare_history",
                schema: "weighing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tare_weight_kg = table.Column<int>(type: "integer", nullable: false),
                    weighed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    station_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_tare_history", x => x.id);
                    table.CheckConstraint("chk_tare_source", "source IN ('measured', 'manual')");
                    table.ForeignKey(
                        name: "FK_vehicle_tare_history_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vehicle_tare_history_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_vehicle_tare_history_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalSchema: "weighing",
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_consignment_no",
                schema: "weighing",
                table: "weighing_transactions",
                column: "consignment_no",
                filter: "consignment_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_transporter_date",
                schema: "weighing",
                table: "weighing_transactions",
                columns: new[] { "transporter_id", "weighed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_weighing_mode",
                schema: "weighing",
                table: "weighing_transactions",
                column: "weighing_mode");

            migrationBuilder.CreateIndex(
                name: "IX_transporters_portal_account_id",
                schema: "weighing",
                table: "transporters",
                column: "portal_account_id",
                filter: "portal_account_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_transporters_portal_email",
                schema: "weighing",
                table: "transporters",
                column: "portal_account_email",
                filter: "portal_account_email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_commercial_tolerance_org_cargo",
                schema: "weighing",
                table: "commercial_tolerance_settings",
                columns: new[] { "organization_id", "cargo_type_id" });

            migrationBuilder.CreateIndex(
                name: "IX_commercial_tolerance_settings_cargo_type_id",
                schema: "weighing",
                table: "commercial_tolerance_settings",
                column: "cargo_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_commercial_tolerance_settings_organization_id",
                schema: "weighing",
                table: "commercial_tolerance_settings",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_commercial_tolerance_settings_station_id",
                schema: "weighing",
                table: "commercial_tolerance_settings",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tare_history_organization_id",
                schema: "weighing",
                table: "vehicle_tare_history",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tare_history_station_id",
                schema: "weighing",
                table: "vehicle_tare_history",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tare_history_vehicle_date",
                schema: "weighing",
                table: "vehicle_tare_history",
                columns: new[] { "vehicle_id", "weighed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tare_history_vehicle_id",
                schema: "weighing",
                table: "vehicle_tare_history",
                column: "vehicle_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commercial_tolerance_settings",
                schema: "weighing");

            migrationBuilder.DropTable(
                name: "vehicle_tare_history",
                schema: "weighing");

            migrationBuilder.DropIndex(
                name: "IX_weighing_transactions_consignment_no",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropIndex(
                name: "IX_weighing_transactions_transporter_date",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropIndex(
                name: "IX_weighing_transactions_weighing_mode",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropIndex(
                name: "IX_transporters_portal_account_id",
                schema: "weighing",
                table: "transporters");

            migrationBuilder.DropIndex(
                name: "IX_transporters_portal_email",
                schema: "weighing",
                table: "transporters");

            migrationBuilder.DropColumn(
                name: "adjusted_net_weight_kg",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "consignment_no",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "expected_net_weight_kg",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "first_weight_at",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "first_weight_kg",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "first_weight_type",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "gross_weight_kg",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "industry_metadata",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "net_weight_kg",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "order_reference",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "quality_deduction_kg",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "remarks",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "seal_numbers",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "second_weight_at",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "second_weight_kg",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "second_weight_type",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "tare_source",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "tare_weight_kg",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "trailer_reg_no",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "weighing_mode",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "weight_discrepancy_kg",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "default_tare_weight_kg",
                schema: "weighing",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "last_tare_weighed_at",
                schema: "weighing",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "last_tare_weight_kg",
                schema: "weighing",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "tare_expiry_days",
                schema: "weighing",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "portal_account_email",
                schema: "weighing",
                table: "transporters");

            migrationBuilder.DropColumn(
                name: "portal_account_id",
                schema: "weighing",
                table: "transporters");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_transporter_id",
                schema: "weighing",
                table: "weighing_transactions",
                column: "transporter_id");
        }
    }
}
