using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWeighingLocationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocationCounty",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LocationLat",
                schema: "weighing",
                table: "weighing_transactions",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LocationLng",
                schema: "weighing",
                table: "weighing_transactions",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationTown",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoadId",
                schema: "weighing",
                table: "weighing_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_RoadId",
                schema: "weighing",
                table: "weighing_transactions",
                column: "RoadId");

            migrationBuilder.AddForeignKey(
                name: "FK_weighing_transactions_roads_RoadId",
                schema: "weighing",
                table: "weighing_transactions",
                column: "RoadId",
                principalTable: "roads",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_weighing_transactions_roads_RoadId",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropIndex(
                name: "IX_weighing_transactions_RoadId",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "LocationCounty",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "LocationLat",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "LocationLng",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "LocationTown",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "RoadId",
                schema: "weighing",
                table: "weighing_transactions");
        }
    }
}
