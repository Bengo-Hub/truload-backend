using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

/// <summary>
/// Base for all weighing report PDF documents that follow the standard
/// summary cards + data table pattern.
/// </summary>
internal abstract class WeighingReportDocumentBase : BaseReportDocument
{
    public string[] Headers { get; set; } = [];
    public string[][] Rows { get; set; } = [];
    public (string label, string value)[] SummaryItems { get; set; } = [];

    protected override void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(5);

            if (SummaryItems.Length > 0)
                col.Item().Element(c => ComposeSummaryCards(c, SummaryItems));

            col.Item().Element(c => ComposeDataTable(c, Headers, Rows));
        });
    }
}
