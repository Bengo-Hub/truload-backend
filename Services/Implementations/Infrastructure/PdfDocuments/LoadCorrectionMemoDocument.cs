using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Load Correction Memo - Documents redistribution/offloading process
/// Compares original overload weighing with post-correction reweigh
/// Issued to document compliance efforts and load adjustments
/// </summary>
public class LoadCorrectionMemoDocument : BaseDocument
{
    private readonly WeighingTransaction _originalWeighing;
    private readonly WeighingTransaction _reweighing;
    private readonly string _caseNo;

    public LoadCorrectionMemoDocument(
        WeighingTransaction originalWeighing,
        WeighingTransaction reweighing,
        string caseNo)
    {
        _originalWeighing = originalWeighing;
        _reweighing = reweighing;
        _caseNo = caseNo;
    }

    public override byte[] Generate()
    {
        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(15).Element(ComposeContent);
                page.Footer().Element(ComposeOfficialFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        var primaryLogo = LoadLogo(BrandingConstants.Logos.KuraLogo);

        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(LogoWidth).AlignMiddle().Column(logoCol =>
                {
                    if (primaryLogo != null)
                        logoCol.Item().Height(LogoHeight).Image(primaryLogo, ImageScaling.FitArea);
                });

                row.RelativeItem().AlignCenter().PaddingHorizontal(5).Column(center =>
                {
                    center.Item().AlignCenter().Text(BrandingConstants.Organization.RepublicOfKenya)
                        .FontSize(14).SemiBold();
                    center.Item().AlignCenter().Text(BrandingConstants.Organization.KenyaRoadsAuthority)
                        .FontSize(11);
                });

                row.ConstantItem(LogoWidth); // No secondary logo for memo
            });

            col.Item().PaddingVertical(10).AlignCenter()
                .Background(KuraBlue)
                .Padding(10)
                .Text("LOAD CORRECTION MEMO")
                .FontSize(18)
                .SemiBold()
                .FontColor(Colors.White);

            col.Item().PaddingVertical(5).Row(row =>
            {
                row.RelativeItem().Text($"Case No: {_caseNo}").FontSize(10).SemiBold();
                row.RelativeItem().AlignRight().Text($"Date: {DateTime.UtcNow:dd/MM/yyyy HH:mm}").FontSize(10);
            });

            col.Item().LineHorizontal(1.5f).LineColor(Colors.Black);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(12);

            // Vehicle information
            col.Item().Text(t =>
            {
                t.Span("VEHICLE REGISTRATION: ").SemiBold();
                t.Span(_originalWeighing.VehicleRegNumber ?? "N/A").FontSize(12);
            });

            // Purpose statement
            col.Item().Text("This memo documents the load correction process undertaken to achieve compliance with legal weight limits.");

            // Original Violation Section
            col.Item().Border(1).BorderColor(Colors.Red.Medium).Padding(10).Column(original =>
            {
                original.Spacing(5);
                original.Item().Text("ORIGINAL VIOLATION").FontSize(12).SemiBold().FontColor(Colors.Red.Darken2);

                original.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Weighing Date: {_originalWeighing.WeighedAt:dd/MM/yyyy HH:mm}").FontSize(10);
                    row.RelativeItem().Text($"Ticket No: {_originalWeighing.TicketNumber}").FontSize(10);
                });

