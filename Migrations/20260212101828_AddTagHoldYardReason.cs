using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTagHoldYardReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_yard_entry_reason",
                table: "yard_entries");

            migrationBuilder.AddCheckConstraint(
                name: "chk_yard_entry_reason",
                table: "yard_entries",
                sql: "reason IN ('redistribution', 'gvw_overload', 'permit_check', 'offload', 'tag_hold')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_yard_entry_reason",
                table: "yard_entries");

            migrationBuilder.AddCheckConstraint(
                name: "chk_yard_entry_reason",
                table: "yard_entries",
                sql: "reason IN ('redistribution', 'gvw_overload', 'permit_check', 'offload')");
        }
    }
}
