using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCargoTypeQualityFieldsAndOrgTareGrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "tare_grace_period_days",
                table: "organizations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "foreign_matter_limit_percent",
                table: "cargo_types",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "moisture_target_percent",
                table: "cargo_types",
                type: "numeric(5,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tare_grace_period_days",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "foreign_matter_limit_percent",
                table: "cargo_types");

            migrationBuilder.DropColumn(
                name: "moisture_target_percent",
                table: "cargo_types");
        }
    }
}
