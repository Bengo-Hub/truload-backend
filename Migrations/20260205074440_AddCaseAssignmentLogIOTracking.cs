using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseAssignmentLogIOTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_case_assignment_type",
                table: "case_assignment_logs");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "weighing",
                table: "vehicles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "issuing_authority",
                schema: "weighing",
                table: "permits",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "permit_types",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "is_current",
                table: "case_assignment_logs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "officer_rank",
                table: "case_assignment_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_case_assignment_logs_current_io",
                table: "case_assignment_logs",
                columns: new[] { "case_register_id", "is_current" });

            migrationBuilder.AddCheckConstraint(
                name: "chk_case_assignment_type",
                table: "case_assignment_logs",
                sql: "assignment_type IN ('initial', 're_assignment', 'transfer', 'handover')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_case_assignment_logs_current_io",
                table: "case_assignment_logs");

            migrationBuilder.DropCheckConstraint(
                name: "chk_case_assignment_type",
                table: "case_assignment_logs");

            migrationBuilder.DropColumn(
                name: "is_current",
                table: "case_assignment_logs");

            migrationBuilder.DropColumn(
                name: "officer_rank",
                table: "case_assignment_logs");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "weighing",
                table: "vehicles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "issuing_authority",
                schema: "weighing",
                table: "permits",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "permit_types",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "chk_case_assignment_type",
                table: "case_assignment_logs",
                sql: "assignment_type IN ('initial', 're_assignment', 'transfer')");
        }
    }
}
