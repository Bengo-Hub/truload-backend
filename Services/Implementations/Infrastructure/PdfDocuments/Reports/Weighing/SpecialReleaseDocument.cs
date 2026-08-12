namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

internal sealed class SpecialReleaseDocument : WeighingReportDocumentBase
{
    public SpecialReleaseDocument()
    {
        ReportTitle = "Special Release Register";
        ReportSubtitle = "Special release certificates issued for case dispositions";
    }
}
