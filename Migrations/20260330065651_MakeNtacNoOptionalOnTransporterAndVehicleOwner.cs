using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class MakeNtacNoOptionalOnTransporterAndVehicleOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vehicle_owners_ntac_no",
                schema: "weighing",
                table: "vehicle_owners");

            migrationBuilder.DropIndex(
                name: "IX_transporters_ntac_no",
                schema: "weighing",
                table: "transporters");

            migrationBuilder.AlterColumn<string>(
                name: "ntac_no",
                schema: "weighing",
                table: "vehicle_owners",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ntac_no",
                schema: "weighing",
                table: "transporters",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_owners_ntac_no",
                schema: "weighing",
                table: "vehicle_owners",
                column: "ntac_no",
                unique: true,
                filter: "ntac_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_transporters_ntac_no",
                schema: "weighing",
                table: "transporters",
                column: "ntac_no",
                unique: true,
                filter: "ntac_no IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vehicle_owners_ntac_no",
                schema: "weighing",
                table: "vehicle_owners");

            migrationBuilder.DropIndex(
                name: "IX_transporters_ntac_no",
                schema: "weighing",
                table: "transporters");

            migrationBuilder.AlterColumn<string>(
                name: "ntac_no",
                schema: "weighing",
                table: "vehicle_owners",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ntac_no",
                schema: "weighing",
                table: "transporters",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_owners_ntac_no",
                schema: "weighing",
                table: "vehicle_owners",
                column: "ntac_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transporters_ntac_no",
                schema: "weighing",
                table: "transporters",
                column: "ntac_no",
                unique: true);
        }
    }
}
