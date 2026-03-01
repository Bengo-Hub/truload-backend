using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class FixDriverDemeritIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(TruLoad.Backend.Data.Migrations.MigrationScriptHelper.GetScript("CreateMaterializedViews.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(TruLoad.Backend.Data.Migrations.MigrationScriptHelper.GetScript("CreateMaterializedViews.sql"));
        }
    }
}
