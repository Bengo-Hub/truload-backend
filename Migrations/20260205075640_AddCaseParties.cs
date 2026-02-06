using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "case_parties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    case_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "defendant_driver"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vehicle_owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transporter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    external_id_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    external_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_currently_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    removed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_parties", x => x.id);
                    table.CheckConstraint("chk_case_party_role", "party_role IN ('investigating_officer', 'ocs', 'arresting_officer', 'prosecutor', 'defendant_driver', 'defendant_owner', 'defendant_transporter', 'witness', 'complainant')");
                    table.ForeignKey(
                        name: "FK_case_parties_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_case_parties_case_registers_case_register_id",
                        column: x => x.case_register_id,
                        principalTable: "case_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_case_parties_drivers_driver_id",
                        column: x => x.driver_id,
                        principalSchema: "weighing",
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_case_parties_transporters_transporter_id",
                        column: x => x.transporter_id,
                        principalSchema: "weighing",
                        principalTable: "transporters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_case_parties_vehicle_owners_vehicle_owner_id",
                        column: x => x.vehicle_owner_id,
                        principalSchema: "weighing",
                        principalTable: "vehicle_owners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_case_parties_active",
                table: "case_parties",
                column: "is_currently_active");

            migrationBuilder.CreateIndex(
                name: "idx_case_parties_case_id",
                table: "case_parties",
                column: "case_register_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_parties_case_role_active",
                table: "case_parties",
                columns: new[] { "case_register_id", "party_role", "is_currently_active" });

            migrationBuilder.CreateIndex(
                name: "idx_case_parties_driver_id",
                table: "case_parties",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_parties_role",
                table: "case_parties",
                column: "party_role");

            migrationBuilder.CreateIndex(
                name: "idx_case_parties_user_id",
                table: "case_parties",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_parties_transporter_id",
                table: "case_parties",
                column: "transporter_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_parties_vehicle_owner_id",
                table: "case_parties",
                column: "vehicle_owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_parties");
        }
    }
}
