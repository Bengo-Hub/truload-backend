using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialTariffRuleBilledToTransporter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "billed_to_transporter_id",
                table: "commercial_tariff_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_commercial_tariff_rules_billed_to_transporter_id",
                table: "commercial_tariff_rules",
                column: "billed_to_transporter_id");

            migrationBuilder.AddForeignKey(
                name: "FK_commercial_tariff_rules_transporters_billed_to_transporter_~",
                table: "commercial_tariff_rules",
                column: "billed_to_transporter_id",
                principalSchema: "weighing",
                principalTable: "transporters",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_commercial_tariff_rules_transporters_billed_to_transporter_~",
                table: "commercial_tariff_rules");

            migrationBuilder.DropIndex(
                name: "idx_commercial_tariff_rules_billed_to_transporter_id",
                table: "commercial_tariff_rules");

            migrationBuilder.DropColumn(
                name: "billed_to_transporter_id",
                table: "commercial_tariff_rules");
        }
    }
}
