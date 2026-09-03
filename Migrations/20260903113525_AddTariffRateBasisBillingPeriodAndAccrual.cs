using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTariffRateBasisBillingPeriodAndAccrual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "billing_period",
                table: "commercial_tariff_rules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Immediate");

            // Backfill EXISTING rules as "Flat" (their original, only-ever-flat behavior before this
            // migration) — NOT "PerTonne". Reinterpreting an already-configured flat fee as a
            // per-tonne rate would silently multiply what an org is actually charging. The DB-level
            // default for genuinely NEW rows going forward is set separately below to "PerTonne",
            // matching the C# model/DTO default (most commercial tenants bill by tonnage) — the
            // application always sets this column explicitly on insert either way, so this only
            // matters as a defensive backstop.
            migrationBuilder.AddColumn<string>(
                name: "rate_basis",
                table: "commercial_tariff_rules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Flat");

            migrationBuilder.Sql(
                "ALTER TABLE commercial_tariff_rules ALTER COLUMN rate_basis SET DEFAULT 'PerTonne';");

            migrationBuilder.CreateTable(
                name: "commercial_tariff_accruals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    weighing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tariff_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transporter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    billing_period = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    period_key = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    net_weight_kg = table.Column<int>(type: "integer", nullable: true),
                    computed_amount_kes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_tariff_accruals", x => x.id);
                    table.CheckConstraint("chk_commercial_tariff_accrual_amount", "computed_amount_kes >= 0");
                    table.ForeignKey(
                        name: "FK_commercial_tariff_accruals_commercial_tariff_rules_tariff_r~",
                        column: x => x.tariff_rule_id,
                        principalTable: "commercial_tariff_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_commercial_tariff_accruals_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_commercial_tariff_accruals_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_commercial_tariff_accruals_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_commercial_tariff_accruals_transporters_transporter_id",
                        column: x => x.transporter_id,
                        principalSchema: "weighing",
                        principalTable: "transporters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_commercial_tariff_accruals_org_status_period",
                table: "commercial_tariff_accruals",
                columns: new[] { "organization_id", "status", "period_key" });

            migrationBuilder.CreateIndex(
                name: "IX_commercial_tariff_accruals_invoice_id",
                table: "commercial_tariff_accruals",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_commercial_tariff_accruals_station_id",
                table: "commercial_tariff_accruals",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "IX_commercial_tariff_accruals_tariff_rule_id",
                table: "commercial_tariff_accruals",
                column: "tariff_rule_id");

            migrationBuilder.CreateIndex(
                name: "IX_commercial_tariff_accruals_transporter_id",
                table: "commercial_tariff_accruals",
                column: "transporter_id");

            migrationBuilder.CreateIndex(
                name: "uq_commercial_tariff_accruals_weighing_id",
                table: "commercial_tariff_accruals",
                column: "weighing_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commercial_tariff_accruals");

            migrationBuilder.DropColumn(
                name: "billing_period",
                table: "commercial_tariff_rules");

            migrationBuilder.DropColumn(
                name: "rate_basis",
                table: "commercial_tariff_rules");
        }
    }
}
