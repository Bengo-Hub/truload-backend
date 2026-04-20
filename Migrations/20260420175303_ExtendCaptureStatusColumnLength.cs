using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class ExtendCaptureStatusColumnLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CaptureStatus",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "captured",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "CaptureSource",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "manual",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CaptureStatus",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "captured");

            migrationBuilder.AlterColumn<string>(
                name: "CaptureSource",
                schema: "weighing",
                table: "weighing_transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "manual");
        }
    }
}
