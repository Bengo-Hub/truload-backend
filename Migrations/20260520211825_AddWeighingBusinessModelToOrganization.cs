using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWeighingBusinessModelToOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ToleranceExceeded",
                schema: "weighing",
                table: "weighing_transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WeighingBusinessModel",
                table: "organizations",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ToleranceExceeded",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "WeighingBusinessModel",
                table: "organizations");
        }
    }
}
