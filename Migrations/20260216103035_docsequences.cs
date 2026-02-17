using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class docsequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_conventions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IncludeStationCode = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeBound = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeDate = table.Column<bool>(type: "boolean", nullable: false),
                    DateFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IncludeVehicleReg = table.Column<bool>(type: "boolean", nullable: false),
                    SequencePadding = table.Column<int>(type: "integer", nullable: false),
                    Separator = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    ResetFrequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_conventions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_conventions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_sequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrentSequence = table.Column<int>(type: "integer", nullable: false),
                    ResetFrequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastResetDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_sequences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_sequences_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_sequences_stations_StationId",
                        column: x => x.StationId,
                        principalTable: "stations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_weighing_transactions_weighed_by_user_id",
                schema: "weighing",
                table: "weighing_transactions",
                column: "weighed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_conventions_OrganizationId",
                table: "document_conventions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_document_sequences_OrganizationId",
                table: "document_sequences",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_document_sequences_StationId",
                table: "document_sequences",
                column: "StationId");

            migrationBuilder.AddForeignKey(
                name: "FK_weighing_transactions_asp_net_users_weighed_by_user_id",
                schema: "weighing",
                table: "weighing_transactions",
                column: "weighed_by_user_id",
                principalTable: "asp_net_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_weighing_transactions_asp_net_users_weighed_by_user_id",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.DropTable(
                name: "document_conventions");

            migrationBuilder.DropTable(
                name: "document_sequences");

            migrationBuilder.DropIndex(
                name: "IX_weighing_transactions_weighed_by_user_id",
                schema: "weighing",
                table: "weighing_transactions");
        }
    }
}
