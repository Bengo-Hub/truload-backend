namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

internal sealed class StationPerformanceDocument : WeighingReportDocumentBase
{
    public StationPerformanceDocument()
    {
        ReportTitle = "Station Performance Report";
        ReportSubtitle = "Comparative performance across weighbridge stations";
    }
}
