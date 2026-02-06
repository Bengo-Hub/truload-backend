using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Models.Financial;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Invoice Document - Official payment invoice for overload charges
/// Supports multiple currencies (USD/KES) with exchange rate display
/// </summary>
public class InvoiceDocument : BaseDocument
{
    private readonly Invoice _invoice;
    private readonly string? _organizationName;
    private readonly string? _organizationAddress;

    public InvoiceDocument(Invoice invoice, string? organizationName = null, string? organizationAddress = null)
    {
        _invoice = invoice;
        _organizationName = organizationName ?? "Kenya Roads Authority (KURA)";
        _organizationAddress = organizationAddress ?? "P.O. Box 00100-1234, Nairobi, Kenya";
    }

    public override byte[] Generate()
    {
        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(10).Element(ComposeContent);
                page.Footer().Element(ComposeOfficialFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(org =>
                {
                    org.Item().Text(_organizationName).FontSize(14).SemiBold().FontColor(KuraBlue);
                    org.Item().Text(_organizationAddress).FontSize(9);
                    org.Item().Text("Tel: +254 20 XXXXXXX | Email: info@kura.go.ke").FontSize(8);
                });

                row.ConstantItem(120).AlignRight().Column(inv =>
                {
                    inv.Item().Background(KuraBlue).Padding(8).AlignCenter()
                        .Text("INVOICE").FontSize(16).Bold().FontColor(Colors.White);
                });
            });

