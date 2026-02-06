using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixReweighCycleNoDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "reweigh_cycle_no",
                schema: "weighing",
                table: "weighing_transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            // Fix existing original weighings that incorrectly have reweigh_cycle_no = 1
            migrationBuilder.Sql(
                "UPDATE weighing.weighing_transactions SET reweigh_cycle_no = 0 WHERE original_weighing_id IS NULL AND reweigh_cycle_no = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "reweigh_cycle_no",
                schema: "weighing",
                table: "weighing_transactions",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);
        }
    }
}
