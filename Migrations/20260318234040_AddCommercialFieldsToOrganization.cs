using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialFieldsToOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommercialWeighingFeeKes",
                table: "organizations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentGateway",
                table: "organizations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SsoTenantSlug",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceType",
                table: "invoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TreasuryIntentId",
                table: "invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TreasuryIntentStatus",
                table: "invoices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommercialWeighingFeeKes",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "PaymentGateway",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "SsoTenantSlug",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "InvoiceType",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TreasuryIntentId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "TreasuryIntentStatus",
                table: "invoices");
        }
    }
}
