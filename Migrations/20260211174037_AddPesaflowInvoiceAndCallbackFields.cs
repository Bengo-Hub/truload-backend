using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPesaflowInvoiceAndCallbackFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pesaflow_checkout_url",
                table: "invoices");

            migrationBuilder.AddColumn<decimal>(
                name: "pesaflow_amount_net",
                table: "invoices",
                type: "numeric(18,2)",
                nullable: true,
                comment: "Original invoice amount before gateway fees");

            migrationBuilder.AddColumn<decimal>(
                name: "pesaflow_gateway_fee",
                table: "invoices",
                type: "numeric(18,2)",
                nullable: true,
                comment: "Commission charged by Pesaflow gateway");

            migrationBuilder.AddColumn<string>(
                name: "pesaflow_payment_link",
                table: "invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Customer payment URL from Pesaflow iframe response");

            migrationBuilder.AddColumn<string>(
                name: "pesaflow_sync_status",
                table: "invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                comment: "Sync status: null (not applicable), pending, synced, failed");

            migrationBuilder.AddColumn<decimal>(
                name: "pesaflow_total_amount",
                table: "invoices",
                type: "numeric(18,2)",
                nullable: true,
                comment: "Total amount including gateway fees (amount_expected from Pesaflow)");

            migrationBuilder.AlterColumn<string>(
                name: "callback_url",
                table: "integration_configs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Success callback URL",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "callback_failure_url",
                table: "integration_configs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Failure callback URL for payment failures");

            migrationBuilder.AddColumn<string>(
                name: "callback_timeout_url",
                table: "integration_configs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Timeout callback URL for payment timeouts");

            migrationBuilder.AddColumn<string>(
                name: "payment_confirmation_endpoint",
                table: "integration_configs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Endpoint for manual payment confirmation/reconciliation");

            migrationBuilder.AddColumn<string>(
                name: "payment_polling_endpoint",
                table: "integration_configs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Endpoint for polling payment status as fallback");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pesaflow_amount_net",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "pesaflow_gateway_fee",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "pesaflow_payment_link",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "pesaflow_sync_status",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "pesaflow_total_amount",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "callback_failure_url",
                table: "integration_configs");

            migrationBuilder.DropColumn(
                name: "callback_timeout_url",
                table: "integration_configs");

            migrationBuilder.DropColumn(
                name: "payment_confirmation_endpoint",
                table: "integration_configs");

            migrationBuilder.DropColumn(
                name: "payment_polling_endpoint",
                table: "integration_configs");

            migrationBuilder.AddColumn<string>(
                name: "pesaflow_checkout_url",
                table: "invoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "callback_url",
                table: "integration_configs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "Success callback URL");
        }
    }
}