            col.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Invoice No: {_invoice.InvoiceNo}").FontSize(11).SemiBold();
                    c.Item().Text($"Case No: {_invoice.ProsecutionCase?.CaseRegister?.CaseNo ?? "N/A"}").FontSize(10);
                    c.Item().Text($"Certificate No: {_invoice.ProsecutionCase?.CertificateNo ?? "N/A"}").FontSize(10);
                });
                row.ConstantItem(150).AlignRight().Column(c =>
                {
                    c.Item().Text($"Date: {_invoice.GeneratedAt:dd/MM/yyyy}").FontSize(10);
                    c.Item().Text($"Due Date: {_invoice.DueDate:dd/MM/yyyy}").FontSize(10).SemiBold().FontColor(GetDueDateColor());
                    c.Item().Text($"Status: {GetStatusDisplay()}").FontSize(10).SemiBold().FontColor(GetStatusColor());
                });
            });

            col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(10);

            // Vehicle Details Section
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(billTo =>
                {
                    billTo.Item().Text("VEHICLE DETAILS:").FontSize(10).SemiBold();
                    billTo.Item().PaddingLeft(10).Column(c =>
                    {
                        c.Item().Text($"Reg No: {_invoice.ProsecutionCase?.Weighing?.VehicleRegNumber ?? "N/A"}").SemiBold();
                        c.Item().Text($"Ticket No: {_invoice.ProsecutionCase?.Weighing?.TicketNumber ?? "N/A"}");
                        c.Item().Text($"Weighed At: {_invoice.ProsecutionCase?.Weighing?.WeighedAt:dd/MM/yyyy HH:mm}");
                    });
                });
            });

            // Charge Details Table
            col.Item().PaddingTop(10).Text("CHARGE DETAILS").FontSize(11).SemiBold();
            col.Item().Element(ComposeChargeTable);

            // Payment Summary
            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem(); // Spacer
                row.ConstantItem(250).Element(ComposePaymentSummary);
            });

            // Payment Instructions
            col.Item().PaddingTop(15).Element(ComposePaymentInstructions);

            // Terms and Conditions
            col.Item().PaddingTop(15).Element(ComposeTermsAndConditions);

            // Signature
            col.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Issuing Officer"));
            });
        });
    }

    private void ComposeChargeTable(IContainer container)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            // Header
            table.Header(header =>
            {
                header.Cell().Element(HeaderStyle).Text("Description");
                header.Cell().Element(HeaderStyle).AlignRight().Text("Qty/Kg");
                header.Cell().Element(HeaderStyle).AlignRight().Text("Amount (USD)");
                header.Cell().Element(HeaderStyle).AlignRight().Text("Amount (KES)");

                static IContainer HeaderStyle(IContainer c) =>
                    c.DefaultTextStyle(x => x.SemiBold().FontSize(9).FontColor(Colors.White))
                     .Background(Colors.Grey.Darken2)
                     .PaddingVertical(5)
                     .PaddingHorizontal(5);
            });

            var prosecution = _invoice.ProsecutionCase;
            if (prosecution != null)
            {
                // GVW Overload Charge
                if (prosecution.GvwOverloadKg > 0)
                {
                    table.Cell().Element(CellStyle).Text("GVW Overload Charge");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.GvwOverloadKg:N0}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.GvwFeeUsd:N2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.GvwFeeKes:N2}");
                }

                // Axle Overload Charge
                if (prosecution.MaxAxleOverloadKg > 0)
                {
                    table.Cell().Element(CellStyle).Text("Axle Overload Charge");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.MaxAxleOverloadKg:N0}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.MaxAxleFeeUsd:N2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.MaxAxleFeeKes:N2}");
                }

                // Best Charge Basis Note
                var basisLabel = prosecution.BestChargeBasis == "gvw" ? "GVW" : "Axle";
                table.Cell().ColumnSpan(4).Element(NoteStyle).Text($"* Charge applied based on {basisLabel} (higher of the two)").FontSize(8).Italic();

                // Penalty Multiplier
                if (prosecution.PenaltyMultiplier > 1)
                {
                    table.Cell().Element(CellStyle).Text($"Repeat Offender Penalty ({prosecution.PenaltyMultiplier:N1}x)").FontColor(OfficialRed);
                    table.Cell().Element(CellStyle).Text("");
                    table.Cell().Element(CellStyle).Text("");
                    table.Cell().Element(CellStyle).Text("Applied");
                }
            }

            static IContainer CellStyle(IContainer c) =>
                c.PaddingVertical(4)
                 .PaddingHorizontal(5)
                 .BorderBottom(0.5f)
                 .BorderColor(Colors.Grey.Lighten2)
                 .DefaultTextStyle(t => t.FontSize(9));

            static IContainer NoteStyle(IContainer c) =>
                c.PaddingVertical(3)
                 .PaddingHorizontal(5)
                 .Background(Colors.Grey.Lighten4)
                 .DefaultTextStyle(t => t.FontSize(8));
        });
    }

    private void ComposePaymentSummary(IContainer container)
    {
        container.Border(1).BorderColor(Colors.Black).Column(col =>
        {
            col.Item().Background(Colors.Grey.Lighten4).Padding(5).Row(r =>
            {
                r.RelativeItem().Text("Subtotal (USD):").SemiBold();
                r.ConstantItem(80).AlignRight().Text($"${_invoice.AmountDue:N2}");
            });

            col.Item().Padding(5).Row(r =>
            {
                r.RelativeItem().Text($"Exchange Rate (1 USD):").FontSize(9);
                r.ConstantItem(80).AlignRight().Text($"KES {_invoice.ProsecutionCase?.ForexRate:N2}").FontSize(9);
            });

            col.Item().Padding(5).Row(r =>
            {
                r.RelativeItem().Text("Amount Due (KES):").SemiBold();
                r.ConstantItem(80).AlignRight().Text($"KES {_invoice.AmountDue * (_invoice.ProsecutionCase?.ForexRate ?? 130m):N2}").SemiBold();
            });

            // If partial payment made
            var amountPaid = _invoice.Receipts?.Where(r => r.DeletedAt == null).Sum(r => r.AmountPaid) ?? 0;
            if (amountPaid > 0)
            {
                col.Item().Background(Colors.Green.Lighten4).Padding(5).Row(r =>
                {
                    r.RelativeItem().Text("Amount Paid:").FontColor(Colors.Green.Darken2);
                    r.ConstantItem(80).AlignRight().Text($"${amountPaid:N2}").FontColor(Colors.Green.Darken2);
                });

                var balance = _invoice.AmountDue - amountPaid;
                col.Item().Background(balance > 0 ? Colors.Orange.Lighten4 : Colors.Green.Lighten3).Padding(5).Row(r =>
                {
                    r.RelativeItem().Text("BALANCE DUE:").Bold();
                    r.ConstantItem(80).AlignRight().Text($"${balance:N2}").Bold();
                });
            }
            else
            {
                col.Item().Background(Colors.Blue.Lighten4).Padding(5).Row(r =>
                {
                    r.RelativeItem().Text("TOTAL DUE (USD):").Bold().FontSize(11);
                    r.ConstantItem(80).AlignRight().Text($"${_invoice.AmountDue:N2}").Bold().FontSize(11);
                });
            }
        });
    }

    private void ComposePaymentInstructions(IContainer container)
    {
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
        {
            col.Item().Text("PAYMENT INSTRUCTIONS").FontSize(10).SemiBold().FontColor(KuraBlue);
            col.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Column(bank =>
                {
                    bank.Item().Text("Bank Transfer:").FontSize(9).SemiBold();
                    bank.Item().Text("Bank: Kenya Commercial Bank").FontSize(9);
                    bank.Item().Text("Account: 1234567890").FontSize(9);
                    bank.Item().Text("Branch: Nairobi Main").FontSize(9);
                    bank.Item().Text($"Reference: {_invoice.InvoiceNo}").FontSize(9).SemiBold();
                });
                row.RelativeItem().Column(mpesa =>
                {
                    mpesa.Item().Text("M-Pesa Paybill:").FontSize(9).SemiBold();
                    mpesa.Item().Text("Business No: 123456").FontSize(9);
                    mpesa.Item().Text($"Account: {_invoice.InvoiceNo}").FontSize(9);
                    mpesa.Item().PaddingTop(3).Text("Till No: 654321").FontSize(9).SemiBold();
                });
            });
        });
    }

    private void ComposeTermsAndConditions(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Text("TERMS AND CONDITIONS:").FontSize(9).SemiBold();
            col.Item().PaddingLeft(10).Text("1. Payment is due within 14 days from the invoice date.").FontSize(8);
            col.Item().PaddingLeft(10).Text("2. Failure to pay may result in additional penalties and legal action.").FontSize(8);
            col.Item().PaddingLeft(10).Text("3. Quote the Invoice Number on all payments and correspondence.").FontSize(8);
            col.Item().PaddingLeft(10).Text("4. This invoice is subject to Kenya Traffic Act Cap 403 regulations.").FontSize(8);
        });
    }

    private string GetStatusDisplay()
    {
        return _invoice.Status switch
        {
            "pending" => "PENDING",
            "paid" => "PAID",
            "partial" => "PARTIAL PAYMENT",
            "void" => "VOIDED",
            "cancelled" => "CANCELLED",
            _ => _invoice.Status.ToUpper()
        };
    }

    private string GetStatusColor()
    {
        return _invoice.Status switch
        {
            "pending" => Colors.Orange.Medium,
            "paid" => Colors.Green.Medium,
            "partial" => Colors.Blue.Medium,
            "void" => Colors.Grey.Medium,
            "cancelled" => Colors.Red.Medium,
            _ => Colors.Black
        };
    }

    private string GetDueDateColor()
    {
        if (_invoice.Status == "paid") return Colors.Green.Medium;
        if (_invoice.DueDate < DateTime.UtcNow) return Colors.Red.Medium;
        if (_invoice.DueDate < DateTime.UtcNow.AddDays(3)) return Colors.Orange.Medium;
        return Colors.Black;
    }
}
