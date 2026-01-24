using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class ScaleTestUPdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ScaleTestId",
                schema: "weighing",
                table: "weighing_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bound",
                table: "scale_tests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_ScaleTestId",
                schema: "weighing",
                table: "weighing_transactions",
                column: "ScaleTestId");

            migrationBuilder.AddForeignKey(
                name: "FK_weighing_transactions_scale_tests_ScaleTestId",
                schema: "weighing",
                table: "weighing_transactions",
                column: "ScaleTestId",
                principalTable: "scale_tests",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_weighing_transactions_scale_tests_ScaleTestId",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropIndex(
                name: "IX_weighing_transactions_ScaleTestId",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "ScaleTestId",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "bound",
                table: "scale_tests");
        }
    }
}
