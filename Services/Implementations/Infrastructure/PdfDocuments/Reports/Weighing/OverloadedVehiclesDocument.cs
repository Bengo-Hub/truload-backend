namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

internal sealed class OverloadedVehiclesDocument : WeighingReportDocumentBase
{
    public OverloadedVehiclesDocument()
    {
        ReportTitle = "Overloaded Vehicles Register";
        ReportSubtitle = "Vehicles exceeding permissible gross vehicle weight";
    }
}
