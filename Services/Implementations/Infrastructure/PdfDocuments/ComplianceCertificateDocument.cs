using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Compliance Certificate - Official clearance document after successful reweigh
/// Certifies that vehicle has achieved compliance with legal weight limits
/// Authorizes vehicle release and resumption of journey
/// Issued under Kenya Traffic Act and EAC Vehicle Load Control Act
/// </summary>
public class ComplianceCertificateDocument : BaseDocument
{
    private readonly WeighingTransaction _reweighing;
    private readonly string _caseNo;
    private readonly string _certificateNo;
    private readonly string _orgLogoFile;

    public ComplianceCertificateDocument(
        WeighingTransaction reweighing,
        string caseNo,
        string certificateNo,
        string? orgLogoFile = null)
    {
        _reweighing = reweighing;
        _caseNo = caseNo;
        _certificateNo = certificateNo;
        _orgLogoFile = ResolveOrgLogo(orgLogoFile);
    }

    public override byte[] Generate()
    {
        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.0f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Inter"));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(8).Element(ComposeContent);
                page.Footer().Element(ComposeOfficialFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        var primaryLogo = LoadLogo(_orgLogoFile);
        var secondaryLogo = LoadLogo(BrandingConstants.Logos.CourtOfArmsKenya);

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
                        .FontSize(11).SemiBold();
                    center.Item().AlignCenter().Text(BrandingConstants.Organization.KenyaRoadsAuthority)
                        .FontSize(9);
                    center.Item().AlignCenter().Text("THE TRAFFIC ACT (CAP. 403)").FontSize(7.5f);
                    center.Item().AlignCenter().Text("EAST AFRICAN COMMUNITY VEHICLE LOAD CONTROL ACT, 2016").FontSize(7.5f);
                });

