using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class FixDriverSurnameWidthAndUniqueIndexFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_drivers_driving_license_no",
                schema: "weighing",
                table: "drivers");

            migrationBuilder.DropIndex(
                name: "IX_drivers_id_number",
                schema: "weighing",
                table: "drivers");

            migrationBuilder.AlterColumn<string>(
                name: "surname",
                schema: "weighing",
                table: "drivers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_drivers_driving_license_no",
                schema: "weighing",
                table: "drivers",
                column: "driving_license_no",
                unique: true,
                filter: "driving_license_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_drivers_id_number",
                schema: "weighing",
                table: "drivers",
                column: "id_number",
                unique: true,
                filter: "id_number IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_drivers_driving_license_no",
                schema: "weighing",
                table: "drivers");

            migrationBuilder.DropIndex(
                name: "IX_drivers_id_number",
                schema: "weighing",
                table: "drivers");

            migrationBuilder.AlterColumn<string>(
                name: "surname",
                schema: "weighing",
                table: "drivers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_drivers_driving_license_no",
                schema: "weighing",
                table: "drivers",
                column: "driving_license_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_drivers_id_number",
                schema: "weighing",
                table: "drivers",
                column: "id_number",
                unique: true);
        }
    }
}
