using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Base class for all PDF document generators.
/// Provides common document structure, branding, logo rendering, and status images.
/// All official documents inherit from this for consistent branding.
/// </summary>
public abstract class BaseDocument
{
    private static readonly string ImagesBasePath = Path.Combine(
        Directory.GetCurrentDirectory(), "wwwroot", "images");

    protected readonly string KuraBlack = BrandingConstants.Colors.KuraBlack;
    protected readonly string KuraBlue = BrandingConstants.Colors.KuraBlue;
    protected readonly string OfficialRed = BrandingConstants.Colors.OfficialRed;
    protected readonly string OfficialGreen = BrandingConstants.Colors.OfficialGreen;

    // Logo sizes - large enough for official documents
    protected const float LogoWidth = 120;
    protected const float LogoHeight = 90;
    protected const float SmallLogoWidth = 80;
    protected const float SmallLogoHeight = 60;

    public abstract byte[] Generate();

    /// <summary>
    /// Resolves the organization logo file to use for document branding.
    /// Falls back to TruLoad default logo, then KURA logo as last resort.
    /// </summary>
    public static string ResolveOrgLogo(string? orgLogoFile)
    {
        // Try org-specific logo first
        if (!string.IsNullOrEmpty(orgLogoFile))
        {
            var orgPath = Path.Combine(ImagesBasePath, orgLogoFile);
            if (File.Exists(orgPath))
                return orgLogoFile;
        }

        // Fall back to TruLoad default logo
        var truloadPath = Path.Combine(ImagesBasePath, BrandingConstants.Logos.TruLoadLogo);
        if (File.Exists(truloadPath))
            return BrandingConstants.Logos.TruLoadLogo;

        // Last resort: KURA logo (for enforcement orgs)
        return BrandingConstants.Logos.KuraLogo;
    }

