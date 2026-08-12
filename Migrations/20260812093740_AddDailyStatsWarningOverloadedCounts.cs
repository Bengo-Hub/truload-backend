using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyStatsWarningOverloadedCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // mv_daily_weighing_stats is a raw-SQL materialized view (ToView, no EF-managed DDL) so
            // the model diff is empty; re-running the script re-creates it with the new
            // warning_count/overloaded_count columns (see the 2026-08-12 stats-accuracy fix).
            migrationBuilder.Sql(TruLoad.Backend.Data.Migrations.MigrationScriptHelper.GetScript("CreateMaterializedViews.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-running the same script is sufficient - the views are dropped and re-created within
            // the script itself, same as FixMaterializedViewsSchema's Down.
            migrationBuilder.Sql(TruLoad.Backend.Data.Migrations.MigrationScriptHelper.GetScript("CreateMaterializedViews.sql"));
        }
    }
}
