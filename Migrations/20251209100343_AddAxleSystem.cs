using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace truload_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAxleSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_axle_configs_is_active",
                table: "axle_configurations");

            migrationBuilder.DropColumn(
                name: "axle_limits",
                table: "axle_configurations");

            migrationBuilder.DropColumn(
                name: "axle_pattern",
                table: "axle_configurations");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                table: "work_shifts",
                newName: "deleted_at");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "work_shift_schedules",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "work_shift_schedules",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "Location",
                table: "stations",
                newName: "location");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "stations",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                table: "stations",
                newName: "deleted_at");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "roles",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "roles",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "total_axles",
                table: "axle_configurations",
                newName: "axle_number");

            migrationBuilder.RenameColumn(
                name: "code",
                table: "axle_configurations",
                newName: "axle_code");

            migrationBuilder.RenameIndex(
                name: "idx_axle_configs_total_axles",
                table: "axle_configurations",
                newName: "idx_axle_configurations_axle_number");

            migrationBuilder.RenameIndex(
                name: "idx_axle_configs_code_unique",
                table: "axle_configurations",
                newName: "idx_axle_configurations_code_unique");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "work_shifts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "shift_code",
                table: "work_shifts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shift_name",
                table: "work_shifts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "end_time_str",
                table: "work_shift_schedules",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "start_time_str",
                table: "work_shift_schedules",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "location",
                table: "stations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "code",
                table: "stations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "station_name",
                table: "stations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "stations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "roles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "visual_diagram_url",
                table: "axle_configurations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "axle_configurations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "axle_name",
                table: "axle_configurations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                table: "axle_configurations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_standard",
                table: "axle_configurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "legal_framework",
                table: "axle_configurations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "BOTH");

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "axle_configurations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "axle_fee_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_framework = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fee_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    overload_min_kg = table.Column<int>(type: "integer", nullable: false),
                    overload_max_kg = table.Column<int>(type: "integer", nullable: true),
                    fee_per_kg_usd = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    flat_fee_usd = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    demerit_points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    penalty_description = table.Column<string>(type: "text", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_axle_fee_schedules", x => x.Id);
                    table.CheckConstraint("chk_fee_type", "fee_type IN ('GVW', 'AXLE')");
                    table.CheckConstraint("chk_legal_framework", "legal_framework IN ('EAC', 'TRAFFIC_ACT')");
                });

            migrationBuilder.CreateTable(
                name: "axle_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    typical_weight_kg = table.Column<int>(type: "integer", nullable: false),
                    min_spacing_feet = table.Column<decimal>(type: "numeric(4,1)", nullable: true),
                    max_spacing_feet = table.Column<decimal>(type: "numeric(4,1)", nullable: true),
                    axle_count_in_group = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_axle_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permit_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    axle_extension_kg = table.Column<int>(type: "integer", nullable: false),
                    gvw_extension_kg = table.Column<int>(type: "integer", nullable: false),
                    validity_days = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permit_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tolerance_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    legal_framework = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tolerance_percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    tolerance_kg = table.Column<int>(type: "integer", nullable: true),
                    applies_to = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tolerance_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tyre_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    typical_max_weight_kg = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tyre_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "axle_weight_references",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_position = table.Column<int>(type: "integer", nullable: false),
                    axle_legal_weight_kg = table.Column<int>(type: "integer", nullable: false),
                    axle_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_grouping = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tyre_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_axle_weight_references", x => x.Id);
                    table.ForeignKey(
                        name: "FK_axle_weight_references_axle_configurations_axle_configurati~",
                        column: x => x.axle_configuration_id,
                        principalTable: "axle_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_axle_weight_references_axle_groups_axle_group_id",
                        column: x => x.axle_group_id,
                        principalTable: "axle_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_axle_weight_references_tyre_types_tyre_type_id",
                        column: x => x.tyre_type_id,
                        principalTable: "tyre_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "weighing_axles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    weighing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_number = table.Column<int>(type: "integer", nullable: false),
                    measured_weight_kg = table.Column<int>(type: "integer", nullable: false),
                    permissible_weight_kg = table.Column<int>(type: "integer", nullable: false),
                    axle_configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_weight_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    axle_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axle_grouping = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tyre_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fee_usd = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    captured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weighing_axles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_weighing_axles_axle_configurations_axle_configuration_id",
                        column: x => x.axle_configuration_id,
                        principalTable: "axle_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weighing_axles_axle_groups_axle_group_id",
                        column: x => x.axle_group_id,
                        principalTable: "axle_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weighing_axles_axle_weight_references_axle_weight_reference~",
                        column: x => x.axle_weight_reference_id,
                        principalTable: "axle_weight_references",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_weighing_axles_tyre_types_tyre_type_id",
                        column: x => x.tyre_type_id,
                        principalTable: "tyre_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_stations_code_alias",
                table: "stations",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "idx_roles_code",
                table: "roles",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "idx_axle_configurations_active",
                table: "axle_configurations",
                columns: new[] { "is_active", "deleted_at" },
                filter: "is_active = true AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_axle_configurations_framework",
                table: "axle_configurations",
                column: "legal_framework");

            migrationBuilder.CreateIndex(
                name: "idx_axle_configurations_standard",
                table: "axle_configurations",
                column: "is_standard",
                filter: "is_standard = true");

            migrationBuilder.CreateIndex(
                name: "IX_axle_configurations_created_by_user_id",
                table: "axle_configurations",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_axle_fee_schedule_effective",
                table: "axle_fee_schedules",
                columns: new[] { "effective_from", "effective_to" });

            migrationBuilder.CreateIndex(
                name: "idx_axle_fee_schedule_framework_type",
                table: "axle_fee_schedules",
                columns: new[] { "legal_framework", "fee_type" });

            migrationBuilder.CreateIndex(
                name: "idx_axle_groups_active",
                table: "axle_groups",
                column: "is_active",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_axle_groups_code_unique",
                table: "axle_groups",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_axle_weight_ref_config_id",
                table: "axle_weight_references",
                column: "axle_configuration_id");

            migrationBuilder.CreateIndex(
                name: "idx_axle_weight_ref_config_position_unique",
                table: "axle_weight_references",
                columns: new[] { "axle_configuration_id", "axle_position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_axle_weight_ref_group_id",
                table: "axle_weight_references",
                column: "axle_group_id");

            migrationBuilder.CreateIndex(
                name: "idx_axle_weight_ref_tyre_type_id",
                table: "axle_weight_references",
                column: "tyre_type_id");

            migrationBuilder.CreateIndex(
                name: "idx_permit_types_code",
                table: "permit_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tolerance_settings_code",
                table: "tolerance_settings",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tolerance_settings_effective_dates",
                table: "tolerance_settings",
                columns: new[] { "effective_from", "effective_to" });

            migrationBuilder.CreateIndex(
                name: "idx_tyre_types_active",
                table: "tyre_types",
                column: "is_active",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "idx_tyre_types_code_unique",
                table: "tyre_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_weighing_axles_configuration",
                table: "weighing_axles",
                column: "axle_configuration_id");

            migrationBuilder.CreateIndex(
                name: "idx_weighing_axles_group",
                table: "weighing_axles",
                column: "axle_group_id");

            migrationBuilder.CreateIndex(
                name: "idx_weighing_axles_weighing",
                table: "weighing_axles",
                column: "weighing_id");

            migrationBuilder.CreateIndex(
                name: "idx_weighing_axles_weighing_axle_unique",
                table: "weighing_axles",
                columns: new[] { "weighing_id", "axle_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weighing_axles_axle_weight_reference_id",
                table: "weighing_axles",
                column: "axle_weight_reference_id");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_axles_tyre_type_id",
                table: "weighing_axles",
                column: "tyre_type_id");

            migrationBuilder.AddForeignKey(
                name: "FK_axle_configurations_users_created_by_user_id",
                table: "axle_configurations",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_axle_configurations_users_created_by_user_id",
                table: "axle_configurations");

            migrationBuilder.DropTable(
                name: "axle_fee_schedules");

            migrationBuilder.DropTable(
                name: "permit_types");

            migrationBuilder.DropTable(
                name: "tolerance_settings");

            migrationBuilder.DropTable(
                name: "weighing_axles");

            migrationBuilder.DropTable(
                name: "axle_weight_references");

            migrationBuilder.DropTable(
                name: "axle_groups");

            migrationBuilder.DropTable(
                name: "tyre_types");

            migrationBuilder.DropIndex(
                name: "idx_stations_code_alias",
                table: "stations");

            migrationBuilder.DropIndex(
                name: "idx_roles_code",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "idx_axle_configurations_active",
                table: "axle_configurations");

            migrationBuilder.DropIndex(
                name: "idx_axle_configurations_framework",
                table: "axle_configurations");

            migrationBuilder.DropIndex(
                name: "idx_axle_configurations_standard",
                table: "axle_configurations");

            migrationBuilder.DropIndex(
                name: "IX_axle_configurations_created_by_user_id",
                table: "axle_configurations");

            migrationBuilder.DropColumn(
                name: "code",
                table: "work_shifts");

            migrationBuilder.DropColumn(
                name: "shift_code",
                table: "work_shifts");

            migrationBuilder.DropColumn(
                name: "shift_name",
                table: "work_shifts");

            migrationBuilder.DropColumn(
                name: "end_time_str",
                table: "work_shift_schedules");

            migrationBuilder.DropColumn(
                name: "start_time_str",
                table: "work_shift_schedules");

            migrationBuilder.DropColumn(
                name: "station_name",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "status",
                table: "stations");

            migrationBuilder.DropColumn(
                name: "code",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "axle_name",
                table: "axle_configurations");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "axle_configurations");

            migrationBuilder.DropColumn(
                name: "is_standard",
                table: "axle_configurations");

            migrationBuilder.DropColumn(
                name: "legal_framework",
                table: "axle_configurations");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "axle_configurations");

            migrationBuilder.RenameColumn(
                name: "deleted_at",
                table: "work_shifts",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "work_shift_schedules",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "work_shift_schedules",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "location",
                table: "stations",
                newName: "Location");

            migrationBuilder.RenameColumn(
                name: "code",
                table: "stations",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "deleted_at",
                table: "stations",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "roles",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "roles",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "axle_number",
                table: "axle_configurations",
                newName: "total_axles");

            migrationBuilder.RenameColumn(
                name: "axle_code",
                table: "axle_configurations",
                newName: "code");

            migrationBuilder.RenameIndex(
                name: "idx_axle_configurations_code_unique",
                table: "axle_configurations",
                newName: "idx_axle_configs_code_unique");

            migrationBuilder.RenameIndex(
                name: "idx_axle_configurations_axle_number",
                table: "axle_configurations",
                newName: "idx_axle_configs_total_axles");

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "stations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "stations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "visual_diagram_url",
                table: "axle_configurations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "axle_configurations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "axle_limits",
                table: "axle_configurations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "axle_pattern",
                table: "axle_configurations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_axle_configs_is_active",
                table: "axle_configurations",
                column: "is_active");
        }
    }
}
