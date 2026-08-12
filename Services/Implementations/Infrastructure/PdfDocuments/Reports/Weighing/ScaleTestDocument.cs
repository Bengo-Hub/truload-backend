namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

internal sealed class ScaleTestDocument : WeighingReportDocumentBase
{
    public ScaleTestDocument()
    {
        ReportTitle = "Scale Test Log";
        ReportSubtitle = "Daily scale calibration tests and results";
    }
}
