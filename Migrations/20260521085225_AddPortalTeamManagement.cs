using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPortalTeamManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "portal_team_invitations",
                schema: "weighing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    transporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_team_invitations", x => x.id);
                    table.ForeignKey(
                        name: "FK_portal_team_invitations_transporters_transporter_id",
                        column: x => x.transporter_id,
                        principalSchema: "weighing",
                        principalTable: "transporters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "portal_team_memberships",
                schema: "weighing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    transporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    user_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "viewer"),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_team_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_portal_team_memberships_transporters_transporter_id",
                        column: x => x.transporter_id,
                        principalSchema: "weighing",
                        principalTable: "transporters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_portal_team_invitations_invited_email",
                schema: "weighing",
                table: "portal_team_invitations",
                column: "invited_email");

            migrationBuilder.CreateIndex(
                name: "IX_portal_team_invitations_token",
                schema: "weighing",
                table: "portal_team_invitations",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_portal_team_invitations_transporter_id",
                schema: "weighing",
                table: "portal_team_invitations",
                column: "transporter_id");

            migrationBuilder.CreateIndex(
                name: "IX_portal_team_memberships_transporter_user_active",
                schema: "weighing",
                table: "portal_team_memberships",
                columns: new[] { "transporter_id", "user_id" },
                unique: true,
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "IX_portal_team_memberships_user_id",
                schema: "weighing",
                table: "portal_team_memberships",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portal_team_invitations",
                schema: "weighing");

            migrationBuilder.DropTable(
                name: "portal_team_memberships",
                schema: "weighing");
        }
    }
}
