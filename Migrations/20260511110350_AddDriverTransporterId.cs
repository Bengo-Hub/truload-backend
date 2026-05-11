using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverTransporterId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TransporterId",
                schema: "weighing",
                table: "drivers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_drivers_TransporterId",
                schema: "weighing",
                table: "drivers",
                column: "TransporterId");

            migrationBuilder.AddForeignKey(
                name: "FK_drivers_transporters_TransporterId",
                schema: "weighing",
                table: "drivers",
                column: "TransporterId",
                principalSchema: "weighing",
                principalTable: "transporters",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_drivers_transporters_TransporterId",
                schema: "weighing",
                table: "drivers");

            migrationBuilder.DropIndex(
                name: "IX_drivers_TransporterId",
                schema: "weighing",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "TransporterId",
                schema: "weighing",
                table: "drivers");
        }
    }
}
