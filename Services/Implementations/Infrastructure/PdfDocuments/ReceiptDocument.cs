using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;
using TruLoad.Backend.Models.Financial;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Receipt Document - Official payment receipt
/// Confirms payment received for overload charges
/// </summary>
public class ReceiptDocument : BaseDocument
{
    private readonly Receipt _receipt;
    private readonly string? _organizationName;
    private readonly string? _organizationAddress;
    private readonly string _orgLogoFile;
    private readonly bool _showSecondaryLogo;

    public ReceiptDocument(Receipt receipt, string? organizationName = null, string? organizationAddress = null, string? orgLogoFile = null, bool showSecondaryLogo = true)
    {
        _receipt = receipt;
        _organizationName = organizationName ?? "Kenya Roads Authority (KURA)";
        _organizationAddress = organizationAddress ?? "P.O. Box 00100-1234, Nairobi, Kenya";
        _orgLogoFile = ResolveOrgLogo(orgLogoFile);
        _showSecondaryLogo = showSecondaryLogo;
    }

    public override byte[] Generate()
    {
        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.0f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Inter"));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(6).Element(ComposeContent);
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
            // Logo row with organization branding
            col.Item().Row(row =>
            {
                row.ConstantItem(LogoWidth).AlignMiddle().Column(logoCol =>
                {
                    if (primaryLogo != null)
                        logoCol.Item().Height(LogoHeight).Image(primaryLogo, ImageScaling.FitArea);
                });

                row.RelativeItem().PaddingHorizontal(5).Column(org =>
                {
                    org.Item().AlignCenter().Text(BrandingConstants.Organization.RepublicOfKenya)
                        .FontSize(10).SemiBold();
                    org.Item().AlignCenter().Text(_organizationName).FontSize(12).SemiBold().FontColor(KuraBlue);
                    org.Item().AlignCenter().Text(_organizationAddress).FontSize(8);
                });

                row.ConstantItem(LogoWidth).AlignMiddle().Column(logoCol =>
                {
                    if (secondaryLogo != null)
                        logoCol.Item().Height(LogoHeight).Image(secondaryLogo, ImageScaling.FitArea);
                });
            });

            // Receipt badge
            col.Item().PaddingTop(6).AlignCenter()
                .Background(OfficialGreen).Padding(6)
                .Text("RECEIPT").FontSize(14).Bold().FontColor(Colors.White);

            // Receipt details row
            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Receipt No: {_receipt.ReceiptNo}").FontSize(10).SemiBold();
                    c.Item().Text($"Invoice No: {_receipt.Invoice?.InvoiceNo ?? "N/A"}").FontSize(9);
                });
                row.ConstantItem(120).AlignRight().Column(c =>
                {
                    c.Item().Text($"Date: {_receipt.PaymentDate:dd/MM/yyyy}").FontSize(9);
                    c.Item().Text($"Time: {_receipt.PaymentDate:HH:mm}").FontSize(9);
                });
            });

            col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(8);

            // Vehicle Information
            col.Item().Text("VEHICLE:").FontSize(9).SemiBold();
            col.Item().PaddingLeft(10).Text(_receipt.Invoice?.ProsecutionCase?.Weighing?.VehicleRegNumber ?? "N/A").SemiBold();

            // Payment Details
            col.Item().PaddingTop(5).Border(1).BorderColor(Colors.Black).Column(payment =>
            {
                payment.Item().Background(Colors.Grey.Lighten4).Padding(5).Row(r =>
                {
                    r.RelativeItem().Text("PAYMENT DETAILS").FontSize(9).SemiBold();
                });

                payment.Item().Padding(5).Column(details =>
                {
                    details.Spacing(3);
                    ComposePaymentRow(details.Item(), "Payment Method", GetPaymentMethodDisplay());
                    ComposePaymentRow(details.Item(), "Transaction Ref", _receipt.TransactionReference ?? "N/A");
                    ComposePaymentRow(details.Item(), "Currency", _receipt.Currency);
                });

                // Amount Section
                payment.Item().Background(OfficialGreen).Padding(8).Row(r =>
                {
                    r.RelativeItem().Text("AMOUNT RECEIVED:").FontSize(10).SemiBold().FontColor(Colors.White);
                    r.ConstantItem(100).AlignRight().Text($"{_receipt.Currency} {_receipt.AmountPaid:N2}")
                        .FontSize(12).Bold().FontColor(Colors.White);
                });
            });

            // Invoice Summary
            if (_receipt.Invoice != null)
            {
                col.Item().PaddingTop(5).Column(summary =>
                {
                    summary.Item().Text("INVOICE SUMMARY:").FontSize(9).SemiBold();
                    summary.Item().PaddingLeft(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.ConstantColumn(80);
                        });

                        var totalPaid = _receipt.Invoice.Receipts?.Where(r => r.DeletedAt == null).Sum(r => r.AmountPaid) ?? 0;
                        var balance = _receipt.Invoice.AmountDue - totalPaid;
                        var sym = _receipt.Currency == "KES" ? "KES " : "$";

                        table.Cell().Text("Invoice Amount:").FontSize(8);
                        table.Cell().AlignRight().Text($"{sym}{_receipt.Invoice.AmountDue:N2}").FontSize(8);

                        table.Cell().Text("Total Paid:").FontSize(8);
                        table.Cell().AlignRight().Text($"{sym}{totalPaid:N2}").FontSize(8).FontColor(Colors.Green.Medium);

                        table.Cell().Text("Balance Due:").FontSize(8).SemiBold();
                        table.Cell().AlignRight().Text($"{sym}{balance:N2}").FontSize(8).SemiBold()
                            .FontColor(balance > 0 ? Colors.Red.Medium : Colors.Green.Medium);
                    });

                    // Payment Status Badge
                    var totalPaidCheck = _receipt.Invoice.Receipts?.Where(r => r.DeletedAt == null).Sum(r => r.AmountPaid) ?? 0;
                    var balanceCheck = _receipt.Invoice.AmountDue - totalPaidCheck;

                    summary.Item().PaddingTop(5).AlignCenter().Element(badge =>
                    {
                        if (balanceCheck <= 0)
                        {
                            badge.Background(Colors.Green.Lighten3).Padding(5).Text("PAID IN FULL")
                                .FontSize(10).SemiBold().FontColor(Colors.Green.Darken2);
                        }
                        else
                        {
                            badge.Background(Colors.Orange.Lighten3).Padding(5).Text("PARTIAL PAYMENT")
                                .FontSize(10).SemiBold().FontColor(Colors.Orange.Darken2);
                        }
                    });
                });
            }

            // Reference Information
            col.Item().PaddingTop(5).Background(Colors.Grey.Lighten4).Padding(5).Column(refs =>
            {
                refs.Item().Text("REFERENCE INFORMATION:").FontSize(8).SemiBold();
                refs.Item().Row(r =>
                {
                    r.RelativeItem().Text($"Case No: {_receipt.Invoice?.ProsecutionCase?.CaseRegister?.CaseNo ?? "N/A"}").FontSize(8);
                    r.RelativeItem().Text($"Certificate: {_receipt.Invoice?.ProsecutionCase?.CertificateNo ?? "N/A"}").FontSize(8);
                });
            });

            // Signature Section
            col.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Cashier/Receiving Officer", _receipt.ReceivedBy?.FullName));
            });

            // Notice
            col.Item().PaddingTop(10).AlignCenter().Text("This receipt is valid proof of payment. Please retain for your records.")
                .FontSize(7).Italic();
        });
    }

    private void ComposePaymentRow(IContainer container, string label, string value)
    {
        container.Row(row =>
        {
            row.ConstantItem(100).Text(label + ":").FontSize(8);
            row.RelativeItem().Text(value).FontSize(8).SemiBold();
        });
    }

    private string GetPaymentMethodDisplay()
    {
        return _receipt.PaymentMethod switch
        {
            "cash" => "Cash",
            "mobile_money" => "M-Pesa / Mobile Money",
            "bank_transfer" => "Bank Transfer",
            "card" => "Card Payment",
            "cheque" => "Cheque",
            _ => _receipt.PaymentMethod
        };
    }
}
