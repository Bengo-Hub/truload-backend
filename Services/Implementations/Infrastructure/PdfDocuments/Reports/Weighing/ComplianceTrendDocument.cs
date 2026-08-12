namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

internal sealed class ComplianceTrendDocument : WeighingReportDocumentBase
{
    public ComplianceTrendDocument()
    {
        ReportTitle = "Compliance Trend Analysis";
        ReportSubtitle = "Daily compliance rates over the reporting period";
    }
}
