using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class ModalCkecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "details",
                table: "scale_tests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<int>(
                name: "actual_weight_kg",
                table: "scale_tests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "test_type",
                table: "scale_tests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "vehicle_plate",
                table: "scale_tests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "weighing_mode",
                table: "scale_tests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "actual_weight_kg",
                table: "scale_tests");

            migrationBuilder.DropColumn(
                name: "test_type",
                table: "scale_tests");

            migrationBuilder.DropColumn(
                name: "vehicle_plate",
                table: "scale_tests");

            migrationBuilder.DropColumn(
                name: "weighing_mode",
                table: "scale_tests");

            migrationBuilder.AlterColumn<string>(
                name: "details",
                table: "scale_tests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
