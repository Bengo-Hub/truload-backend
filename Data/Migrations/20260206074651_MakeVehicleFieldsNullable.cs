using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeVehicleFieldsNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_vehicles_vehicle_owners_owner_id",
                schema: "weighing",
                table: "vehicles");

            migrationBuilder.DropIndex(
                name: "IX_vehicles_chassis_no",
                schema: "weighing",
                table: "vehicles");

            migrationBuilder.AlterColumn<string>(
                name: "vehicle_type",
                schema: "weighing",
                table: "vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<Guid>(
                name: "owner_id",
                schema: "weighing",
                table: "vehicles",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "model",
                schema: "weighing",
                table: "vehicles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "make",
                schema: "weighing",
                table: "vehicles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "engine_no",
                schema: "weighing",
                table: "vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "color",
                schema: "weighing",
                table: "vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "chassis_no",
                schema: "weighing",
                table: "vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_chassis_no",
                schema: "weighing",
                table: "vehicles",
                column: "chassis_no",
                unique: true,
                filter: "chassis_no IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_vehicles_vehicle_owners_owner_id",
                schema: "weighing",
                table: "vehicles",
                column: "owner_id",
                principalSchema: "weighing",
                principalTable: "vehicle_owners",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_vehicles_vehicle_owners_owner_id",
                schema: "weighing",
                table: "vehicles");

            migrationBuilder.DropIndex(
                name: "IX_vehicles_chassis_no",
                schema: "weighing",
                table: "vehicles");

            migrationBuilder.AlterColumn<string>(
                name: "vehicle_type",
                schema: "weighing",
                table: "vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "owner_id",
                schema: "weighing",
                table: "vehicles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "model",
                schema: "weighing",
                table: "vehicles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "make",
                schema: "weighing",
                table: "vehicles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "engine_no",
                schema: "weighing",
                table: "vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "color",
                schema: "weighing",
                table: "vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "chassis_no",
                schema: "weighing",
                table: "vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_chassis_no",
                schema: "weighing",
                table: "vehicles",
                column: "chassis_no",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_vehicles_vehicle_owners_owner_id",
                schema: "weighing",
                table: "vehicles",
                column: "owner_id",
                principalSchema: "weighing",
                principalTable: "vehicle_owners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
