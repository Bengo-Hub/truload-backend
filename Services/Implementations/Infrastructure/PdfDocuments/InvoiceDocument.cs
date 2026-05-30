using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;
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
    private readonly string? _organizationContact;
    private readonly string _orgLogoFile;
    private readonly bool _showSecondaryLogo;
    private readonly OrgPaymentConfig? _payment;

    public record OrgPaymentConfig(
        string? BankName,
        string? BankBranch,
        string? BankAccountNumber,
        string? MpesaPaybillNumber,
        string? MpesaTillNumber);

    public InvoiceDocument(Invoice invoice, string? organizationName = null, string? organizationAddress = null, string? organizationContact = null, string? orgLogoFile = null, bool showSecondaryLogo = true, OrgPaymentConfig? paymentConfig = null)
    {
        _invoice = invoice;
        _organizationName = organizationName;
        _organizationAddress = organizationAddress;
        _organizationContact = organizationContact;
        _orgLogoFile = ResolveOrgLogo(orgLogoFile);
        _showSecondaryLogo = showSecondaryLogo;
        _payment = paymentConfig;
    }

    public override byte[] Generate()
    {
        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0.7f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9f).FontFamily("Inter"));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(4).Element(ComposeContent);
                page.Footer().Element(ComposeOfficialFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        var primaryLogo = LoadLogo(_orgLogoFile);
        var secondaryLogo = _showSecondaryLogo ? LoadLogo(BrandingConstants.Logos.EcitizenLogo) : null;

        container.Column(col =>
        {
            AddOrgBrandingRow(col, primaryLogo, secondaryLogo, _organizationName, _organizationAddress, _organizationContact);

            // Invoice badge
            col.Item().PaddingTop(4).AlignCenter()
                .Background(KuraBlue).Padding(4)
                .Text("INVOICE").FontSize(13).Bold().FontColor(Colors.White);

            // Invoice details row
            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Invoice No: {_invoice.InvoiceNo}").FontSize(10).SemiBold();
                    c.Item().Text($"Case No: {_invoice.ProsecutionCase?.CaseRegister?.CaseNo ?? "N/A"}").FontSize(9);
                    c.Item().Text($"Certificate No: {_invoice.ProsecutionCase?.CertificateNo ?? "N/A"}").FontSize(9);
                });
                row.ConstantItem(150).AlignRight().Column(c =>
                {
                    c.Item().Text($"Date: {_invoice.GeneratedAt:dd/MM/yyyy}").FontSize(9);
                    c.Item().Text($"Due Date: {_invoice.DueDate:dd/MM/yyyy}").FontSize(9).SemiBold().FontColor(GetDueDateColor());
                    c.Item().Text($"Status: {GetStatusDisplay()}").FontSize(9).SemiBold().FontColor(GetStatusColor());
                });
            });

            col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(6);

            // Vehicle Details Section
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(billTo =>
                {
                    billTo.Item().Text("VEHICLE DETAILS:").FontSize(9).SemiBold();
                    billTo.Item().PaddingLeft(10).Column(c =>
                    {
                        c.Item().Text($"Reg No: {_invoice.ProsecutionCase?.Weighing?.VehicleRegNumber ?? "N/A"}").SemiBold();
                        c.Item().Text($"Ticket No: {_invoice.ProsecutionCase?.Weighing?.TicketNumber ?? "N/A"}");
                        c.Item().Text($"Weighed At: {_invoice.ProsecutionCase?.Weighing?.WeighedAt:dd/MM/yyyy HH:mm}");
                    });
                });
            });

            // Charge Details Table
            col.Item().Text("CHARGE DETAILS").FontSize(10).SemiBold();
            col.Item().Element(ComposeChargeTable);

            // Payment Summary
            col.Item().Row(row =>
            {
                row.RelativeItem(); // Spacer
                row.ConstantItem(250).Element(ComposePaymentSummary);
            });

            // Payment Instructions
            col.Item().PaddingTop(4).Element(ComposePaymentInstructions);

            // Terms and Conditions
            col.Item().PaddingTop(4).Element(ComposeTermsAndConditions);

            // Signature
            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Issuing Officer"));
            });
        });
    }

    private void ComposeChargeTable(IContainer container)
    {
        var prosecution = _invoice.ProsecutionCase;
        var isTrafficAct = string.Equals(prosecution?.Act?.ChargingCurrency, "KES", StringComparison.OrdinalIgnoreCase)
                        || prosecution?.Act == null;
        var perPartyKes = (prosecution?.TotalFeeKes ?? 0) / 2;
        var perPartyUsd = (prosecution?.TotalFeeUsd ?? 0) / 2;
        var basisLabel = prosecution?.BestChargeBasis == "gvw" ? "GVW" : "Axle";

        container.Column(col =>
        {
            col.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Table(table =>
            {
                if (isTrafficAct)
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderStyle).Text("Description");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("Qty/Kg");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("Amount (KES)");
                        static IContainer HeaderStyle(IContainer c) =>
                            c.DefaultTextStyle(x => x.SemiBold().FontSize(8.5f).FontColor(Colors.White))
                             .Background(Colors.Grey.Darken2).PaddingVertical(4).PaddingHorizontal(5);
                    });

                    if (prosecution != null)
                    {
                        if (prosecution.GvwOverloadKg > 0)
                        {
                            table.Cell().Element(CellStyle).Text("GVW Overload Charge");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.GvwOverloadKg:N0}");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.GvwFeeKes:N2}");
                        }
                        if (prosecution.MaxAxleOverloadKg > 0)
                        {
                            table.Cell().Element(CellStyle).Text("Axle Overload Charge");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.MaxAxleOverloadKg:N0}");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.MaxAxleFeeKes:N2}");
                        }

                        table.Cell().ColumnSpan(3).Element(NoteStyle)
                            .Text($"* Charge applied based on {basisLabel} (higher of the two)").FontSize(8).Italic();

                        if (prosecution.PenaltyMultiplier > 1)
                        {
                            table.Cell().Element(CellStyle).Text($"Repeat Offender Penalty ({prosecution.PenaltyMultiplier:N1}x)").FontColor(OfficialRed);
                            table.Cell().Element(CellStyle).Text("");
                            table.Cell().Element(CellStyle).Text("Applied");
                        }

                        // Joint liability breakdown
                        table.Cell().ColumnSpan(3).Element(SeparatorStyle)
                            .Text("JOINT AND SEVERAL LIABILITY — s.58(1) Traffic Act Cap 403").FontSize(8).SemiBold().FontColor(OfficialRed);

                        table.Cell().Element(CellStyle).Text("Driver's Fine");
                        table.Cell().Element(CellStyle).Text("");
                        table.Cell().Element(CellStyle).AlignRight().Text($"KES {perPartyKes:N2}");

                        table.Cell().Element(CellStyle).Text("Registered Owner's Fine");
                        table.Cell().Element(CellStyle).Text("");
                        table.Cell().Element(CellStyle).AlignRight().Text($"KES {perPartyKes:N2}");

                        table.Cell().Element(TotalStyle).Text("TOTAL COMBINED CHARGE");
                        table.Cell().Element(TotalStyle).Text("");
                        table.Cell().Element(TotalStyle).AlignRight().Text($"KES {prosecution.TotalFeeKes:N2}");
                    }
                }
                else
                {
                    // EAC — show both USD and KES
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderStyle).Text("Description");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("Qty/Kg");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("Amount (USD)");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("Amount (KES)");
                        static IContainer HeaderStyle(IContainer c) =>
                            c.DefaultTextStyle(x => x.SemiBold().FontSize(8.5f).FontColor(Colors.White))
                             .Background(Colors.Grey.Darken2).PaddingVertical(4).PaddingHorizontal(5);
                    });

                    if (prosecution != null)
                    {
                        if (prosecution.GvwOverloadKg > 0)
                        {
                            table.Cell().Element(CellStyle).Text("GVW Overload Charge");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.GvwOverloadKg:N0}");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.GvwFeeUsd:N2}");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.GvwFeeKes:N2}");
                        }
                        if (prosecution.MaxAxleOverloadKg > 0)
                        {
                            table.Cell().Element(CellStyle).Text("Axle Overload Charge");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.MaxAxleOverloadKg:N0}");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.MaxAxleFeeUsd:N2}");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{prosecution.MaxAxleFeeKes:N2}");
                        }

                        table.Cell().ColumnSpan(4).Element(NoteStyle)
                            .Text($"* Charge applied based on {basisLabel} (higher of the two)").FontSize(8).Italic();

                        if (prosecution.PenaltyMultiplier > 1)
                        {
                            table.Cell().Element(CellStyle).Text($"Repeat Offender Penalty ({prosecution.PenaltyMultiplier:N1}x)").FontColor(OfficialRed);
                            table.Cell().Element(CellStyle).Text(""); table.Cell().Element(CellStyle).Text(""); table.Cell().Element(CellStyle).Text("Applied");
                        }

                        // Joint liability breakdown
                        table.Cell().ColumnSpan(4).Element(SeparatorStyle)
                            .Text("JOINT AND SEVERAL LIABILITY — EAC Vehicle Load Control Act").FontSize(8).SemiBold().FontColor(OfficialRed);

                        table.Cell().Element(CellStyle).Text("Driver's Fine");
                        table.Cell().Element(CellStyle).Text("");
                        table.Cell().Element(CellStyle).AlignRight().Text($"USD {perPartyUsd:N2}");
                        table.Cell().Element(CellStyle).AlignRight().Text($"KES {perPartyKes:N2}");

                        table.Cell().Element(CellStyle).Text("Registered Owner's Fine");
                        table.Cell().Element(CellStyle).Text("");
                        table.Cell().Element(CellStyle).AlignRight().Text($"USD {perPartyUsd:N2}");
                        table.Cell().Element(CellStyle).AlignRight().Text($"KES {perPartyKes:N2}");

                        table.Cell().Element(TotalStyle).Text("TOTAL COMBINED CHARGE");
                        table.Cell().Element(TotalStyle).Text("");
                        table.Cell().Element(TotalStyle).AlignRight().Text($"USD {prosecution.TotalFeeUsd:N2}");
                        table.Cell().Element(TotalStyle).AlignRight().Text($"KES {prosecution.TotalFeeKes:N2}");
                    }
                }

                static IContainer CellStyle(IContainer c) =>
                    c.PaddingVertical(3)
                     .PaddingHorizontal(5)
                     .BorderBottom(0.5f)
                     .BorderColor(Colors.Grey.Lighten2)
                     .DefaultTextStyle(t => t.FontSize(8.5f));

                static IContainer NoteStyle(IContainer c) =>
                    c.PaddingVertical(2)
                     .PaddingHorizontal(5)
                     .Background(Colors.Grey.Lighten4)
                     .DefaultTextStyle(t => t.FontSize(7.5f));

                static IContainer SeparatorStyle(IContainer c) =>
                    c.PaddingVertical(2).PaddingHorizontal(5)
                     .Background(Colors.Red.Lighten5)
                     .DefaultTextStyle(t => t.FontSize(7.5f));

                static IContainer TotalStyle(IContainer c) =>
                    c.PaddingVertical(3).PaddingHorizontal(5)
                     .Background(Colors.Grey.Lighten4)
                     .DefaultTextStyle(t => t.FontSize(9.5f).SemiBold());
            });

            if (!isTrafficAct && prosecution != null)
                col.Item().PaddingTop(3).Text($"Exchange Rate: 1 USD = {prosecution.ForexRate:N2} KES")
                    .FontSize(8).Italic();
        });
    }

    private void ComposePaymentSummary(IContainer container)
    {
        var prosecution = _invoice.ProsecutionCase;
        var isTrafficAct = string.Equals(prosecution?.Act?.ChargingCurrency, "KES", StringComparison.OrdinalIgnoreCase)
                        || prosecution?.Act == null;
        var invoiceCurrency = string.IsNullOrWhiteSpace(_invoice.Currency) ? "KES" : _invoice.Currency;
        var forexRate = prosecution?.ForexRate ?? 130m;
        var amountPaid = _invoice.Receipts?.Where(r => r.DeletedAt == null).Sum(r => r.AmountPaid) ?? 0;

        container.Border(1).BorderColor(Colors.Black).Column(col =>
        {
            col.Item().Background(Colors.Grey.Lighten4).Padding(4).Row(r =>
            {
                r.RelativeItem().Text($"Subtotal ({invoiceCurrency}):").SemiBold();
                r.ConstantItem(80).AlignRight().Text($"{invoiceCurrency} {_invoice.AmountDue:N2}");
            });

            // Exchange rate and USD equivalent only for EAC (non-Traffic Act) invoices
            if (!isTrafficAct)
            {
                var equivalentCurrency = string.Equals(invoiceCurrency, "USD", StringComparison.OrdinalIgnoreCase) ? "KES" : "USD";
                var equivalentAmount = string.Equals(invoiceCurrency, "USD", StringComparison.OrdinalIgnoreCase)
                    ? _invoice.AmountDue * forexRate
                    : forexRate > 0 ? _invoice.AmountDue / forexRate : 0m;

                col.Item().Padding(4).Row(r =>
                {
                    r.RelativeItem().Text("Exchange Rate (1 USD):").FontSize(9);
                    r.ConstantItem(80).AlignRight().Text($"KES {forexRate:N2}").FontSize(9);
                });

                col.Item().Padding(4).Row(r =>
                {
                    r.RelativeItem().Text($"Amount Due ({equivalentCurrency}):").SemiBold();
                    r.ConstantItem(80).AlignRight().Text($"{equivalentCurrency} {equivalentAmount:N2}").SemiBold();
                });
            }

            if (amountPaid > 0)
            {
                col.Item().Background(Colors.Green.Lighten4).Padding(4).Row(r =>
                {
                    r.RelativeItem().Text("Amount Paid:").FontColor(Colors.Green.Darken2);
                    r.ConstantItem(80).AlignRight().Text($"{invoiceCurrency} {amountPaid:N2}").FontColor(Colors.Green.Darken2);
                });

                var balance = _invoice.AmountDue - amountPaid;
                col.Item().Background(balance > 0 ? Colors.Orange.Lighten4 : Colors.Green.Lighten3).Padding(4).Row(r =>
                {
                    r.RelativeItem().Text("BALANCE DUE:").Bold();
                    r.ConstantItem(80).AlignRight().Text($"{invoiceCurrency} {balance:N2}").Bold();
                });
            }
            else
            {
                col.Item().Background(Colors.Blue.Lighten4).Padding(4).Row(r =>
                {
                    r.RelativeItem().Text($"TOTAL DUE ({invoiceCurrency}):").Bold().FontSize(10);
                    r.ConstantItem(80).AlignRight().Text($"{invoiceCurrency} {_invoice.AmountDue:N2}").Bold().FontSize(10);
                });
            }
        });
    }

    private void ComposePaymentInstructions(IContainer container)
    {
        var hasBankDetails = !string.IsNullOrWhiteSpace(_payment?.BankAccountNumber);
        var hasMpesaPaybill = !string.IsNullOrWhiteSpace(_payment?.MpesaPaybillNumber);
        var hasMpesaTill = !string.IsNullOrWhiteSpace(_payment?.MpesaTillNumber);
        var hasAnyPaymentConfig = hasBankDetails || hasMpesaPaybill || hasMpesaTill;

        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(col =>
        {
            col.Item().Text("PAYMENT INSTRUCTIONS").FontSize(9).SemiBold().FontColor(KuraBlue);

            if (!hasAnyPaymentConfig)
            {
                col.Item().PaddingTop(3).Text("Contact the authority for payment instructions.").FontSize(8.5f).Italic();
                return;
            }

            col.Item().PaddingTop(3).Row(row =>
            {
                if (hasBankDetails)
                {
                    row.RelativeItem().Column(bank =>
                    {
                        bank.Item().Text("Bank Transfer:").FontSize(9).SemiBold();
                        if (!string.IsNullOrWhiteSpace(_payment!.BankName))
                            bank.Item().Text($"Bank: {_payment.BankName}").FontSize(9);
                        bank.Item().Text($"Account: {_payment.BankAccountNumber}").FontSize(9);
                        if (!string.IsNullOrWhiteSpace(_payment.BankBranch))
                            bank.Item().Text($"Branch: {_payment.BankBranch}").FontSize(9);
                        bank.Item().Text($"Reference: {_invoice.PesaflowInvoiceNumber ?? _invoice.InvoiceNo}").FontSize(9).SemiBold();
                    });
                }

                if (hasMpesaPaybill || hasMpesaTill)
                {
                    row.RelativeItem().Column(mpesa =>
                    {
                        mpesa.Item().Text("M-Pesa:").FontSize(9).SemiBold();
                        if (hasMpesaPaybill)
                        {
                            mpesa.Item().Text($"Business No: {_payment!.MpesaPaybillNumber}").FontSize(9);
                            mpesa.Item().Text($"Account: {_invoice.PesaflowInvoiceNumber ?? _invoice.InvoiceNo}").FontSize(9);
                        }
                        if (hasMpesaTill)
                            mpesa.Item().PaddingTop(hasMpesaPaybill ? 3 : 0).Text($"Till No: {_payment!.MpesaTillNumber}").FontSize(9).SemiBold();
                    });
                }
            });
        });
    }

    private void ComposeTermsAndConditions(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Text("TERMS AND CONDITIONS:").FontSize(8.5f).SemiBold();
            col.Item().PaddingLeft(8).Text(
                "1. Payment is due within 14 days from the invoice date.  " +
                "2. Failure to pay may result in additional penalties and legal action.  " +
                "3. Quote the Invoice Number on all payments and correspondence.  " +
                "4. This invoice is subject to Kenya Traffic Act Cap 403 regulations.")
                .FontSize(7.5f);
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
