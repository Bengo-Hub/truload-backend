using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseRegisterDetentionStation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "detention_station_id",
                table: "case_registers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_case_registers_detention_station_id",
                table: "case_registers",
                column: "detention_station_id");

            migrationBuilder.AddForeignKey(
                name: "FK_case_registers_stations_detention_station_id",
                table: "case_registers",
                column: "detention_station_id",
                principalTable: "stations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_case_registers_stations_detention_station_id",
                table: "case_registers");

            migrationBuilder.DropIndex(
                name: "IX_case_registers_detention_station_id",
                table: "case_registers");

            migrationBuilder.DropColumn(
                name: "detention_station_id",
                table: "case_registers");
        }
    }
}
