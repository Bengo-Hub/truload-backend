using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDocumentSequenceRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "document_sequences");

            migrationBuilder.CreateTable(
                name: "ActivePermits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PermitId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermitNo = table.Column<string>(type: "text", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegNo = table.Column<string>(type: "text", nullable: false),
                    Make = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    PermitTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermitType = table.Column<string>(type: "text", nullable: false),
                    PermitTypeDescription = table.Column<string>(type: "text", nullable: true),
                    GvwExtensionKg = table.Column<decimal>(type: "numeric", nullable: false),
                    AxleExtensionKg = table.Column<decimal>(type: "numeric", nullable: false),
                    IssuingAuthority = table.Column<string>(type: "text", nullable: true),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DaysUntilExpiry = table.Column<double>(type: "double precision", nullable: false),
                    IsExpiringSoon = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivePermits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivePermits_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivePermits_stations_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivePermits_organization_id",
                table: "ActivePermits",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_ActivePermits_station_id",
                table: "ActivePermits",
                column: "station_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivePermits");

            migrationBuilder.RenameColumn(
                name: "xmin",
                table: "document_sequences",
                newName: "RowVersion");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "document_sequences",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);
        }
    }
}
