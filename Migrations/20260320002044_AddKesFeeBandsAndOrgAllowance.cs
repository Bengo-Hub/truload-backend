using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddKesFeeBandsAndOrgAllowance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalFeeKes",
                schema: "weighing",
                table: "weighing_transactions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OperationalAllowanceKg",
                table: "organizations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FeePerKgKes",
                table: "axle_fee_schedules",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FlatFeeKes",
                table: "axle_fee_schedules",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalFeeKes",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "OperationalAllowanceKg",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "FeePerKgKes",
                table: "axle_fee_schedules");

            migrationBuilder.DropColumn(
                name: "FlatFeeKes",
                table: "axle_fee_schedules");
        }
    }
}
