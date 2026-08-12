using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

internal sealed class WeighbridgeRegisterDocument : WeighingReportDocumentBase
{
    public int TotalRecords { get; set; }

    public WeighbridgeRegisterDocument()
    {
        ReportTitle = "Weighbridge Register";
        ReportSubtitle = "Detailed record of all weighing transactions";
    }

    protected override void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(5);

            col.Item().PaddingBottom(5).Text($"Total Records: {TotalRecords}")
                .FontSize(9).SemiBold();

            col.Item().Element(c => ComposeDataTable(c, Headers, Rows));
        });
    }
}
