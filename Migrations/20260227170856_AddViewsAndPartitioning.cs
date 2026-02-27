using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddViewsAndPartitioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Apply partitioning logic (Conversion, Partitions, Trigger, Functions)
            migrationBuilder.Sql(TruLoad.Backend.Data.Migrations.MigrationScriptHelper.GetScript("CreateWeighingTransactionPartitioning.sql"));

            // 2. Create Regular Views
            migrationBuilder.Sql(TruLoad.Backend.Data.Migrations.MigrationScriptHelper.GetScript("CreateRegularViews.sql"));

            // 3. Create Materialized Views
            migrationBuilder.Sql(TruLoad.Backend.Data.Migrations.MigrationScriptHelper.GetScript("CreateMaterializedViews.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop Views (Cascading handle dependencies)
            migrationBuilder.Sql("DROP VIEW IF EXISTS weighing.vw_weighing_transactions_details CASCADE;");
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS weighing.mv_organization_weighing_summary CASCADE;");
            
            // Reverting partitioning in a down migration is complex and typically 
            // involves dropping the partitioned table and recreating it as a regular table.
            // For this clean-slate approach, dropping the schema or resetting is more common.
        }
    }
}
