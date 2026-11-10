using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class FixDailyStatsCapturedAndKesFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // mv_daily_weighing_stats is a raw-SQL materialized view (ToView), so the model diff is
            // empty. Re-running the script re-creates it counting finished weighings only
            // (DeletedAt IS NULL AND CaptureStatus = 'captured', matching the reports), with
            // legal_count (ControlStatus = 'Compliant') and total_fees_kes alongside the USD
            // total_fees_collected. See truload-open-items-after-wd2-2026-11-04.md findings 1 to 6.
            migrationBuilder.Sql(TruLoad.Backend.Data.Migrations.MigrationScriptHelper.GetScript("CreateMaterializedViews.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-running the same script is sufficient: the views are dropped and re-created within
            // the script itself, same as EatShiftDailyWeighingStatsDate's Down.
            migrationBuilder.Sql(TruLoad.Backend.Data.Migrations.MigrationScriptHelper.GetScript("CreateMaterializedViews.sql"));
        }
    }
}
