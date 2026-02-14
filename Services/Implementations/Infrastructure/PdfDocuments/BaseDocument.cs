using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Base class for all PDF document generators.
/// Provides common document structure, branding, and logo rendering using centralized constants.
/// </summary>
public abstract class BaseDocument
{
    private static readonly string ImagesBasePath = Path.Combine(
        Directory.GetCurrentDirectory(), "wwwroot", "images");

    protected readonly string KuraBlack = BrandingConstants.Colors.KuraBlack;
    protected readonly string KuraBlue = BrandingConstants.Colors.KuraBlue;
    protected readonly string OfficialRed = BrandingConstants.Colors.OfficialRed;
    protected readonly string OfficialGreen = BrandingConstants.Colors.OfficialGreen;

    public abstract byte[] Generate();

    /// <summary>
    /// Loads a logo image from wwwroot/images/ as byte array.
    /// Returns null if the file does not exist (graceful degradation).
    /// </summary>
    protected static byte[]? LoadLogo(string fileName)
    {
        var path = Path.Combine(ImagesBasePath, fileName);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    /// <summary>
    /// Composes an official document header with left logo, centered title block, and right logo.
    /// Used by all official documents for consistent branding.
    /// </summary>
    protected void ComposeOfficialHeaderWithLogos(
        IContainer container,
        string primaryLogoFile,
        string? secondaryLogoFile,
        string documentTitle,
        string? subtitle = null,
        string? referenceNumber = null,
        string? dateText = null,
        string? titleColor = null)
    {
        var primaryLogo = LoadLogo(primaryLogoFile);
        var secondaryLogo = secondaryLogoFile != null ? LoadLogo(secondaryLogoFile) : null;
        var headerColor = titleColor ?? KuraBlue;

        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                // Left logo
                row.ConstantItem(60).AlignMiddle().Column(logoCol =>
                {
                    if (primaryLogo != null)
                        logoCol.Item().Height(50).Image(primaryLogo, ImageScaling.FitArea);
                });

                // Center title block
                row.RelativeItem().AlignCenter().PaddingHorizontal(5).Column(center =>
                {
                    center.Item().AlignCenter().Text(BrandingConstants.Organization.RepublicOfKenya)
                        .FontSize(11).SemiBold();
                    center.Item().AlignCenter().Text(documentTitle)
                        .FontSize(14).Bold().FontColor(headerColor);
                    if (!string.IsNullOrEmpty(subtitle))
                        center.Item().AlignCenter().Text(subtitle).FontSize(9);
                });

                // Right logo
                row.ConstantItem(60).AlignMiddle().Column(logoCol =>
                {
                    if (secondaryLogo != null)
                        logoCol.Item().Height(50).Image(secondaryLogo, ImageScaling.FitArea);
                });
            });

            // Reference number and date row
            if (!string.IsNullOrEmpty(referenceNumber) || !string.IsNullOrEmpty(dateText))
            {
                col.Item().PaddingTop(3).Row(row =>
                {
                    if (!string.IsNullOrEmpty(referenceNumber))
                        row.RelativeItem().Text(referenceNumber).FontSize(10).SemiBold();
                    if (!string.IsNullOrEmpty(dateText))
                        row.RelativeItem().AlignRight().Text(dateText).FontSize(10);
                });
            }

            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    /// <summary>
    /// Composes a triple-logo header: left logo, center coat of arms, right logo.
    /// Used by documents requiring all three official logos.
    /// </summary>
    protected void ComposeTripleLogoHeader(
        IContainer container,
        string leftLogoFile,
        string centerLogoFile,
        string rightLogoFile,
        string documentTitle,
        string? subtitle = null,
        string? referenceNumber = null,
        string? dateText = null,
        string? titleColor = null)
    {
        var leftLogo = LoadLogo(leftLogoFile);
        var centerLogo = LoadLogo(centerLogoFile);
        var rightLogo = LoadLogo(rightLogoFile);
        var headerColor = titleColor ?? KuraBlue;

        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(55).AlignMiddle().Column(logoCol =>
                {
                    if (leftLogo != null)
                        logoCol.Item().Height(45).Image(leftLogo, ImageScaling.FitArea);
                });

                row.RelativeItem().AlignCenter().PaddingHorizontal(5).Column(center =>
                {
                    if (centerLogo != null)
                        center.Item().AlignCenter().Height(40).Image(centerLogo, ImageScaling.FitArea);

                    center.Item().AlignCenter().Text(BrandingConstants.Organization.RepublicOfKenya)
                        .FontSize(10).SemiBold();
                    center.Item().AlignCenter().Text(documentTitle)
                        .FontSize(13).Bold().FontColor(headerColor);
                    if (!string.IsNullOrEmpty(subtitle))
                        center.Item().AlignCenter().Text(subtitle).FontSize(8);
                });

                row.ConstantItem(55).AlignMiddle().Column(logoCol =>
                {
                    if (rightLogo != null)
                        logoCol.Item().Height(45).Image(rightLogo, ImageScaling.FitArea);
                });
            });

            if (!string.IsNullOrEmpty(referenceNumber) || !string.IsNullOrEmpty(dateText))
            {
                col.Item().PaddingTop(3).Row(row =>
                {
                    if (!string.IsNullOrEmpty(referenceNumber))
                        row.RelativeItem().Text(referenceNumber).FontSize(10).SemiBold();
                    if (!string.IsNullOrEmpty(dateText))
                        row.RelativeItem().AlignRight().Text(dateText).FontSize(10);
                });
            }

            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    /// <summary>
    /// Composes a conditional cell with color based on compliance status.
    /// </summary>
    protected void ComposeConditionalCell(IContainer container, string text, string status)
    {
        var (bgColor, textColor) = status.ToLower() switch
        {
            "overloaded" or "violation" or "failed" => ("#FEE2E2", OfficialRed),
            "compliant" or "passed" or "success" => ("#DCFCE7", OfficialGreen),
            "warning" or "pending" or "tolerance" => ("#FEF3C7", "#B45309"),
            "legal" => ("#DBEAFE", KuraBlue),
            _ => ("#F3F4F6", KuraBlack)
        };

        container.Background(bgColor).Padding(3)
            .Text(text).FontSize(8).FontColor(textColor).SemiBold();
    }

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
                row.RelativeItem().DefaultTextStyle(x => x.FontSize(7)).Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });

                row.RelativeItem().AlignCenter().DefaultTextStyle(x => x.FontSize(7).Italic())
                    .Text(BrandingConstants.DocumentFooter.OfficialSealText);

                row.RelativeItem().AlignRight().DefaultTextStyle(x => x.FontSize(7))
                    .Text($"Generated: {DateTime.UtcNow:dd/MM/yyyy HH:mm} EAT");
            });

            col.Item().PaddingTop(2).AlignCenter().DefaultTextStyle(x => x.FontSize(6).Italic())
                .Text(BrandingConstants.DocumentFooter.DisclaimerText);
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
