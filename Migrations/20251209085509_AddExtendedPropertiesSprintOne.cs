using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace truload_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedPropertiesSprintOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "work_shifts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "work_shifts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "work_shift_schedules",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "work_shift_schedules",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "stations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "stations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "stations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "stations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "roles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "organizations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "departments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "departments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "departments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_stations_OrganizationId",
                table: "stations",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_stations_organizations_OrganizationId",
                table: "stations",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stations_organizations_OrganizationId",
                table: "stations");

            migrationBuilder.DropIndex(
                name: "IX_stations_OrganizationId",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "work_shifts");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "work_shifts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "work_shift_schedules");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "work_shift_schedules");

            migrationBuilder.DropColumn(
                name: "LastSyncAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "departments");
        }
    }
}
