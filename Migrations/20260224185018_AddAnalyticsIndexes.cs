using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Indexes on timestamp columns frequently filtered in dashboard and report queries
            migrationBuilder.CreateIndex(
                name: "IX_case_registers_created_at",
                table: "case_registers",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_prosecution_cases_created_at",
                table: "prosecution_cases",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_yard_entries_entered_at",
                table: "yard_entries",
                column: "entered_at");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tags_opened_at",
                table: "vehicle_tags",
                column: "opened_at");

            migrationBuilder.CreateIndex(
                name: "IX_court_hearings_hearing_date",
                table: "court_hearings",
                column: "hearing_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_case_registers_created_at",
                table: "case_registers");

            migrationBuilder.DropIndex(
                name: "IX_prosecution_cases_created_at",
                table: "prosecution_cases");

            migrationBuilder.DropIndex(
                name: "IX_yard_entries_entered_at",
                table: "yard_entries");

            migrationBuilder.DropIndex(
                name: "IX_vehicle_tags_opened_at",
                table: "vehicle_tags");

            migrationBuilder.DropIndex(
                name: "IX_court_hearings_hearing_date",
                table: "court_hearings");
        }
    }
}
