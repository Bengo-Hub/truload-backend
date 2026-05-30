using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationAddressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Address",
                table: "organizations",
                newName: "address");

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "organizations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country",
                table: "organizations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "po_box",
                table: "organizations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "street_address",
                table: "organizations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "website",
                table: "organizations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "city",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "country",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "po_box",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "street_address",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "website",
                table: "organizations");

            migrationBuilder.RenameColumn(
                name: "address",
                table: "organizations",
                newName: "Address");
        }
    }
}
