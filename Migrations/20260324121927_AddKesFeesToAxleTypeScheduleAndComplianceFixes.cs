using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddKesFeesToAxleTypeScheduleAndComplianceFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "QuadAxleFeeKes",
                table: "axle_type_overload_fee_schedules",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SingleDriveAxleFeeKes",
                table: "axle_type_overload_fee_schedules",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SteeringAxleFeeKes",
                table: "axle_type_overload_fee_schedules",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TandemAxleFeeKes",
                table: "axle_type_overload_fee_schedules",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TridemAxleFeeKes",
                table: "axle_type_overload_fee_schedules",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuadAxleFeeKes",
                table: "axle_type_overload_fee_schedules");

            migrationBuilder.DropColumn(
                name: "SingleDriveAxleFeeKes",
                table: "axle_type_overload_fee_schedules");

            migrationBuilder.DropColumn(
                name: "SteeringAxleFeeKes",
                table: "axle_type_overload_fee_schedules");

            migrationBuilder.DropColumn(
                name: "TandemAxleFeeKes",
                table: "axle_type_overload_fee_schedules");

            migrationBuilder.DropColumn(
                name: "TridemAxleFeeKes",
                table: "axle_type_overload_fee_schedules");
        }
    }
}
