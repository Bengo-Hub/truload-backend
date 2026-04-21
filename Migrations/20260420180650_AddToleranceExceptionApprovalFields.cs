using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddToleranceExceptionApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "tolerance_exception_approved",
                schema: "weighing",
                table: "weighing_transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "tolerance_exception_approved_at",
                schema: "weighing",
                table: "weighing_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tolerance_exception_approved_by",
                schema: "weighing",
                table: "weighing_transactions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tolerance_exception_approved",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "tolerance_exception_approved_at",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropColumn(
                name: "tolerance_exception_approved_by",
                schema: "weighing",
                table: "weighing_transactions");
        }
    }
}
