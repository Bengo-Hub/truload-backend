using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;

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
            "kura-logo.png",
            "coat-of-arms.png",
            ReportTitle,
            ReportSubtitle ?? StationName,
            null,
            dateRange);
    }

    protected abstract void ComposeContent(IContainer container);

    /// <summary>
    /// Renders a data table with headers and rows.
    /// </summary>
    protected void ComposeDataTable(
        IContainer container,
        string[] headers,
        IEnumerable<string[]> rows,
        float[]? columnWidths = null,
        string? summaryLabel = null,
        string? summaryValue = null)
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
                    var bgColor = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                    foreach (var cell in row)
                    {
                        table.Cell().Background(bgColor).Padding(3)
                            .Text(cell ?? "-").FontSize(7);
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
}
