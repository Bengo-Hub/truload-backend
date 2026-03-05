using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class RoadCountyRoadDistrictAndCourtLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_roads_Districts_district_id",
                table: "roads");

            migrationBuilder.DropIndex(
                name: "IX_roads_district_id",
                table: "roads");

            migrationBuilder.DropColumn(
                name: "district_id",
                table: "roads");

            migrationBuilder.AddColumn<Guid>(
                name: "county_id",
                table: "courts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "district_id",
                table: "courts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "road_counties",
                columns: table => new
                {
                    RoadId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_road_counties", x => new { x.RoadId, x.CountyId });
                    table.ForeignKey(
                        name: "FK_road_counties_Counties_CountyId",
                        column: x => x.CountyId,
                        principalTable: "Counties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_road_counties_roads_RoadId",
                        column: x => x.RoadId,
                        principalTable: "roads",
                        principalColumn: "id",
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
                name: "idx_courts_county_id",
                table: "courts",
                column: "county_id");

            migrationBuilder.CreateIndex(
                name: "idx_courts_district_id",
                table: "courts",
                column: "district_id");

            migrationBuilder.CreateIndex(
                name: "IX_road_counties_CountyId",
                table: "road_counties",
                column: "CountyId");

            migrationBuilder.CreateIndex(
                name: "IX_road_counties_RoadId",
                table: "road_counties",
                column: "RoadId");

            migrationBuilder.CreateIndex(
                name: "IX_road_districts_DistrictId",
                table: "road_districts",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_road_districts_RoadId",
                table: "road_districts",
                column: "RoadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "road_counties");

            migrationBuilder.DropTable(
                name: "road_districts");

            migrationBuilder.DropIndex(
                name: "idx_courts_county_id",
                table: "courts");

            migrationBuilder.DropIndex(
                name: "idx_courts_district_id",
                table: "courts");

            migrationBuilder.DropColumn(
                name: "county_id",
                table: "courts");

            migrationBuilder.DropColumn(
                name: "district_id",
                table: "courts");

            migrationBuilder.AddColumn<Guid>(
                name: "district_id",
                table: "roads",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_roads_district_id",
                table: "roads",
                column: "district_id");

            migrationBuilder.AddForeignKey(
                name: "FK_roads_Districts_district_id",
                table: "roads",
                column: "district_id",
                principalTable: "Districts",
                principalColumn: "Id");
        }
    }
}
