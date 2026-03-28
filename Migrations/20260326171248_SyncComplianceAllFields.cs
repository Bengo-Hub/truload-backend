using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class SyncComplianceAllFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AxleToleranceDisplay",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GvwToleranceDisplay",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GvwToleranceKg",
                schema: "weighing",
                table: "weighing_transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OperationalAllowanceUsed",
                schema: "weighing",
                table: "weighing_transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ToleranceKg",
                table: "axle_weight_references",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TolerancePercentage",
                table: "axle_weight_references",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PermissibleGvwKg",
                table: "axle_configurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToleranceKg",
                table: "axle_configurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TolerancePercentage",
                table: "axle_configurations",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AxleToleranceDisplay",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "GvwToleranceDisplay",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "GvwToleranceKg",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "OperationalAllowanceUsed",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "ToleranceKg",
                table: "axle_weight_references");

            migrationBuilder.DropColumn(
                name: "TolerancePercentage",
                table: "axle_weight_references");

            migrationBuilder.DropColumn(
                name: "PermissibleGvwKg",
                table: "axle_configurations");

            migrationBuilder.DropColumn(
                name: "ToleranceKg",
                table: "axle_configurations");

            migrationBuilder.DropColumn(
                name: "TolerancePercentage",
                table: "axle_configurations");
        }
    }
}
