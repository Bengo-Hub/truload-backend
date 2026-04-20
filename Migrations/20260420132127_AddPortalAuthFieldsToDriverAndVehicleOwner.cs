using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPortalAuthFieldsToDriverAndVehicleOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "portal_account_email",
                schema: "weighing",
                table: "vehicle_owners",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "portal_account_id",
                schema: "weighing",
                table: "vehicle_owners",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "portal_account_email",
                schema: "weighing",
                table: "drivers",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "portal_account_id",
                schema: "weighing",
                table: "drivers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_owners_portal_account_email",
                schema: "weighing",
                table: "vehicle_owners",
                column: "portal_account_email",
                filter: "portal_account_email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_owners_portal_account_id",
                schema: "weighing",
                table: "vehicle_owners",
                column: "portal_account_id",
                filter: "portal_account_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_drivers_portal_account_email",
                schema: "weighing",
                table: "drivers",
                column: "portal_account_email",
                filter: "portal_account_email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_drivers_portal_account_id",
                schema: "weighing",
                table: "drivers",
                column: "portal_account_id",
                filter: "portal_account_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vehicle_owners_portal_account_email",
                schema: "weighing",
                table: "vehicle_owners");

            migrationBuilder.DropIndex(
                name: "IX_vehicle_owners_portal_account_id",
                schema: "weighing",
                table: "vehicle_owners");

            migrationBuilder.DropIndex(
                name: "IX_drivers_portal_account_email",
                schema: "weighing",
                table: "drivers");

            migrationBuilder.DropIndex(
                name: "IX_drivers_portal_account_id",
                schema: "weighing",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "portal_account_email",
                schema: "weighing",
                table: "vehicle_owners");

            migrationBuilder.DropColumn(
                name: "portal_account_id",
                schema: "weighing",
                table: "vehicle_owners");

            migrationBuilder.DropColumn(
                name: "portal_account_email",
                schema: "weighing",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "portal_account_id",
                schema: "weighing",
                table: "drivers");
        }
    }
}
