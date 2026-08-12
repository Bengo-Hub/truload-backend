namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

internal sealed class TransporterStatementDocument : WeighingReportDocumentBase
{
    public TransporterStatementDocument()
    {
        ReportTitle = "Transporter Statement";
        ReportSubtitle = "Weighing history and compliance summary by transporter";
    }
}
