using Microsoft.EntityFrameworkCore.Migrations;
using TruLoad.Backend.Data.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class FinalizePgVectorMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // pgvector extension (redundant safety — AlterDatabase in InitialCreate already handles it)
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            // SQL scripts — run AFTER all tables from InitialCreate exist
            // Note: HNSW vector indexes are already created by InitialCreate (scaffolded from model annotations)
            migrationBuilder.Sql(MigrationScriptHelper.GetScript("CreateRegularViews.sql"));
            migrationBuilder.Sql(MigrationScriptHelper.GetScript("CreateMaterializedViews.sql"));
            migrationBuilder.Sql(MigrationScriptHelper.GetScript("CreateWeighingTransactionPartitioning.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop views, materialized views, and partition infrastructure
            // Note: HNSW vector indexes are dropped by InitialCreate's Down()
            migrationBuilder.Sql(@"
                DROP VIEW IF EXISTS active_vehicle_tags, yard_status_summary, active_cases,
                    pending_court_hearings, active_arrest_warrants, recent_compliant_weighings,
                    pending_special_releases, active_permits CASCADE;
                DROP MATERIALIZED VIEW IF EXISTS mv_daily_weighing_stats, mv_charge_summaries,
                    mv_axle_group_violations, mv_driver_demerit_rankings,
                    mv_vehicle_violation_history, mv_station_performance_scorecard CASCADE;
                DROP FUNCTION IF EXISTS refresh_all_materialized_views();
                DROP FUNCTION IF EXISTS refresh_recent_materialized_views();
                DROP FUNCTION IF EXISTS create_weighing_partitions(INT, INT);
                DROP FUNCTION IF EXISTS ensure_yearly_partitions();
                DROP FUNCTION IF EXISTS archive_old_weighing_partitions(INT, BOOLEAN);
                DROP VIEW IF EXISTS weighing_partition_stats, weighing_partition_summary;
            ");
        }
    }
}
