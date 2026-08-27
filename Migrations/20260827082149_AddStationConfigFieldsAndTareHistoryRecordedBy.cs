using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddStationConfigFieldsAndTareHistoryRecordedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "recorded_by_name",
                schema: "weighing",
                table: "vehicle_tare_history",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recorded_by_user_id",
                schema: "weighing",
                table: "vehicle_tare_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "default_weighing_mode",
                table: "stations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "operating_hours_end",
                table: "stations",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "operating_hours_start",
                table: "stations",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "printer_configuration",
                table: "stations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ticket_template",
                table: "stations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tare_history_recorded_by_user_id",
                schema: "weighing",
                table: "vehicle_tare_history",
                column: "recorded_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_vehicle_tare_history_asp_net_users_recorded_by_user_id",
                schema: "weighing",
                table: "vehicle_tare_history",
                column: "recorded_by_user_id",
                principalTable: "asp_net_users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_vehicle_tare_history_asp_net_users_recorded_by_user_id",
                schema: "weighing",
                table: "vehicle_tare_history");

            migrationBuilder.DropIndex(
                name: "IX_vehicle_tare_history_recorded_by_user_id",
                schema: "weighing",
                table: "vehicle_tare_history");

            migrationBuilder.DropColumn(
                name: "recorded_by_name",
                schema: "weighing",
                table: "vehicle_tare_history");

            migrationBuilder.DropColumn(
                name: "recorded_by_user_id",
                schema: "weighing",
                table: "vehicle_tare_history");

            migrationBuilder.DropColumn(
                name: "default_weighing_mode",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "operating_hours_end",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "operating_hours_start",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "printer_configuration",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "ticket_template",
                table: "stations");
        }
    }
}
