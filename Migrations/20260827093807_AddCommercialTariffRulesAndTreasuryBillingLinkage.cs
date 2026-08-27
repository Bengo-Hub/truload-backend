using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialTariffRulesAndTreasuryBillingLinkage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "credit_limit_kes",
                schema: "weighing",
                table: "transporters",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "crm_contact_id",
                schema: "weighing",
                table: "transporters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "on_account_billing",
                schema: "weighing",
                table: "transporters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "treasury_invoice_id",
                table: "invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "treasury-api Invoice.Id — the real AR/GL-backed invoice, distinct from TreasuryIntentId");

            migrationBuilder.CreateTable(
                name: "commercial_tariff_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    transporter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vehicle_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    axle_count_min = table.Column<int>(type: "integer", nullable: true),
                    axle_count_max = table.Column<int>(type: "integer", nullable: true),
                    weight_bracket_min_kg = table.Column<int>(type: "integer", nullable: true),
                    weight_bracket_max_kg = table.Column<int>(type: "integer", nullable: true),
                    fee_kes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_tariff_rules", x => x.id);
                    table.CheckConstraint("chk_commercial_tariff_rule_fee", "fee_kes >= 0");
                    table.ForeignKey(
                        name: "FK_commercial_tariff_rules_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_commercial_tariff_rules_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_commercial_tariff_rules_transporters_transporter_id",
                        column: x => x.transporter_id,
                        principalSchema: "weighing",
                        principalTable: "transporters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transporters_crm_contact_id",
                schema: "weighing",
                table: "transporters",
                column: "crm_contact_id",
                filter: "crm_contact_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_commercial_tariff_rules_organization_id",
                table: "commercial_tariff_rules",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "idx_commercial_tariff_rules_transporter_id",
                table: "commercial_tariff_rules",
                column: "transporter_id");

            migrationBuilder.CreateIndex(
                name: "IX_commercial_tariff_rules_station_id",
                table: "commercial_tariff_rules",
                column: "station_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commercial_tariff_rules");

            migrationBuilder.DropIndex(
                name: "IX_transporters_crm_contact_id",
                schema: "weighing",
                table: "transporters");

            migrationBuilder.DropColumn(
                name: "credit_limit_kes",
                schema: "weighing",
                table: "transporters");

            migrationBuilder.DropColumn(
                name: "crm_contact_id",
                schema: "weighing",
                table: "transporters");

            migrationBuilder.DropColumn(
                name: "on_account_billing",
                schema: "weighing",
                table: "transporters");

            migrationBuilder.DropColumn(
                name: "treasury_invoice_id",
                table: "invoices");
        }
    }
}
