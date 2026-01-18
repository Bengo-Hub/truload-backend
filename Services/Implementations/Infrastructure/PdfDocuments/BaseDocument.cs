using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Base class for all PDF document generators.
/// Provides common document structure and branding using centralized constants.
/// </summary>
public abstract class BaseDocument
{
    /// <summary>
    /// Kenya Roads Authority branding color
    /// </summary>
    protected readonly string KuraBlack = BrandingConstants.Colors.KuraBlack;

    /// <summary>
    /// Kenya Roads Authority blue color
    /// </summary>
    protected readonly string KuraBlue = BrandingConstants.Colors.KuraBlue;

    /// <summary>
    /// Official government red for warnings/prohibitions
    /// </summary>
    protected readonly string OfficialRed = BrandingConstants.Colors.OfficialRed;

    /// <summary>
    /// Official green for compliance/clearance
    /// </summary>
    protected readonly string OfficialGreen = BrandingConstants.Colors.OfficialGreen;

    /// <summary>
    /// Generates the PDF document
    /// </summary>
    public abstract byte[] Generate();


    /// <summary>
    /// Creates standard footer for official documents
    /// </summary>
    protected void ComposeOfficialFooter(IContainer container)
    {
        container.PaddingTop(10).Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            col.Item().PaddingTop(3).Row(row =>
            {
                // Page numbers
                row.RelativeItem().DefaultTextStyle(x => x.FontSize(7)).Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });

                // Official seal text
                row.RelativeItem().AlignCenter().DefaultTextStyle(x => x.FontSize(7).Italic())
                    .Text("Official System Generated Document - Digitally Signed");

                // Generation timestamp
                row.RelativeItem().AlignRight().DefaultTextStyle(x => x.FontSize(7))
                    .Text($"Generated: {DateTime.UtcNow:dd/MM/yyyy HH:mm} EAT");
            });

            // Disclaimer
            col.Item().PaddingTop(2).AlignCenter().DefaultTextStyle(x => x.FontSize(6).Italic())
                .Text("This is a computer-generated document. No signature is required for validity.");
        });
    }

    /// <summary>
    /// Creates a standard info table row
    /// </summary>
    protected void ComposeInfoRow(IContainer container, string label, string value, bool bold = false)
    {
        container.Row(row =>
        {
            row.ConstantItem(150).Text(label + ":").FontSize(9).SemiBold();

            if (bold)
                row.RelativeItem().Text(value).FontSize(9).SemiBold();
            else
                row.RelativeItem().Text(value).FontSize(9);
        });
    }

    /// <summary>
    /// Creates signature placeholder section
    /// </summary>
    protected void ComposeSignatureBlock(IContainer container, string role, string? officerName = null)
    {
        container.Column(c =>
        {
            c.Spacing(3);
            c.Item().Text("_______________________________").FontSize(9);
            c.Item().Text(role.ToUpper()).SemiBold().FontSize(8);
            if (!string.IsNullOrEmpty(officerName))
                c.Item().Text(officerName).FontSize(8);
            c.Item().Text("Name, Signature & Official Stamp").FontSize(7).Italic();
        });
    }
}
