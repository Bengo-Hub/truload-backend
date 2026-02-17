using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWeighedByUserNavProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_weighing_transactions_asp_net_users_weighed_by_user_id",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.AddForeignKey(
                name: "FK_weighing_transactions_asp_net_users_weighed_by_user_id",
                schema: "weighing",
                table: "weighing_transactions",
                column: "weighed_by_user_id",
                principalTable: "asp_net_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_weighing_transactions_asp_net_users_weighed_by_user_id",
                schema: "weighing",
                table: "weighing_transactions");

            migrationBuilder.AddForeignKey(
                name: "FK_weighing_transactions_asp_net_users_weighed_by_user_id",
                schema: "weighing",
                table: "weighing_transactions",
                column: "weighed_by_user_id",
                principalTable: "asp_net_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
