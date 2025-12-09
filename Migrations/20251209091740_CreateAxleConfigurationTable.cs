using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace truload_backend.Migrations
{
    /// <inheritdoc />
    public partial class CreateAxleConfigurationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "axle_configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    total_axles = table.Column<int>(type: "integer", nullable: false),
                    axle_pattern = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gvw_permissible_kg = table.Column<int>(type: "integer", nullable: false),
                    axle_limits = table.Column<string>(type: "jsonb", nullable: true),
                    visual_diagram_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_axle_configurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_axle_configs_code_unique",
                table: "axle_configurations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_axle_configs_is_active",
                table: "axle_configurations",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_axle_configs_total_axles",
                table: "axle_configurations",
                column: "total_axles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "axle_configurations");
        }
    }
}
