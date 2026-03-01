using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class FixMaterializedViewsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(TruLoad.Backend.Data.Migrations.MigrationScriptHelper.GetScript("CreateMaterializedViews.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping and re-creating with the same script is usually sufficient for materialized views
            // as they are dropped and re-created within the script itself.
            migrationBuilder.Sql(TruLoad.Backend.Data.Migrations.MigrationScriptHelper.GetScript("CreateMaterializedViews.sql"));
        }
    }
}
