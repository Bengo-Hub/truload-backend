namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

internal sealed class DailySummaryDocument : WeighingReportDocumentBase
{
    public DailySummaryDocument()
    {
        ReportTitle = "Daily Weighing Summary";
        ReportSubtitle = "Aggregated weighing statistics by date and station";
    }
}