                original.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Measurement").SemiBold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("Measured").SemiBold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("Legal Limit").SemiBold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("Overload").SemiBold().FontSize(9);
                    });

                    // GVW Row
                    table.Cell().Padding(5).Text("Gross Vehicle Weight (GVW)").FontSize(9);
                    table.Cell().Padding(5).AlignCenter().Text($"{_originalWeighing.GvwMeasuredKg:N0} kg").FontSize(9);
                    table.Cell().Padding(5).AlignCenter().Text($"{_originalWeighing.GvwPermissibleKg:N0} kg").FontSize(9);
                    table.Cell().Padding(5).AlignCenter().Text($"{_originalWeighing.OverloadKg:N0} kg").FontColor(Colors.Red.Darken2).SemiBold().FontSize(9);
                });

                original.Item().PaddingTop(5).Text($"Control Status: {_originalWeighing.ControlStatus}").FontSize(9).Italic();
            });

            // Corrective Action Section
            col.Item().Border(1).BorderColor(Colors.Orange.Medium).Background(Colors.Orange.Lighten4).Padding(10).Column(action =>
            {
                action.Item().Text("CORRECTIVE ACTION TAKEN").FontSize(11).SemiBold().FontColor(Colors.Orange.Darken3);
                action.Item().PaddingLeft(10).Column(items =>
                {
                    items.Spacing(3);
                    items.Item().Text("• Vehicle moved to designated holding area").FontSize(9);
                    items.Item().Text("• Load redistributed/offloaded under supervision").FontSize(9);
                    items.Item().Text("• Reweighing conducted to verify compliance").FontSize(9);
                });
            });

            // Reweigh Results Section
            col.Item().Border(1).BorderColor(Colors.Green.Medium).Padding(10).Column(reweigh =>
            {
                reweigh.Spacing(5);
                reweigh.Item().Text("REWEIGH RESULTS").FontSize(12).SemiBold().FontColor(Colors.Green.Darken2);

                reweigh.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Reweigh Date: {_reweighing.WeighedAt:dd/MM/yyyy HH:mm}").FontSize(10);
                    row.RelativeItem().Text($"Ticket No: {_reweighing.TicketNumber}").FontSize(10);
                });

                reweigh.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1);
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Measurement").SemiBold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("Measured").SemiBold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("Legal Limit").SemiBold().FontSize(9);
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignCenter().Text("Variance").SemiBold().FontSize(9);
                    });

                    // GVW Row
                    table.Cell().Padding(5).Text("Gross Vehicle Weight (GVW)").FontSize(9);
                    table.Cell().Padding(5).AlignCenter().Text($"{_reweighing.GvwMeasuredKg:N0} kg").FontSize(9);
                    table.Cell().Padding(5).AlignCenter().Text($"{_reweighing.GvwPermissibleKg:N0} kg").FontSize(9);

                    var isCompliant = _reweighing.OverloadKg <= 0;
                    table.Cell().Padding(5).AlignCenter().Text($"{_reweighing.OverloadKg:N0} kg")
                        .FontColor(isCompliant ? Colors.Green.Darken2 : Colors.Red.Darken2)
                        .SemiBold()
                        .FontSize(9);
                });

                // Compliance Status Badge
                var compliant = _reweighing.OverloadKg <= 0;
                reweigh.Item().PaddingTop(8).AlignCenter()
                    .Background(compliant ? Colors.Green.Lighten3 : Colors.Red.Lighten3)
                    .Padding(8)
                    .Text(compliant ? "✓ COMPLIANCE ACHIEVED" : "✗ STILL NON-COMPLIANT")
                    .FontSize(12)
                    .SemiBold()
                    .FontColor(compliant ? Colors.Green.Darken3 : Colors.Red.Darken3);
            });

            // Load Reduction Summary
            var loadReduced = _originalWeighing.GvwMeasuredKg - _reweighing.GvwMeasuredKg;
            col.Item().Background(Colors.Blue.Lighten4).Padding(8).Row(row =>
            {
                row.RelativeItem().Text("TOTAL LOAD REDUCED:").FontSize(10).SemiBold();
                row.ConstantItem(150).AlignRight().Text($"{loadReduced:N0} kg").FontSize(12).SemiBold().FontColor(KuraBlue);
            });

            // Officer Certification
            col.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Supervising Officer"));
                row.ConstantItem(20);
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Driver/Owner Acknowledgment"));
            });

            // Bottom notice
            col.Item().PaddingTop(10).AlignCenter().Background(Colors.Grey.Lighten4).Padding(5)
                .Text("This memo is issued for record purposes and case disposition determination.")
                .FontSize(8).Italic();
        });
    }
}
