using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddScopedCodeUniqueIndexesToCargoTypesAndOriginsDestinations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_origins_destinations_code",
                table: "origins_destinations");

            migrationBuilder.DropIndex(
                name: "IX_cargo_types_code",
                table: "cargo_types");

            migrationBuilder.CreateIndex(
                name: "IX_origins_destinations_code_shared",
                table: "origins_destinations",
                column: "code",
                unique: true,
                filter: "organization_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_origins_destinations_organization_id_code",
                table: "origins_destinations",
                columns: new[] { "organization_id", "code" },
                unique: true,
                filter: "organization_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_types_code_shared",
                table: "cargo_types",
                column: "code",
                unique: true,
                filter: "organization_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cargo_types_organization_id_code",
                table: "cargo_types",
                columns: new[] { "organization_id", "code" },
                unique: true,
                filter: "organization_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_origins_destinations_code_shared",
                table: "origins_destinations");

            migrationBuilder.DropIndex(
                name: "IX_origins_destinations_organization_id_code",
                table: "origins_destinations");

            migrationBuilder.DropIndex(
                name: "IX_cargo_types_code_shared",
                table: "cargo_types");

            migrationBuilder.DropIndex(
                name: "IX_cargo_types_organization_id_code",
                table: "cargo_types");

            migrationBuilder.CreateIndex(
                name: "IX_origins_destinations_code",
                table: "origins_destinations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cargo_types_code",
                table: "cargo_types",
                column: "code",
                unique: true);
        }
    }
}
