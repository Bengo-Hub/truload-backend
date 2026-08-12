namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

internal sealed class ReweighStatementDocument : WeighingReportDocumentBase
{
    public ReweighStatementDocument()
    {
        ReportTitle = "Reweigh Statement";
        ReportSubtitle = "Load correction and reweigh cycle tracking";
    }
}
