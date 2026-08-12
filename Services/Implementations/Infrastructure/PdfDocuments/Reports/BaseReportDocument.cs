using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;
using TruLoad.Backend.Services.Implementations.Reporting;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports;

/// <summary>
/// Base class for tabular report PDF documents.
/// Extends BaseDocument with table rendering capabilities for report generation.
/// </summary>
public abstract class BaseReportDocument : BaseDocument
{
    public string ReportTitle { get; set; } = "Report";
    public string? ReportSubtitle { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? StationName { get; set; }
    public string? OrgLogoFile { get; set; }

    /// <summary>Organization name for report header branding (e.g. tenant name).</summary>
    public string? OrganizationName { get; set; }
    /// <summary>Whether this is an enforcement org (shows "REPUBLIC OF KENYA") or commercial.</summary>
    public bool IsEnforcement { get; set; } = true;
    /// <summary>Secondary logo file override. Null means no secondary logo.</summary>
    public string? SecondaryLogoFile { get; set; } = "coat-of-arms.png";

    public override byte[] Generate()
    {
        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeOfficialFooter);
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    private void ComposeHeader(IContainer container)
    {
        var dateRange = DateFrom.HasValue && DateTo.HasValue
            ? $"Period: {DateFrom.Value:dd/MM/yyyy} - {DateTo.Value:dd/MM/yyyy}"
            : $"Generated: {DateTime.UtcNow:dd/MM/yyyy HH:mm} EAT";

        ComposeOfficialHeaderWithLogos(
            container,
            ResolveOrgLogo(OrgLogoFile),
            SecondaryLogoFile,
            ReportTitle,
            ReportSubtitle ?? StationName,
            null,
            dateRange,
            organizationName: OrganizationName,
            isEnforcement: IsEnforcement);
    }

    protected abstract void ComposeContent(IContainer container);

    /// <summary>
    /// Renders a data table with headers and rows. When <paramref name="conditionalStatusColumnIndex"/>
    /// is set, the status found in that column drives the background/text colour for the WHOLE
    /// row (resolved via <see cref="ReportStatusColors"/>), overriding the flat zebra striping for
    /// that row - verified against the actual sample KURA NRB Axle Load Data Analysis workbook,
    /// whose "Within Permissible Tolerance"/"Overloaded and charged" rows are filled edge-to-edge
    /// (every column, not just the Status cell).
    /// </summary>
    protected void ComposeDataTable(
        IContainer container,
        string[] headers,
        IEnumerable<string[]> rows,
        float[]? columnWidths = null,
        string? summaryLabel = null,
        string? summaryValue = null,
        int? conditionalStatusColumnIndex = null)
    {
        container.Column(col =>
        {
            col.Spacing(3);

            // Table
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    if (columnWidths != null && columnWidths.Length == headers.Length)
                    {
                        foreach (var w in columnWidths)
                        {
                            if (w > 0)
                                columns.ConstantColumn(w);
                            else
                                columns.RelativeColumn();
                        }
                    }
                    else
                    {
                        for (var i = 0; i < headers.Length; i++)
                            columns.RelativeColumn();
                    }
                });

                // Header row
                table.Header(header =>
                {
                    foreach (var h in headers)
                    {
                        header.Cell().Background(KuraBlue).Padding(4)
                            .Text(h).FontSize(7).Bold().FontColor(Colors.White);
                    }
                });

                // Data rows
                var rowIndex = 0;
                foreach (var row in rows)
                {
                    var zebraColor = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                    // Row-level status highlight: the status found in conditionalStatusColumnIndex
                    // colours every cell in ITS row (not just the Status cell itself) when it's an
                    // exception state - verified against the sample template, whose tolerance/
                    // overloaded rows are filled edge-to-edge across every column.
                    ReportStatusColors.StatusStyle? highlight = null;
                    if (conditionalStatusColumnIndex.HasValue && conditionalStatusColumnIndex.Value < row.Length)
                    {
                        var status = row[conditionalStatusColumnIndex.Value];
                        if (ReportStatusColors.ShouldHighlightRow(status))
                            highlight = ReportStatusColors.Resolve(status);
                    }

                    foreach (var cell in row)
                    {
                        if (highlight != null)
                        {
                            table.Cell().Background(highlight.PdfBackgroundHex).Padding(3)
                                .Text(cell ?? "-").FontSize(7).FontColor(highlight.PdfTextHex).SemiBold();
                        }
                        else
                        {
                            table.Cell().Background(zebraColor).Padding(3)
                                .Text(cell ?? "-").FontSize(7);
                        }
                    }
                    rowIndex++;
                }
            });

