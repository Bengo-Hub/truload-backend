using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTareAnomalyDetectionAndTenantScopedTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "tare_anomaly_flagged_at",
                schema: "weighing",
                table: "weighing_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tare_anomaly_reason",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tare_anomaly_resolution",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "tare_anomaly_resolved_at",
                schema: "weighing",
                table: "weighing_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tare_anomaly_resolved_by_user_id",
                schema: "weighing",
                table: "weighing_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rated_capacity_kg",
                schema: "weighing",
                table: "vehicles",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "tare_anomaly_flagged_at",
                schema: "weighing",
                table: "vehicle_tare_history",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tare_anomaly_reason",
                schema: "weighing",
                table: "vehicle_tare_history",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tare_anomaly_resolution",
                schema: "weighing",
                table: "vehicle_tare_history",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "tare_anomaly_resolved_at",
                schema: "weighing",
                table: "vehicle_tare_history",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tare_anomaly_resolved_by_user_id",
                schema: "weighing",
                table: "vehicle_tare_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "origins_destinations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "cargo_types",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_tare_anomaly_pending",
                schema: "weighing",
                table: "weighing_transactions",
                column: "tare_anomaly_flagged_at",
                filter: "tare_anomaly_flagged_at IS NOT NULL AND tare_anomaly_resolved_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tare_history_tare_anomaly_pending",
                schema: "weighing",
                table: "vehicle_tare_history",
                column: "tare_anomaly_flagged_at",
                filter: "tare_anomaly_flagged_at IS NOT NULL AND tare_anomaly_resolved_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_origins_destinations_organization_id",
                table: "origins_destinations",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_types_organization_id",
                table: "cargo_types",
                column: "organization_id");

            migrationBuilder.AddForeignKey(
                name: "FK_cargo_types_organizations_organization_id",
                table: "cargo_types",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_origins_destinations_organizations_organization_id",
                table: "origins_destinations",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cargo_types_organizations_organization_id",
                table: "cargo_types");

            migrationBuilder.DropForeignKey(
                name: "FK_origins_destinations_organizations_organization_id",
                table: "origins_destinations");

            migrationBuilder.DropIndex(
                name: "IX_weighing_transactions_tare_anomaly_pending",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropIndex(
                name: "IX_vehicle_tare_history_tare_anomaly_pending",
                schema: "weighing",
                table: "vehicle_tare_history");

            migrationBuilder.DropIndex(
                name: "IX_origins_destinations_organization_id",
                table: "origins_destinations");

            migrationBuilder.DropIndex(
                name: "IX_cargo_types_organization_id",
                table: "cargo_types");

            migrationBuilder.DropColumn(
                name: "tare_anomaly_flagged_at",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "tare_anomaly_reason",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "tare_anomaly_resolution",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "tare_anomaly_resolved_at",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "tare_anomaly_resolved_by_user_id",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "rated_capacity_kg",
                schema: "weighing",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "tare_anomaly_flagged_at",
                schema: "weighing",
                table: "vehicle_tare_history");

            migrationBuilder.DropColumn(
                name: "tare_anomaly_reason",
                schema: "weighing",
                table: "vehicle_tare_history");

            migrationBuilder.DropColumn(
                name: "tare_anomaly_resolution",
                schema: "weighing",
                table: "vehicle_tare_history");

            migrationBuilder.DropColumn(
                name: "tare_anomaly_resolved_at",
                schema: "weighing",
                table: "vehicle_tare_history");

            migrationBuilder.DropColumn(
                name: "tare_anomaly_resolved_by_user_id",
                schema: "weighing",
                table: "vehicle_tare_history");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "origins_destinations");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "cargo_types");
        }
    }
}
