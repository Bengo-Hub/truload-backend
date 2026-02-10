using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.Financial;

namespace TruLoad.Backend.Data.Configurations.Financial;

/// <summary>
/// Financial Module DbContext Configuration
/// Contains configurations for invoices and receipts with idempotency support
/// </summary>
public static class FinancialModuleDbContextConfiguration
{
    /// <summary>
    /// Applies financial module configurations to the model builder
    /// </summary>
    public static ModelBuilder ApplyFinancialConfigurations(this ModelBuilder modelBuilder)
    {
        // ===== Invoice Entity Configuration =====
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("invoices");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.InvoiceNo)
                .HasColumnName("invoice_no")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.CaseRegisterId)
                .HasColumnName("case_register_id");

            entity.Property(e => e.ProsecutionCaseId)
                .HasColumnName("prosecution_case_id");

            entity.Property(e => e.WeighingId)
                .HasColumnName("weighing_id");

            entity.Property(e => e.AmountDue)
                .HasColumnName("amount_due")
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(e => e.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .HasDefaultValue("USD");

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasDefaultValue("pending");

            entity.Property(e => e.GeneratedAt)
                .HasColumnName("generated_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.DueDate)
                .HasColumnName("due_date");

            entity.Property(e => e.PesaflowInvoiceNumber)
                .HasColumnName("pesaflow_invoice_number")
                .HasMaxLength(100);

            entity.Property(e => e.PesaflowPaymentReference)
                .HasColumnName("pesaflow_payment_reference")
                .HasMaxLength(100);

            entity.Property(e => e.PesaflowCheckoutUrl)
                .HasColumnName("pesaflow_checkout_url")
                .HasMaxLength(500);

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            // Relationships
            entity.HasOne(e => e.CaseRegister)
                .WithMany()
                .HasForeignKey(e => e.CaseRegisterId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ProsecutionCase)
                .WithMany(pc => pc.Invoices)
                .HasForeignKey(e => e.ProsecutionCaseId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Weighing)
                .WithMany()
                .HasForeignKey(e => e.WeighingId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            entity.HasIndex(e => e.InvoiceNo)
                .IsUnique()
                .HasDatabaseName("idx_invoices_invoice_no");

            entity.HasIndex(e => e.CaseRegisterId)
                .HasDatabaseName("idx_invoices_case_register_id");

            entity.HasIndex(e => e.ProsecutionCaseId)
                .HasDatabaseName("idx_invoices_prosecution_case_id");

            entity.HasIndex(e => e.WeighingId)
                .HasDatabaseName("idx_invoices_weighing_id");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("idx_invoices_status");

            entity.HasIndex(e => e.GeneratedAt)
                .HasDatabaseName("idx_invoices_generated_at");

            entity.HasIndex(e => e.DueDate)
                .HasDatabaseName("idx_invoices_due_date");

            entity.HasIndex(e => e.PesaflowInvoiceNumber)
                .HasDatabaseName("idx_invoices_pesaflow_invoice_no")
                .HasFilter("pesaflow_invoice_number IS NOT NULL");

            // CHECK constraints
            entity.HasCheckConstraint("chk_invoice_status",
                "status IN ('pending', 'paid', 'cancelled', 'void')");

            entity.HasCheckConstraint("chk_invoice_currency",
                "currency IN ('USD', 'KES', 'UGX', 'TZS')");

            entity.HasCheckConstraint("chk_invoice_amount",
                "amount_due >= 0");
        });

        // ===== Receipt Entity Configuration =====
        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.ToTable("receipts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.ReceiptNo)
                .HasColumnName("receipt_no")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.InvoiceId)
                .HasColumnName("invoice_id")
                .IsRequired();

            entity.Property(e => e.AmountPaid)
                .HasColumnName("amount_paid")
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(e => e.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .HasDefaultValue("USD");

            entity.Property(e => e.PaymentMethod)
                .HasColumnName("payment_method")
                .HasMaxLength(50)
                .HasDefaultValue("cash");

            entity.Property(e => e.TransactionReference)
                .HasColumnName("transaction_reference")
                .HasMaxLength(100);

            entity.Property(e => e.IdempotencyKey)
                .HasColumnName("idempotency_key")
                .IsRequired();

            entity.Property(e => e.ReceivedById)
                .HasColumnName("received_by_id");

            entity.Property(e => e.PaymentDate)
                .HasColumnName("payment_date")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.PaymentChannel)
                .HasColumnName("payment_channel")
                .HasMaxLength(50);

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            // Relationships
            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.Receipts)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ReceivedBy)
                .WithMany()
                .HasForeignKey(e => e.ReceivedById)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            entity.HasIndex(e => e.ReceiptNo)
                .IsUnique()
                .HasDatabaseName("idx_receipts_receipt_no");

            entity.HasIndex(e => e.InvoiceId)
                .HasDatabaseName("idx_receipts_invoice_id");

            entity.HasIndex(e => e.TransactionReference)
                .IsUnique()
                .HasDatabaseName("idx_receipts_transaction_ref");

            entity.HasIndex(e => e.IdempotencyKey)
                .IsUnique()
                .HasDatabaseName("idx_receipts_idempotency_key");

            entity.HasIndex(e => e.PaymentDate)
                .HasDatabaseName("idx_receipts_payment_date");

            // CHECK constraints
            entity.HasCheckConstraint("chk_receipt_payment_method",
                "payment_method IN ('cash', 'mobile_money', 'bank_transfer', 'card', 'pesaflow')");

            entity.HasCheckConstraint("chk_receipt_currency",
                "currency IN ('USD', 'KES', 'UGX', 'TZS')");

            entity.HasCheckConstraint("chk_receipt_amount",
                "amount_paid > 0");
        });

        return modelBuilder;
    }
}