    /// <summary>
    /// Loads an image from wwwroot/images/ as byte array.
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
    /// Supports optional organization name display below "REPUBLIC OF KENYA".
    /// </summary>
    protected void ComposeOfficialHeaderWithLogos(
        IContainer container,
        string primaryLogoFile,
        string? secondaryLogoFile,
        string documentTitle,
        string? subtitle = null,
        string? referenceNumber = null,
        string? dateText = null,
        string? titleColor = null,
        string? organizationName = null,
        bool isEnforcement = true)
    {
        var primaryLogo = LoadLogo(primaryLogoFile);
        var secondaryLogo = secondaryLogoFile != null ? LoadLogo(secondaryLogoFile) : null;
        var headerColor = titleColor ?? KuraBlue;

        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                // Left logo - same fixed box as right for uniform size (right logo is the standard)
                row.ConstantItem(LogoWidth).AlignMiddle().Column(logoCol =>
                {
                    if (primaryLogo != null)
                        logoCol.Item().Width(LogoWidth).Height(LogoHeight).Image(primaryLogo, ImageScaling.FitArea);
                });

                // Center title block
                row.RelativeItem().AlignCenter().PaddingHorizontal(5).Column(center =>
                {
                    if (isEnforcement)
                    {
                        // Government documents: "REPUBLIC OF KENYA" above org name
                        center.Item().AlignCenter().Text(BrandingConstants.Organization.RepublicOfKenya)
                            .FontSize(11).SemiBold();

                        if (!string.IsNullOrEmpty(organizationName))
                        {
                            center.Item().AlignCenter().Text(organizationName)
                                .FontSize(9).SemiBold();
                        }
                    }
                    else
                    {
                        // Commercial tenants: org name as main heading, no "REPUBLIC OF KENYA"
                        if (!string.IsNullOrEmpty(organizationName))
                        {
                            center.Item().AlignCenter().Text(organizationName)
                                .FontSize(11).SemiBold();
                        }
                    }

                    center.Item().AlignCenter().Text(documentTitle)
                        .FontSize(14).Bold().FontColor(headerColor);
                    if (!string.IsNullOrEmpty(subtitle))
                        center.Item().AlignCenter().Text(subtitle).FontSize(9);
                });

                // Right logo - standard size (both logos use same Width x Height for consistent design)
                row.ConstantItem(LogoWidth).AlignMiddle().Column(logoCol =>
                {
                    if (secondaryLogo != null)
                        logoCol.Item().Width(LogoWidth).Height(LogoHeight).Image(secondaryLogo, ImageScaling.FitArea);
                });
            });

            // Reference number and date row
            if (!string.IsNullOrEmpty(referenceNumber) || !string.IsNullOrEmpty(dateText))
            {
                col.Item().PaddingTop(2).Row(row =>
                {
                    if (!string.IsNullOrEmpty(referenceNumber))
                        row.RelativeItem().Text(referenceNumber).FontSize(9).SemiBold();
                    if (!string.IsNullOrEmpty(dateText))
                        row.RelativeItem().AlignRight().Text(dateText).FontSize(9);
                });
            }

            col.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Colors.Black);
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
                // Left logo - same fixed box as right for uniform size
                row.ConstantItem(SmallLogoWidth).AlignMiddle().Column(logoCol =>
                {
                    if (leftLogo != null)
                        logoCol.Item().Width(SmallLogoWidth).Height(SmallLogoHeight).Image(leftLogo, ImageScaling.FitArea);
                });

                row.RelativeItem().AlignCenter().PaddingHorizontal(5).Column(center =>
                {
                    if (centerLogo != null)
                        center.Item().AlignCenter().Width(SmallLogoWidth).Height(SmallLogoHeight).Image(centerLogo, ImageScaling.FitArea);

                    center.Item().AlignCenter().Text(BrandingConstants.Organization.RepublicOfKenya)
                        .FontSize(10).SemiBold();
                    center.Item().AlignCenter().Text(documentTitle)
                        .FontSize(13).Bold().FontColor(headerColor);
                    if (!string.IsNullOrEmpty(subtitle))
                        center.Item().AlignCenter().Text(subtitle).FontSize(8);
                });

                // Right logo - same size as left for consistent design
                row.ConstantItem(SmallLogoWidth).AlignMiddle().Column(logoCol =>
                {
                    if (rightLogo != null)
                        logoCol.Item().Width(SmallLogoWidth).Height(SmallLogoHeight).Image(rightLogo, ImageScaling.FitArea);
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
    /// Composes a status indicator image based on compliance status.
    /// Uses greenbutton.png for compliant, redbutton.jpg for overloaded, tagged.png for tagged vehicles.
    /// Falls back to colored text if image not found.
    /// </summary>
    protected void ComposeStatusImage(IContainer container, string status, bool isTagged = false)
    {
        string? imageFile;
        string statusText;
        string bgColor;
        string textColor;

        if (isTagged)
        {
            imageFile = "tagged.png";
            statusText = "TAGGED";
            bgColor = "#FEF3C7";
            textColor = "#B45309";
        }
        else
        {
            var normalizedStatus = status?.ToUpperInvariant() ?? "";
            switch (normalizedStatus)
            {
                case "LEGAL" or "COMPLIANT":
                    imageFile = "greenbutton.png";
                    statusText = "LEGAL";
                    bgColor = "#DCFCE7";
                    textColor = OfficialGreen;
                    break;
                case "OVERLOAD" or "OVERLOADED" or "VIOLATION":
                    imageFile = "redbutton.jpg";
                    statusText = "OVERLOADED";
                    bgColor = "#FEE2E2";
                    textColor = OfficialRed;
                    break;
                case "WARNING":
                    imageFile = "tagged.png";
                    statusText = "WARNING";
                    bgColor = "#FEF3C7";
                    textColor = "#B45309";
                    break;
                default:
                    imageFile = null;
                    statusText = status ?? "PENDING";
                    bgColor = "#F3F4F6";
                    textColor = KuraBlack;
                    break;
            }
        }

        var imageBytes = imageFile != null ? LoadLogo(imageFile) : null;

        container.Column(col =>
        {
            if (imageBytes != null)
            {
                col.Item().AlignCenter().Width(40).Height(40).Image(imageBytes, ImageScaling.FitArea);
            }
            col.Item().PaddingTop(3).AlignCenter()
                .Background(bgColor).Padding(5)
                .Text(statusText).FontSize(10).Bold().FontColor(textColor);
        });
    }

    /// <summary>
    /// Composes a conditional cell with color based on compliance status.
    /// </summary>
    protected void ComposeConditionalCell(IContainer container, string text, string status)
    {
        var (bgColor, textColor) = status.ToLower() switch
        {
            "overloaded" or "violation" or "failed" or "overload" => ("#FEE2E2", OfficialRed),
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
