using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddStaleAlertSentAtToWeighingTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StaleAlertSentAt",
                schema: "weighing",
                table: "weighing_transactions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StaleAlertSentAt",
                schema: "weighing",
                table: "weighing_transactions");
        }
    }
}
