using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStationScorecardSubcounty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // mv_station_performance_scorecard is a raw-SQL materialized view (ToView, no EF-managed
            // DDL) so the model diff is empty; re-running the script is how this repo's precedent
            // migration (FixMaterializedViewsSchema) re-creates it with the updated SELECT/JOIN/GROUP BY
            // (adds subcounty_name).
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
