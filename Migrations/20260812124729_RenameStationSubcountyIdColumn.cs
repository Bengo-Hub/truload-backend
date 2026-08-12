using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class RenameStationSubcountyIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stations_subcounties_SubcountyId",
                table: "stations");

            migrationBuilder.RenameColumn(
                name: "SubcountyId",
                table: "stations",
                newName: "subcounty_id");

            migrationBuilder.RenameIndex(
                name: "IX_stations_SubcountyId",
                table: "stations",
                newName: "IX_stations_subcounty_id");

            migrationBuilder.AddForeignKey(
                name: "FK_stations_subcounties_subcounty_id",
                table: "stations",
                column: "subcounty_id",
                principalTable: "subcounties",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stations_subcounties_subcounty_id",
                table: "stations");

            migrationBuilder.RenameColumn(
                name: "subcounty_id",
                table: "stations",
                newName: "SubcountyId");

            migrationBuilder.RenameIndex(
                name: "IX_stations_subcounty_id",
                table: "stations",
                newName: "IX_stations_SubcountyId");

            migrationBuilder.AddForeignKey(
                name: "FK_stations_subcounties_SubcountyId",
                table: "stations",
                column: "SubcountyId",
                principalTable: "subcounties",
                principalColumn: "id");
        }
    }
}
