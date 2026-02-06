using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddStationBoundCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stations_Counties_CountyId",
                table: "stations");

            migrationBuilder.DropForeignKey(
                name: "FK_stations_organizations_OrganizationId",
                table: "stations");

            migrationBuilder.DropForeignKey(
                name: "FK_stations_roads_RoadId",
                table: "stations");

            migrationBuilder.DropIndex(
                name: "idx_stations_code",
                table: "stations");

            migrationBuilder.DropIndex(
                name: "idx_stations_code_alias",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "station_code",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "station_name",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "status",
                table: "stations");

            migrationBuilder.RenameColumn(
                name: "RoadId",
                table: "stations",
                newName: "road_id");

            migrationBuilder.RenameColumn(
                name: "CountyId",
                table: "stations",
                newName: "county_id");

            migrationBuilder.RenameColumn(
                name: "BoundBCode",
                table: "stations",
                newName: "bound_b_code");

            migrationBuilder.RenameColumn(
                name: "BoundACode",
                table: "stations",
                newName: "bound_a_code");

            migrationBuilder.RenameIndex(
                name: "IX_stations_RoadId",
                table: "stations",
                newName: "IX_stations_road_id");

            migrationBuilder.RenameIndex(
                name: "IX_stations_CountyId",
                table: "stations",
                newName: "IX_stations_county_id");

            migrationBuilder.AddColumn<DateTime>(
                name: "AutoweighAt",
                schema: "weighing",
                table: "weighing_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoweighGvwKg",
                schema: "weighing",
                table: "weighing_transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaptureSource",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CaptureStatus",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "bound_b_code",
                table: "stations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "bound_a_code",
                table: "stations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_stations_code",
                table: "stations",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_stations_Counties_county_id",
                table: "stations",
                column: "county_id",
                principalTable: "Counties",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_stations_organizations_OrganizationId",
                table: "stations",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stations_roads_road_id",
                table: "stations",
                column: "road_id",
                principalTable: "roads",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stations_Counties_county_id",
                table: "stations");

            migrationBuilder.DropForeignKey(
                name: "FK_stations_organizations_OrganizationId",
                table: "stations");

            migrationBuilder.DropForeignKey(
                name: "FK_stations_roads_road_id",
                table: "stations");

            migrationBuilder.DropIndex(
                name: "idx_stations_code",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "AutoweighAt",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "AutoweighGvwKg",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "CaptureSource",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "CaptureStatus",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.RenameColumn(
                name: "road_id",
                table: "stations",
                newName: "RoadId");

            migrationBuilder.RenameColumn(
                name: "county_id",
                table: "stations",
                newName: "CountyId");

            migrationBuilder.RenameColumn(
                name: "bound_b_code",
                table: "stations",
                newName: "BoundBCode");

            migrationBuilder.RenameColumn(
                name: "bound_a_code",
                table: "stations",
                newName: "BoundACode");

            migrationBuilder.RenameIndex(
                name: "IX_stations_road_id",
                table: "stations",
                newName: "IX_stations_RoadId");

            migrationBuilder.RenameIndex(
                name: "IX_stations_county_id",
                table: "stations",
                newName: "IX_stations_CountyId");

            migrationBuilder.AlterColumn<string>(
                name: "BoundBCode",
                table: "stations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BoundACode",
                table: "stations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "station_code",
                table: "stations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "station_name",
                table: "stations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "stations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_stations_code",
                table: "stations",
                column: "station_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_stations_code_alias",
                table: "stations",
                column: "code");

            migrationBuilder.AddForeignKey(
                name: "FK_stations_Counties_CountyId",
                table: "stations",
                column: "CountyId",
                principalTable: "Counties",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_stations_organizations_OrganizationId",
                table: "stations",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_stations_roads_RoadId",
                table: "stations",
                column: "RoadId",
                principalTable: "roads",
                principalColumn: "id");
        }
    }
}
