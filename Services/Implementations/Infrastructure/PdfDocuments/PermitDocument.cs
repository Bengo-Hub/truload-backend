using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

public class PermitDocument : BaseDocument
{
    private readonly Permit _permit;

    public PermitDocument(Permit permit)
    {
        _permit = permit;
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
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Inter"));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(8).Element(ComposeContent);
                page.Footer().Element(ComposeOfficialFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        ComposeOfficialHeaderWithLogos(
            container,
            BrandingConstants.Logos.KuraLogo,
            BrandingConstants.Logos.CourtOfArmsKenya,
            "VEHICLE LOAD DIMENSION PERMIT",
            subtitle: "OFFICIAL PERMIT CERTIFICATE",
            referenceNumber: $"Permit No: {_permit.PermitNo}",
            dateText: $"Issued Date: {_permit.CreatedAt:dd/MM/yyyy}");
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(10);

            // Permit Status
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(statusCol =>
                {
                    statusCol.Item().Text("PERMIT DETAILS").FontSize(12).SemiBold();
                    statusCol.Item().PaddingTop(2).Text($"Status: {_permit.Status.ToUpper()}").FontSize(10).SemiBold()
                        .FontColor(_permit.Status.ToLower() == "active" ? OfficialGreen : OfficialRed);
                });
                row.ConstantItem(80).AlignCenter().Element(c =>
                    ComposeStatusImage(c, _permit.Status.ToLower() == "active" ? "LEGAL" : "PENDING"));
            });

            // Vehicle & Validity Information
            col.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Column(tableCol =>
            {
                tableCol.Item().Background("#F0F4F8").Padding(5).Text("VEHICLE & VALIDITY INFORMATION").FontSize(10).SemiBold();
                tableCol.Item().Padding(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    ComposeInfoCell(table, "Permit Number", _permit.PermitNo, true);
                    ComposeInfoCell(table, "Vehicle Registration", _permit.Vehicle?.RegNo ?? "N/A", true);
                    ComposeInfoCell(table, "Permit Type", _permit.PermitType?.Name ?? "N/A");
                    ComposeInfoCell(table, "Valid From", _permit.ValidFrom.ToString("dd/MM/yyyy"));
                    ComposeInfoCell(table, "Valid To", _permit.ValidTo.ToString("dd/MM/yyyy"));
                    ComposeInfoCell(table, "Issuing Authority", _permit.IssuingAuthority ?? "N/A");
                });
            });

            // Allowances / Extensions
            col.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Column(extCol =>
            {
                extCol.Item().Background("#F0F4F8").Padding(5).Text("PERMIT ALLOWANCES").FontSize(10).SemiBold();
                extCol.Item().Padding(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    ComposeInfoCell(table, "GVW Extension (KG)", (_permit.GvwExtensionKg ?? _permit.PermitType?.GvwExtensionKg ?? 0).ToString("N0"));
                    ComposeInfoCell(table, "Axle Extension (KG)", (_permit.AxleExtensionKg ?? _permit.PermitType?.AxleExtensionKg ?? 0).ToString("N0"));
                });
            });

            // Terms and Conditions
            col.Item().Column(terms =>
            {
                terms.Item().Text("TERMS AND CONDITIONS").FontSize(10).SemiBold();
                terms.Item().PaddingTop(5).Text(
                    "1. This permit must be carried in the vehicle at all times and produced on demand by an authorized officer.\n" +
                    "2. The permit is only valid for the vehicle registration number specified above.\n" +
                    "3. Any alteration to this permit renders it null and void.\n" +
                    "4. The permit is subject to the conditions specified in the EAC Vehicle Load Control Act 2016.\n" +
                    "5. Overloading beyond the permit allowances will result in prosecution and fines.")
                    .FontSize(8).LineHeight(1.5f);
            });

            // Signatures
            col.Item().PaddingTop(20).Element(ComposeSignatures);
        });
    }

    private void ComposeSignatures(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Issuing Officer"));
            row.ConstantItem(50);
            row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Verification Officer"));
        });
    }

    private static void ComposeInfoCell(TableDescriptor table, string label, string value, bool bold = false)
    {
        table.Cell().PaddingVertical(3).Row(row =>
        {
            row.ConstantItem(150).Text(label + ":").FontSize(9).SemiBold().FontColor("#4B5563");
            if (bold)
                row.RelativeItem().Text(value).FontSize(10).Bold();
            else
                row.RelativeItem().Text(value).FontSize(10);
        });
    }
}
