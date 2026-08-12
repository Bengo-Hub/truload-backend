using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

internal sealed class OverloadedVehiclesDocument : WeighingReportDocumentBase
{
    public int? StatusColumnIndex { get; set; }
    public (string colorHex, string label)[] Legend { get; set; } = [];

    public OverloadedVehiclesDocument()
    {
        ReportTitle = "Overloaded Vehicles Register";
        ReportSubtitle = "Vehicles exceeding permissible gross vehicle weight";
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
