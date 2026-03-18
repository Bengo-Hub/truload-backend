using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWeighingLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocationSubcounty",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubcountyId",
                schema: "weighing",
                table: "weighing_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubcountyId",
                table: "stations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_SubcountyId",
                schema: "weighing",
                table: "weighing_transactions",
                column: "SubcountyId");

            migrationBuilder.CreateIndex(
                name: "IX_stations_SubcountyId",
                table: "stations",
                column: "SubcountyId");

            migrationBuilder.AddForeignKey(
                name: "FK_stations_subcounties_SubcountyId",
                table: "stations",
                column: "SubcountyId",
                principalTable: "subcounties",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_weighing_transactions_subcounties_SubcountyId",
                schema: "weighing",
                table: "weighing_transactions",
                column: "SubcountyId",
                principalTable: "subcounties",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stations_subcounties_SubcountyId",
                table: "stations");

            migrationBuilder.DropForeignKey(
                name: "FK_weighing_transactions_subcounties_SubcountyId",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropIndex(
                name: "IX_weighing_transactions_SubcountyId",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropIndex(
                name: "IX_stations_SubcountyId",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "LocationSubcounty",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "SubcountyId",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "SubcountyId",
                table: "stations");
        }
    }
}
