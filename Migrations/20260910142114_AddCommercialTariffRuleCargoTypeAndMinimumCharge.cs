using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialTariffRuleCargoTypeAndMinimumCharge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cargo_type_id",
                table: "commercial_tariff_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "minimum_charge_kes",
                table: "commercial_tariff_rules",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_commercial_tariff_rules_cargo_type_id",
                table: "commercial_tariff_rules",
                column: "cargo_type_id");

            migrationBuilder.AddCheckConstraint(
                name: "chk_commercial_tariff_rule_min_charge",
                table: "commercial_tariff_rules",
                sql: "minimum_charge_kes IS NULL OR minimum_charge_kes >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_commercial_tariff_rules_cargo_types_cargo_type_id",
                table: "commercial_tariff_rules",
                column: "cargo_type_id",
                principalTable: "cargo_types",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_commercial_tariff_rules_cargo_types_cargo_type_id",
                table: "commercial_tariff_rules");

            migrationBuilder.DropIndex(
                name: "idx_commercial_tariff_rules_cargo_type_id",
                table: "commercial_tariff_rules");

            migrationBuilder.DropCheckConstraint(
                name: "chk_commercial_tariff_rule_min_charge",
                table: "commercial_tariff_rules");

            migrationBuilder.DropColumn(
                name: "cargo_type_id",
                table: "commercial_tariff_rules");

            migrationBuilder.DropColumn(
                name: "minimum_charge_kes",
                table: "commercial_tariff_rules");
        }
    }
}