                row.ConstantItem(LogoWidth).AlignMiddle().Column(logoCol =>
                {
                    if (secondaryLogo != null)
                        logoCol.Item().Height(LogoHeight).Image(secondaryLogo, ImageScaling.FitArea);
                });
            });

            col.Item().PaddingVertical(6).AlignCenter()
                .Background(OfficialGreen)
                .Padding(6)
                .Text("VEHICLE COMPLIANCE CERTIFICATE")
                .FontSize(15)
                .SemiBold()
                .FontColor(Colors.White);

            col.Item().PaddingVertical(5).Row(row =>
            {
                row.RelativeItem().Text($"Certificate No: {_certificateNo}").FontSize(10).SemiBold();
                row.RelativeItem().AlignCenter().Text($"Case No: {_caseNo}").FontSize(10).SemiBold();
                row.RelativeItem().AlignRight().Text($"Issued: {DateTime.UtcNow:dd/MM/yyyy HH:mm}").FontSize(10);
            });

            col.Item().LineHorizontal(1.5f).LineColor(Colors.Black);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(12);

            // Certificate Statement Box
            col.Item().Border(1.5f).BorderColor(OfficialGreen).Background(Colors.Green.Lighten4).Padding(10).Column(cert =>
            {
                cert.Spacing(6);
                cert.Item().AlignCenter().Text("OFFICIAL CERTIFICATION").FontSize(11).SemiBold().FontColor(Colors.Green.Darken3);

                cert.Item().Text(t =>
                {
                    t.Span("This is to certify that the vehicle bearing registration number ");
                    t.Span(_reweighing.VehicleRegNumber ?? "N/A").SemiBold().FontSize(12).FontColor(Colors.Green.Darken3);
                    t.Span(" has been reweighed at an authorized weighbridge and found to be ");
                    t.Span("IN FULL COMPLIANCE").SemiBold().FontColor(Colors.Green.Darken3);
                    t.Span(" with the prescribed weight limits under the Traffic Act (Cap. 403) and the EAC Vehicle Load Control Act, 2016.");
                });
            });

            // Reweigh Details
            col.Item().Text("REWEIGH VERIFICATION DETAILS").FontSize(12).SemiBold().Underline();

            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Spacing(3);
                    left.Item().Text(t =>
                    {
                        t.Span("Weighing Ticket No: ").FontSize(10);
                        t.Span(_reweighing.TicketNumber).SemiBold().FontSize(10);
                    });
                    left.Item().Text(t =>
                    {
                        t.Span("Weighbridge Station: ").FontSize(10);
                        t.Span(_reweighing.Station?.Name ?? "N/A").SemiBold().FontSize(10);
                    });
                    left.Item().Text(t =>
                    {
                        t.Span("Date & Time: ").FontSize(10);
                        t.Span($"{_reweighing.WeighedAt:dd/MM/yyyy HH:mm}").SemiBold().FontSize(10);
                    });
                });

                row.ConstantItem(20);

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(3);
                    right.Item().Text(t =>
                    {
                        t.Span("Vehicle Type: ").FontSize(10);
                        t.Span(_reweighing.Vehicle?.VehicleType ?? "N/A").SemiBold().FontSize(10);
                    });
                    right.Item().Text(t =>
                    {
                        t.Span("Driver: ").FontSize(10);
                        t.Span(_reweighing.Driver?.FullNames ?? "N/A").SemiBold().FontSize(10);
                    });
                    right.Item().Text(t =>
                    {
                        t.Span("Operator ID: ").FontSize(10);
                        t.Span(_reweighing.WeighedByUserId.ToString().Substring(0, 8)).SemiBold().FontSize(10);
                    });
                });
            });

            // Weight Verification Table
            col.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Green.Darken2).Padding(8).Text("Weight Category").SemiBold().FontSize(10).FontColor(Colors.White);
                    header.Cell().Background(Colors.Green.Darken2).Padding(8).AlignCenter().Text("Measured").SemiBold().FontSize(10).FontColor(Colors.White);
                    header.Cell().Background(Colors.Green.Darken2).Padding(8).AlignCenter().Text("Legal Limit").SemiBold().FontSize(10).FontColor(Colors.White);
                    header.Cell().Background(Colors.Green.Darken2).Padding(8).AlignCenter().Text("Variance").SemiBold().FontSize(10).FontColor(Colors.White);
                    header.Cell().Background(Colors.Green.Darken2).Padding(8).AlignCenter().Text("Status").SemiBold().FontSize(10).FontColor(Colors.White);
                });

                // GVW Row
                table.Cell().Background(Colors.Grey.Lighten4).Padding(8).Text("Gross Vehicle Weight (GVW)").FontSize(10).SemiBold();
                table.Cell().Background(Colors.Grey.Lighten4).Padding(8).AlignCenter().Text($"{_reweighing.GvwMeasuredKg:N0} kg").FontSize(10);
                table.Cell().Background(Colors.Grey.Lighten4).Padding(8).AlignCenter().Text($"{_reweighing.GvwPermissibleKg:N0} kg").FontSize(10);
                table.Cell().Background(Colors.Grey.Lighten4).Padding(8).AlignCenter().Text($"{_reweighing.OverloadKg:N0} kg").FontSize(10).FontColor(Colors.Green.Darken2);
                table.Cell().Background(Colors.Grey.Lighten4).Padding(8).AlignCenter().Text("✓ COMPLIANT").FontSize(9).SemiBold().FontColor(Colors.Green.Darken3);
            });

            // Compliance Badge
            col.Item().PaddingTop(10).AlignCenter()
                .Background(Colors.Green.Lighten3)
                .Border(1.5f)
                .BorderColor(Colors.Green.Darken2)
                .Padding(10)
                .Column(badge =>
                {
                    badge.Item().AlignCenter().Text("✓").FontSize(30).FontColor(Colors.Green.Darken3);
                    badge.Item().AlignCenter().Text("COMPLIANCE VERIFIED").FontSize(14).SemiBold().FontColor(Colors.Green.Darken3);
                    badge.Item().AlignCenter().Text("Vehicle meets all legal weight requirements").FontSize(9).Italic();
                });

            // Authorization Statement
            col.Item().PaddingTop(15).Border(1).BorderColor(KuraBlue).Background(Colors.Blue.Lighten4).Padding(10).Column(auth =>
            {
                auth.Item().Text("AUTHORIZATION").FontSize(11).SemiBold().FontColor(KuraBlue);
                auth.Item().Text("The above-named vehicle is hereby AUTHORIZED to proceed with its journey. All prohibitions and restrictions previously imposed are now LIFTED.");
                auth.Item().PaddingTop(5).Text("The driver/owner is reminded to maintain compliance with all traffic and load control regulations during transit.").FontSize(9).Italic();
            });

            // Validity Notice
            col.Item().PaddingTop(10).Background(Colors.Yellow.Lighten3).Border(1).BorderColor(Colors.Yellow.Darken2).Padding(8).Column(validity =>
            {
                validity.Item().Text("VALIDITY NOTICE").FontSize(10).SemiBold().FontColor(Colors.Orange.Darken3);
                validity.Item().PaddingLeft(10).Column(notes =>
                {
                    notes.Spacing(3);
                    notes.Item().Text("• This certificate is valid for the current journey only").FontSize(8);
                    notes.Item().Text("• Any subsequent loading requires new weighing and certification").FontSize(8);
                    notes.Item().Text("• This certificate must be carried in the vehicle and produced on demand").FontSize(8);
                    notes.Item().Text("• Alteration or falsification is a criminal offense").FontSize(8);
                });
            });

            // Signature Blocks
            col.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Authorized Officer"));
                row.ConstantItem(30);
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Station Supervisor"));
            });

            // Official Seal Notice
            col.Item().PaddingTop(15).AlignCenter().Border(1).BorderColor(Colors.Grey.Medium).Padding(10).Column(seal =>
            {
                seal.Item().AlignCenter().Text("OFFICIAL SEAL").FontSize(8).SemiBold();
                seal.Item().AlignCenter().PaddingVertical(20).Text("[STAMP HERE]").FontSize(10).FontColor(Colors.Grey.Medium);
                seal.Item().AlignCenter().Text("Kenya Roads Authority").FontSize(7);
            });

            // Bottom Disclaimer
            col.Item().PaddingTop(10).AlignCenter().Background(Colors.Grey.Lighten4).Padding(5)
                .Text("This is an official government document. Unauthorized reproduction or alteration is prohibited by law.")
                .FontSize(7).Italic();
        });
    }
}
