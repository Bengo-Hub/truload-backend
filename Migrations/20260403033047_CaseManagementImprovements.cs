using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class CaseManagementImprovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_axle_fee_schedules_lookup",
                table: "axle_fee_schedules");

            migrationBuilder.RenameColumn(
                name: "FlatFeeKes",
                table: "axle_fee_schedules",
                newName: "flat_fee_kes");

            migrationBuilder.RenameColumn(
                name: "FeePerKgKes",
                table: "axle_fee_schedules",
                newName: "fee_per_kg_kes");

            migrationBuilder.AddColumn<string>(
                name: "court_case_no",
                table: "case_registers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ob_extract_file_url",
                table: "case_registers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "police_case_file_no",
                table: "case_registers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "flat_fee_kes",
                table: "axle_fee_schedules",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "fee_per_kg_kes",
                table: "axle_fee_schedules",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<int>(
                name: "conviction_number",
                table: "axle_fee_schedules",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "case_party_id",
                table: "arrest_warrants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "execution_date",
                table: "arrest_warrants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "issued_date",
                table: "arrest_warrants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "warrant_file_url",
                table: "arrest_warrants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_axle_fee_schedules_lookup",
                table: "axle_fee_schedules",
                columns: new[] { "legal_framework", "fee_type", "conviction_number", "overload_min_kg", "overload_max_kg" });

            migrationBuilder.CreateIndex(
                name: "IX_arrest_warrants_case_party_id",
                table: "arrest_warrants",
                column: "case_party_id");

            migrationBuilder.AddForeignKey(
                name: "FK_arrest_warrants_case_parties_case_party_id",
                table: "arrest_warrants",
                column: "case_party_id",
                principalTable: "case_parties",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Data migration: Fix Traffic Act fee bands to match Rule 41(2) of Traffic Amendment Rules 2008
            // Delete incorrect TRAFFIC_ACT GVW bands and insert correct ones with 1st/2nd conviction support
            migrationBuilder.Sql(@"
                DELETE FROM axle_fee_schedules WHERE legal_framework = 'TRAFFIC_ACT' AND fee_type = 'GVW';

                INSERT INTO axle_fee_schedules (""Id"", legal_framework, fee_type, overload_min_kg, overload_max_kg, fee_per_kg_usd, flat_fee_usd, fee_per_kg_kes, flat_fee_kes, conviction_number, demerit_points, penalty_description, effective_from, is_active, created_at, updated_at)
                VALUES
                -- 1st conviction bands (Rule 41(2))
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 1, 999, 0, 0, 0, 5000, 1, 1, 'Traffic Act Rule 41(2) 1st conviction (1-999 kg) - KSh 5,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 1000, 1999, 0, 0, 0, 10000, 1, 2, 'Traffic Act Rule 41(2) 1st conviction (1000-1999 kg) - KSh 10,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 2000, 2999, 0, 0, 0, 15000, 1, 3, 'Traffic Act Rule 41(2) 1st conviction (2000-2999 kg) - KSh 15,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 3000, 3999, 0, 0, 0, 20000, 1, 4, 'Traffic Act Rule 41(2) 1st conviction (3000-3999 kg) - KSh 20,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 4000, 4999, 0, 0, 0, 30000, 1, 5, 'Traffic Act Rule 41(2) 1st conviction (4000-4999 kg) - KSh 30,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 5000, 5999, 0, 0, 0, 50000, 1, 6, 'Traffic Act Rule 41(2) 1st conviction (5000-5999 kg) - KSh 50,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 6000, 6999, 0, 0, 0, 75000, 1, 7, 'Traffic Act Rule 41(2) 1st conviction (6000-6999 kg) - KSh 75,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 7000, 7999, 0, 0, 0, 100000, 1, 8, 'Traffic Act Rule 41(2) 1st conviction (7000-7999 kg) - KSh 100,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 8000, 8999, 0, 0, 0, 150000, 1, 9, 'Traffic Act Rule 41(2) 1st conviction (8000-8999 kg) - KSh 150,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 9000, 9999, 0, 0, 0, 175000, 1, 10, 'Traffic Act Rule 41(2) 1st conviction (9000-9999 kg) - KSh 175,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 10000, NULL, 0, 0, 0, 200000, 1, 12, 'Traffic Act Rule 41(2) 1st conviction (>=10000 kg) - KSh 200,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                -- 2nd conviction bands (exactly 2x the 1st conviction fines)
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 1, 999, 0, 0, 0, 10000, 2, 3, 'Traffic Act Rule 41(2) 2nd conviction (1-999 kg) - KSh 10,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 1000, 1999, 0, 0, 0, 20000, 2, 4, 'Traffic Act Rule 41(2) 2nd conviction (1000-1999 kg) - KSh 20,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 2000, 2999, 0, 0, 0, 30000, 2, 5, 'Traffic Act Rule 41(2) 2nd conviction (2000-2999 kg) - KSh 30,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 3000, 3999, 0, 0, 0, 40000, 2, 6, 'Traffic Act Rule 41(2) 2nd conviction (3000-3999 kg) - KSh 40,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 4000, 4999, 0, 0, 0, 60000, 2, 7, 'Traffic Act Rule 41(2) 2nd conviction (4000-4999 kg) - KSh 60,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 5000, 5999, 0, 0, 0, 100000, 2, 8, 'Traffic Act Rule 41(2) 2nd conviction (5000-5999 kg) - KSh 100,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 6000, 6999, 0, 0, 0, 150000, 2, 9, 'Traffic Act Rule 41(2) 2nd conviction (6000-6999 kg) - KSh 150,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 7000, 7999, 0, 0, 0, 200000, 2, 10, 'Traffic Act Rule 41(2) 2nd conviction (7000-7999 kg) - KSh 200,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 8000, 8999, 0, 0, 0, 300000, 2, 11, 'Traffic Act Rule 41(2) 2nd conviction (8000-8999 kg) - KSh 300,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 9000, 9999, 0, 0, 0, 350000, 2, 12, 'Traffic Act Rule 41(2) 2nd conviction (9000-9999 kg) - KSh 350,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'TRAFFIC_ACT', 'GVW', 10000, NULL, 0, 0, 0, 400000, 2, 14, 'Traffic Act Rule 41(2) 2nd conviction (>=10000 kg) - KSh 400,000', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);

                -- Add missing hearing types
                INSERT INTO hearing_types (id, code, name, description, is_active, created_at, ""UpdatedAt"")
                SELECT gen_random_uuid(), v.code, v.name, v.description, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                FROM (VALUES
                    ('CONVICTION', 'Conviction', 'Court conviction of the accused'),
                    ('WARRANT_EXECUTION', 'Execution of Arrest Warrant', 'Hearing for execution of an arrest warrant'),
                    ('WARRANT_ISSUED', 'Warrant of Arrest Issued', 'Hearing where a warrant of arrest is issued'),
                    ('PLEA_GUILTY', 'Plea of Guilty', 'Accused enters a plea of guilty'),
                    ('PLEA_NOT_GUILTY', 'Plea of Not Guilty', 'Accused enters a plea of not guilty'),
                    ('HEARING', 'Hearing', 'General court hearing'),
                    ('PRE_TRIAL', 'Pre-Trial', 'Pre-trial conference or hearing'),
                    ('DEFENSE', 'Defense Hearing', 'Defense presents their case'),
                    ('JUDGMENT', 'Judgment', 'Court delivers judgment')
                ) AS v(code, name, description)
                WHERE NOT EXISTS (SELECT 1 FROM hearing_types ht WHERE ht.code = v.code);

                -- Add IN_FORCE and LIFTED warrant statuses
                INSERT INTO warrant_statuses (id, code, name, description, is_active, created_at, ""UpdatedAt"")
                SELECT gen_random_uuid(), v.code, v.name, v.description, true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                FROM (VALUES
                    ('IN_FORCE', 'In Force', 'Warrant is active and enforceable'),
                    ('LIFTED', 'Lifted', 'Court has lifted the warrant')
                ) AS v(code, name, description)
                WHERE NOT EXISTS (SELECT 1 FROM warrant_statuses ws WHERE ws.code = v.code);

                -- Add EAC 2nd conviction GVW bands (5x penalty per Section 20(1)(a))
                INSERT INTO axle_fee_schedules (""Id"", legal_framework, fee_type, overload_min_kg, overload_max_kg, fee_per_kg_usd, flat_fee_usd, fee_per_kg_kes, flat_fee_kes, conviction_number, demerit_points, penalty_description, effective_from, is_active, created_at, updated_at)
                VALUES
                (gen_random_uuid(), 'EAC', 'GVW', 1, 500, 2.50, 0, 0, 0, 2, 2, 'EAC 2nd conviction GVW (1-500 kg) - 5x penalty', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'GVW', 501, 1000, 3.75, 0, 0, 0, 2, 4, 'EAC 2nd conviction GVW (501-1000 kg) - 5x penalty', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'GVW', 1001, 1500, 5.00, 0, 0, 0, 2, 6, 'EAC 2nd conviction GVW (1001-1500 kg) - 5x penalty', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'GVW', 1501, 3000, 12.50, 0, 0, 0, 2, 8, 'EAC 2nd conviction GVW (1501-3000 kg) - 5x penalty', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'GVW', 3001, NULL, 25.00, 2500, 0, 0, 2, 15, 'EAC 2nd conviction GVW (>3000 kg) - 5x penalty', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);

                -- Add EAC 2nd conviction Axle bands (5x penalty per Section 20(1)(a))
                INSERT INTO axle_fee_schedules (""Id"", legal_framework, fee_type, overload_min_kg, overload_max_kg, fee_per_kg_usd, flat_fee_usd, fee_per_kg_kes, flat_fee_kes, conviction_number, demerit_points, penalty_description, effective_from, is_active, created_at, updated_at)
                VALUES
                (gen_random_uuid(), 'EAC', 'AXLE', 1, 200, 2.00, 0, 0, 0, 2, 2, 'EAC 2nd conviction Axle (1-200 kg) - 5x penalty', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'AXLE', 201, 500, 3.00, 0, 0, 0, 2, 4, 'EAC 2nd conviction Axle (201-500 kg) - 5x penalty', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'AXLE', 501, 1000, 5.00, 0, 0, 0, 2, 6, 'EAC 2nd conviction Axle (501-1000 kg) - 5x penalty', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'AXLE', 1001, 1500, 7.50, 0, 0, 0, 2, 7, 'EAC 2nd conviction Axle (1001-1500 kg) - 5x penalty', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
                (gen_random_uuid(), 'EAC', 'AXLE', 1501, NULL, 15.00, 1000, 0, 0, 2, 12, 'EAC 2nd conviction Axle (>1500 kg) - 5x penalty', '2024-01-01', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_arrest_warrants_case_parties_case_party_id",
                table: "arrest_warrants");

            migrationBuilder.DropIndex(
                name: "IX_axle_fee_schedules_lookup",
                table: "axle_fee_schedules");

            migrationBuilder.DropIndex(
                name: "IX_arrest_warrants_case_party_id",
                table: "arrest_warrants");

            migrationBuilder.DropColumn(
                name: "court_case_no",
                table: "case_registers");

            migrationBuilder.DropColumn(
                name: "ob_extract_file_url",
                table: "case_registers");

            migrationBuilder.DropColumn(
                name: "police_case_file_no",
                table: "case_registers");

            migrationBuilder.DropColumn(
                name: "conviction_number",
                table: "axle_fee_schedules");

            migrationBuilder.DropColumn(
                name: "case_party_id",
                table: "arrest_warrants");

            migrationBuilder.DropColumn(
                name: "execution_date",
                table: "arrest_warrants");

            migrationBuilder.DropColumn(
                name: "issued_date",
                table: "arrest_warrants");

            migrationBuilder.DropColumn(
                name: "warrant_file_url",
                table: "arrest_warrants");

            migrationBuilder.RenameColumn(
                name: "flat_fee_kes",
                table: "axle_fee_schedules",
                newName: "FlatFeeKes");

            migrationBuilder.RenameColumn(
                name: "fee_per_kg_kes",
                table: "axle_fee_schedules",
                newName: "FeePerKgKes");

            migrationBuilder.AlterColumn<decimal>(
                name: "FlatFeeKes",
                table: "axle_fee_schedules",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "FeePerKgKes",
                table: "axle_fee_schedules",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldDefaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_axle_fee_schedules_lookup",
                table: "axle_fee_schedules",
                columns: new[] { "legal_framework", "fee_type", "overload_min_kg", "overload_max_kg" });
        }
    }
}