            // Summary row if provided
            if (!string.IsNullOrEmpty(summaryLabel))
            {
                col.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().AlignRight().Text(summaryLabel).FontSize(9).SemiBold();
                    row.ConstantItem(100).AlignRight().Text(summaryValue ?? "").FontSize(9).Bold();
                });
            }
        });
    }

    /// <summary>
    /// Renders a summary statistics section.
    /// </summary>
    protected void ComposeSummaryCards(IContainer container, (string label, string value)[] items)
    {
        container.PaddingBottom(8).Row(row =>
        {
            foreach (var (label, value) in items)
            {
                row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
                {
                    c.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(value).FontSize(11).Bold();
                });
            }
        });
    }

    /// <summary>
    /// Renders a "Key:" legend block - a colour swatch + label per row - mirroring the sample KURA
    /// NRB template's Status legend beneath its data table.
    /// </summary>
    protected void ComposeLegend(IContainer container, (string colorHex, string label)[] items)
    {
        if (items.Length == 0)
            return;

        container.PaddingTop(6).Column(col =>
        {
            col.Spacing(2);
            col.Item().Text("Key:").FontSize(8).Bold();
            foreach (var (colorHex, label) in items)
            {
                col.Item().Row(row =>
                {
                    row.ConstantItem(16).Height(10).Background(colorHex)
                        .Border(0.5f).BorderColor(Colors.Grey.Lighten1);
                    row.RelativeItem().PaddingLeft(6).Text(label).FontSize(8);
                });
            }
        });
    }

    /// <summary>
    /// Renders a titled summary/breakdown table - a bold section heading followed by
    /// <see cref="ComposeDataTable"/> - the shared shape for the multi-table sections that
    /// reports like the Axle Load Data Analysis template need beneath their main register.
    /// </summary>
    protected void ComposeTitledTable(
        IContainer container, string title, string[] headers, IEnumerable<string[]> rows)
    {
        container.PaddingTop(10).Column(col =>
        {
            col.Spacing(3);
            col.Item().Text(title).FontSize(10).Bold().FontColor(KuraBlue);
            col.Item().Element(c => ComposeDataTable(c, headers, rows));
        });
    }

    /// <summary>
    /// Renders a single horizontal stacked proportion bar - the PDF "chart" primitive for simple
    /// share-of-total breakdowns (e.g. vehicle-type mix, overload share). QuestPDF has no real
    /// charting API, so this reuses the same coloured-box idiom as <see cref="ComposeSummaryCards"/>
    /// rather than attempting fragile custom-drawn charts; genuine multi-series/interactive charts
    /// stay on Recharts on-screen and Superset/AI-query for ad-hoc BI.
    /// </summary>
    protected void ComposeProportionBar(IContainer container, (string label, decimal value, string colorHex)[] segments)
    {
        var total = segments.Sum(s => s.value);
        if (total <= 0)
            return;

        container.Column(col =>
        {
            col.Spacing(3);

            col.Item().Height(18).Row(row =>
            {
                foreach (var (_, value, colorHex) in segments)
                {
                    if (value <= 0) continue;
                    row.RelativeItem((float)value).Background(colorHex);
                }
            });

            col.Item().Row(row =>
            {
                foreach (var (label, value, colorHex) in segments)
                {
                    row.AutoItem().PaddingRight(10).Row(legend =>
                    {
                        legend.ConstantItem(8).Height(8).Background(colorHex);
                        legend.AutoItem().PaddingLeft(3)
                            .Text($"{label} ({(decimal)value / total * 100:F1}%)").FontSize(6.5f);
                    });
                }
            });
        });
    }
}
