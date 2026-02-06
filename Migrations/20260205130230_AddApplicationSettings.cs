using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CaseRegisterId",
                table: "vehicle_tags",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "special_releases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "special_releases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ComplianceCertificateId",
                table: "special_releases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "special_releases",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "special_releases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRejected",
                table: "special_releases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LoadCorrectionMemoId",
                table: "special_releases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "special_releases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedById",
                table: "special_releases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "special_releases",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "application_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettingKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "text", nullable: false),
                    SettingType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsEditable = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultValue = table.Column<string>(type: "text", nullable: true),
                    ValidationRules = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tags_CaseRegisterId",
                table: "vehicle_tags",
                column: "CaseRegisterId");

            migrationBuilder.AddForeignKey(
                name: "FK_vehicle_tags_case_registers_CaseRegisterId",
                table: "vehicle_tags",
                column: "CaseRegisterId",
                principalTable: "case_registers",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_vehicle_tags_case_registers_CaseRegisterId",
                table: "vehicle_tags");

            migrationBuilder.DropTable(
                name: "application_settings");

            migrationBuilder.DropIndex(
                name: "IX_vehicle_tags_CaseRegisterId",
                table: "vehicle_tags");

            migrationBuilder.DropColumn(
                name: "CaseRegisterId",
                table: "vehicle_tags");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "special_releases");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "special_releases");

            migrationBuilder.DropColumn(
                name: "ComplianceCertificateId",
                table: "special_releases");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "special_releases");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "special_releases");

            migrationBuilder.DropColumn(
                name: "IsRejected",
                table: "special_releases");

            migrationBuilder.DropColumn(
                name: "LoadCorrectionMemoId",
                table: "special_releases");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "special_releases");

            migrationBuilder.DropColumn(
                name: "RejectedById",
                table: "special_releases");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "special_releases");
        }
    }
}
