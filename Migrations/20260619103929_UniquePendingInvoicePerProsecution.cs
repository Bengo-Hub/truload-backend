using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruLoad.Backend.Migrations
{
    /// <inheritdoc />
    public partial class UniquePendingInvoicePerProsecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Self-heal before adding the unique partial index: if any prosecution case already
            // has more than one live pending invoice (the "double posting" bug this index prevents),
            // void all but the EARLIEST so the unique index can be created. Without this, the startup
            // migration would fail on any DB that still holds duplicates (test truload + live kuraweigh).
            migrationBuilder.Sql(@"
                WITH ranked AS (
                    SELECT id,
                           ROW_NUMBER() OVER (
                               PARTITION BY prosecution_case_id
                               ORDER BY generated_at ASC, created_at ASC, id ASC
                           ) AS rn
                    FROM invoices
                    WHERE status = 'pending'
                      AND deleted_at IS NULL
                      AND prosecution_case_id IS NOT NULL
                )
                UPDATE invoices i
                SET status = 'void',
                    updated_at = NOW()
                FROM ranked r
                WHERE i.id = r.id
                  AND r.rn > 1;
            ");

            migrationBuilder.CreateIndex(
                name: "uq_invoices_pending_per_prosecution",
                table: "invoices",
                column: "prosecution_case_id",
                unique: true,
                filter: "status = 'pending' AND deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_invoices_pending_per_prosecution",
                table: "invoices");
        }
    }
}
