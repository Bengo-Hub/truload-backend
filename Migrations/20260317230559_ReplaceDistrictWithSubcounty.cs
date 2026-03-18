using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceDistrictWithSubcounty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_subcounties_Districts_district_id",
                table: "subcounties");

            migrationBuilder.DropTable(
                name: "road_districts");

            migrationBuilder.DropTable(
                name: "Districts");

            migrationBuilder.DropColumn(
                name: "district_id",
                table: "case_registers");

            migrationBuilder.RenameColumn(
                name: "district_id",
                table: "subcounties",
                newName: "county_id");

            migrationBuilder.RenameIndex(
                name: "idx_subcounties_district_id",
                table: "subcounties",
                newName: "idx_subcounties_county_id");

            migrationBuilder.RenameColumn(
                name: "district_id",
                table: "courts",
                newName: "subcounty_id");

            migrationBuilder.RenameIndex(
                name: "idx_courts_district_id",
                table: "courts",
                newName: "idx_courts_subcounty_id");

            migrationBuilder.CreateTable(
                name: "road_subcounties",
                columns: table => new
                {
                    RoadId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubcountyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_road_subcounties", x => new { x.RoadId, x.SubcountyId });
                    table.ForeignKey(
                        name: "FK_road_subcounties_roads_RoadId",
                        column: x => x.RoadId,
                        principalTable: "roads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_road_subcounties_subcounties_SubcountyId",
                        column: x => x.SubcountyId,
                        principalTable: "subcounties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_road_subcounties_RoadId",
                table: "road_subcounties",
                column: "RoadId");

            migrationBuilder.CreateIndex(
                name: "IX_road_subcounties_SubcountyId",
                table: "road_subcounties",
                column: "SubcountyId");

            // Courts previously had district_id (FK to Districts). After renaming to subcounty_id,
            // those values are no longer valid (Districts dropped; subcounties have different IDs).
            // Null out court subcounty_id so the FK to subcounties can be added.
            migrationBuilder.Sql(@"
                UPDATE courts
                SET subcounty_id = NULL
                WHERE subcounty_id IS NOT NULL
                  AND subcounty_id NOT IN (SELECT id FROM subcounties);
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_courts_subcounties_subcounty_id",
                table: "courts",
                column: "subcounty_id",
                principalTable: "subcounties",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_subcounties_Counties_county_id",
                table: "subcounties",
                column: "county_id",
                principalTable: "Counties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_courts_subcounties_subcounty_id",
                table: "courts");

            migrationBuilder.DropForeignKey(
                name: "FK_subcounties_Counties_county_id",
                table: "subcounties");

            migrationBuilder.DropTable(
                name: "road_subcounties");

            migrationBuilder.RenameColumn(
                name: "county_id",
                table: "subcounties",
                newName: "district_id");

            migrationBuilder.RenameIndex(
                name: "idx_subcounties_county_id",
                table: "subcounties",
                newName: "idx_subcounties_district_id");

            migrationBuilder.RenameColumn(
                name: "subcounty_id",
                table: "courts",
                newName: "district_id");

            migrationBuilder.RenameIndex(
                name: "idx_courts_subcounty_id",
                table: "courts",
                newName: "idx_courts_district_id");

            migrationBuilder.AddColumn<Guid>(
                name: "district_id",
                table: "case_registers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Districts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Districts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Districts_Counties_CountyId",
                        column: x => x.CountyId,
                        principalTable: "Counties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "road_districts",
                columns: table => new
                {
                    RoadId = table.Column<Guid>(type: "uuid", nullable: false),
                    DistrictId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_road_districts", x => new { x.RoadId, x.DistrictId });
                    table.ForeignKey(
                        name: "FK_road_districts_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_road_districts_roads_RoadId",
                        column: x => x.RoadId,
                        principalTable: "roads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Districts_CountyId",
                table: "Districts",
                column: "CountyId");

            migrationBuilder.CreateIndex(
                name: "IX_road_districts_DistrictId",
                table: "road_districts",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_road_districts_RoadId",
                table: "road_districts",
                column: "RoadId");

            migrationBuilder.AddForeignKey(
                name: "FK_subcounties_Districts_district_id",
                table: "subcounties",
                column: "district_id",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
