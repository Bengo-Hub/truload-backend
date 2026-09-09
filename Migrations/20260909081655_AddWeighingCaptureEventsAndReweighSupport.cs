using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWeighingCaptureEventsAndReweighSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastWeightCapturedAt",
                schema: "weighing",
                table: "weighing_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "weighing_capture_events",
                schema: "weighing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    weighing_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    reweigh_no = table.Column<int>(type: "integer", nullable: true),
                    weight_kg = table.Column<int>(type: "integer", nullable: false),
                    weight_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    captured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    capture_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_manual_entry = table.Column<bool>(type: "boolean", nullable: false),
                    manual_entry_justification = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    deck_readings = table.Column<string>(type: "jsonb", nullable: true),
                    captured_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_finalizing_event = table.Column<bool>(type: "boolean", nullable: false),
                    reweigh_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weighing_capture_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_weighing_capture_events_asp_net_users_captured_by_user_id",
                        column: x => x.captured_by_user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_weighing_capture_events_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_weighing_capture_events_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_weighing_capture_events_weighing_transactions_weighing_tran~",
                        columns: x => new { x.weighing_transaction_id, x.organization_id },
                        principalSchema: "weighing",
                        principalTable: "weighing_transactions",
                        principalColumns: new[] { "id", "organization_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_weighing_capture_events_captured_by_user_id",
                schema: "weighing",
                table: "weighing_capture_events",
                column: "captured_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_capture_events_organization_id",
                schema: "weighing",
                table: "weighing_capture_events",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_capture_events_station_id",
                schema: "weighing",
                table: "weighing_capture_events",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "IX_weighing_capture_events_weighing_transaction_id_organizatio~",
                schema: "weighing",
                table: "weighing_capture_events",
                columns: new[] { "weighing_transaction_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_weighing_capture_events_weighing_transaction_id_sequence_no",
                schema: "weighing",
                table: "weighing_capture_events",
                columns: new[] { "weighing_transaction_id", "sequence_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "weighing_capture_events",
                schema: "weighing");

            migrationBuilder.DropColumn(
                name: "LastWeightCapturedAt",
                schema: "weighing",
                table: "weighing_transactions");
        }
    }
}
