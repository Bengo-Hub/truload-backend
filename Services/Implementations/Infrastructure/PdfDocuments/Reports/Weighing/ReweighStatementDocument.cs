using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

internal sealed class ReweighStatementDocument : WeighingReportDocumentBase
{
    public int? StatusColumnIndex { get; set; }
    public (string colorHex, string label)[] Legend { get; set; } = [];

    public ReweighStatementDocument()
    {
        ReportTitle = "Reweigh Statement";
        ReportSubtitle = "Load correction and reweigh cycle tracking";
    }

    protected override void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(5);

            if (SummaryItems.Length > 0)
                col.Item().Element(c => ComposeSummaryCards(c, SummaryItems));

            col.Item().Element(c => ComposeDataTable(c, Headers, Rows, conditionalStatusColumnIndex: StatusColumnIndex));
            col.Item().Element(c => ComposeLegend(c, Legend));
        });
    }
}
