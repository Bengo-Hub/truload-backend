using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace truload_backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePermissionsColumnFromRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_audit_logs_entity_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "permissions",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "entity_id",
                table: "audit_logs");

            migrationBuilder.RenameColumn(
                name: "timestamp",
                table: "audit_logs",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "entity_type",
                table: "audit_logs",
                newName: "resource_type");

            migrationBuilder.RenameIndex(
                name: "idx_audit_logs_timestamp",
                table: "audit_logs",
                newName: "idx_audit_logs_created_at");

            migrationBuilder.RenameIndex(
                name: "idx_audit_logs_entity_type",
                table: "audit_logs",
                newName: "idx_audit_logs_resource_type");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "audit_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DenialReason",
                table: "audit_logs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endpoint",
                table: "audit_logs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HttpMethod",
                table: "audit_logs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestId",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredPermission",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResourceName",
                table: "audit_logs",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusCode",
                table: "audit_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Success",
                table: "audit_logs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "resource_id",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_resource_id",
                table: "audit_logs",
                column: "resource_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_audit_logs_resource_id",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "DenialReason",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "Endpoint",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "HttpMethod",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "RequiredPermission",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ResourceName",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "Success",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "resource_id",
                table: "audit_logs");

            migrationBuilder.RenameColumn(
                name: "resource_type",
                table: "audit_logs",
                newName: "entity_type");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "audit_logs",
                newName: "timestamp");

            migrationBuilder.RenameIndex(
                name: "idx_audit_logs_resource_type",
                table: "audit_logs",
                newName: "idx_audit_logs_entity_type");

            migrationBuilder.RenameIndex(
                name: "idx_audit_logs_created_at",
                table: "audit_logs",
                newName: "idx_audit_logs_timestamp");

            migrationBuilder.AddColumn<string>(
                name: "permissions",
                table: "roles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "audit_logs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "entity_id",
                table: "audit_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_entity_id",
                table: "audit_logs",
                column: "entity_id");
        }
    }
}
