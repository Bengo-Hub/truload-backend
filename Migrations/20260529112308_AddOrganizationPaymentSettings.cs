using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationPaymentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentBankAccountNumber",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentBankBranch",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentBankName",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMpesaPaybillNumber",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMpesaTillNumber",
                table: "organizations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentBankAccountNumber",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "PaymentBankBranch",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "PaymentBankName",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "PaymentMpesaPaybillNumber",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "PaymentMpesaTillNumber",
                table: "organizations");
        }
    }
}
