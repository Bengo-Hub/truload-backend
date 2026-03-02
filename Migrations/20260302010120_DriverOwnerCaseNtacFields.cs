using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class DriverOwnerCaseNtacFields : Migration
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
                name: "IX_case_registers_complainant_officer_id",
                table: "case_registers",
                column: "complainant_officer_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_registers_detention_station_id",
                table: "case_registers",
                column: "detention_station_id");

            migrationBuilder.AddForeignKey(
                name: "FK_case_registers_asp_net_users_complainant_officer_id",
                table: "case_registers",
                column: "complainant_officer_id",
                principalTable: "asp_net_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

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
                name: "FK_case_registers_asp_net_users_complainant_officer_id",
                table: "case_registers");

            migrationBuilder.DropForeignKey(
                name: "FK_case_registers_stations_detention_station_id",
                table: "case_registers");

            migrationBuilder.DropIndex(
                name: "IX_case_registers_complainant_officer_id",
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
