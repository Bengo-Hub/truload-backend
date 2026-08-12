namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

internal sealed class AxleOverloadDocument : WeighingReportDocumentBase
{
    public AxleOverloadDocument()
    {
        ReportTitle = "Axle Overload Analysis";
        ReportSubtitle = "Breakdown of overloaded axles by type and configuration";
    }
}
