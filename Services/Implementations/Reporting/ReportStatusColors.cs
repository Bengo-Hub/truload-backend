using TruLoad.Backend.Common.Constants;

namespace TruLoad.Backend.Services.Implementations.Reporting;

/// <summary>
/// Single source of truth for status-driven colour coding, shared by the Excel (ClosedXML)
/// and PDF (QuestPDF) report engines, plus the vocabulary mapping used by templates that
/// mirror an external KURA document format (e.g. the NRB Axle Load Data Analysis report).
/// </summary>
public static class ReportStatusColors
{
    /// <summary>
    /// One status bucket's colours for both output formats. Excel uses the sample template's
    /// bold, solid legend fills; PDF keeps the platform's existing soft-tint cell style
    /// (<see cref="Infrastructure.PdfDocuments.BaseDocument.ComposeConditionalCell"/>'s scheme)
    /// so on-screen-adjacent documents stay visually consistent with the rest of the app.
    /// </summary>
    public sealed record StatusStyle(
        string ExcelFillHex, string ExcelFontHex,
        string PdfBackgroundHex, string PdfTextHex);

    private static readonly Dictionary<string, StatusStyle> Styles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["overloaded"] = new(BrandingConstants.Colors.SampleTemplateRed, "#FFFFFF", "#FEE2E2", BrandingConstants.Colors.OfficialRed),
        ["compliant"] = new("#C6EFCE", BrandingConstants.Colors.OfficialGreen, "#DCFCE7", BrandingConstants.Colors.OfficialGreen),
        ["tolerance"] = new(BrandingConstants.Colors.ToleranceBlue, "#FFFFFF", "#DBEAFE", BrandingConstants.Colors.KuraBlue),
        ["warning"] = new("#FEF3C7", "#B45309", "#FEF3C7", "#B45309"),
        ["legal"] = new("#DBEAFE", BrandingConstants.Colors.KuraBlue, "#DBEAFE", BrandingConstants.Colors.KuraBlue),
        ["default"] = new("#F3F4F6", BrandingConstants.Colors.KuraBlack, "#F3F4F6", BrandingConstants.Colors.KuraBlack),
    };

    /// <summary>
    /// Classifies a free-form status string (either the platform's raw ControlStatus vocabulary
    /// - Compliant/Warning/Overloaded/Pending/TagHold - or an already-mapped display string like
    /// "Within Permissible Tolerance") into one of the shared colour buckets.
    /// </summary>
    public static string Classify(string? status)
    {
        var s = (status ?? string.Empty).Trim().ToLowerInvariant();
        return s switch
        {
            "" => "default",
            _ when s.Contains("overloaded and charged") || s is "overloaded" or "violation" or "failed" or "overload" or "fail" or "rejected" => "overloaded",
            _ when s.Contains("within permissible tolerance") || s == "tolerance" => "tolerance",
            _ when s is "compliant" or "passed" or "pass" or "success" or "ok" or "approved" or "released" => "compliant",
            _ when s is "warning" or "pending" => "warning",
            "legal" => "legal",
            _ => "default"
        };
    }

    /// <summary>Resolves the shared colour style for a status string via <see cref="Classify"/>.</summary>
    public static StatusStyle Resolve(string? status)
        => Styles.TryGetValue(Classify(status), out var style) ? style : Styles["default"];

    /// <summary>
    /// Whether a status should visually flag/highlight its row (full-row fill) rather than blend
    /// into the plain zebra-striped background. Only "exception" states highlight, so highlighted
    /// rows stay the signal rather than the noise - verified against the sample KURA NRB template,
    /// where "Ok" rows carry no fill at all and only the tolerance/overloaded exceptions are filled
    /// (edge-to-edge across every column, not just the Status cell).
    /// </summary>
    public static bool ShouldHighlightRow(string? status)
        => Classify(status) is "overloaded" or "tolerance" or "warning";

    /// <summary>
    /// Maps the platform's real ControlStatus vocabulary (Compliant/Warning/Overloaded/...) onto
    /// the KURA NRB Axle Load Data Analysis sample template's 3-state wording
    /// (Ok / Within Permissible Tolerance / Overloaded and charged). Statuses outside that
    /// 3-state set (Pending, TagHold, Configuration Error) pass through unchanged rather than
    /// being silently dropped.
    /// </summary>
    public static string ToSampleTemplateStatus(string? controlStatus)
    {
        var s = (controlStatus ?? string.Empty).Trim();
        return s switch
        {
            "" => "Ok",
            "Compliant" => "Ok",
            "Warning" => "Within Permissible Tolerance",
            "Overloaded" => "Overloaded and charged",
            _ => s
        };
    }
}
