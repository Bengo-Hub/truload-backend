using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Special Release Certificate - Conditional vehicle release document
/// Issued for exceptional circumstances requiring release before full compliance
/// Documents release type, conditions, and authorization chain
/// Legal authority under administrative discretion provisions
/// </summary>
public class SpecialReleaseCertificateDocument : BaseDocument
{
    private readonly SpecialRelease _specialRelease;

    public SpecialReleaseCertificateDocument(SpecialRelease specialRelease)
    {
        _specialRelease = specialRelease;
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
        container.Column(col =>
        {
            col.Item().AlignCenter().Text("REPUBLIC OF KENYA").FontSize(14).SemiBold();
            col.Item().AlignCenter().Text("KENYA ROADS AUTHORITY").FontSize(11);
            col.Item().AlignCenter().Text("VEHICLE LOAD CONTROL UNIT").FontSize(9);

            col.Item().PaddingVertical(10).AlignCenter()
                .Background(Colors.Orange.Darken2)
                .Padding(10)
                .Text("SPECIAL RELEASE CERTIFICATE")
                .FontSize(18)
                .SemiBold()
                .FontColor(Colors.White);

            col.Item().PaddingVertical(5).Row(row =>
            {
                row.RelativeItem().Text($"Certificate No: {_specialRelease.CertificateNo}").FontSize(10).SemiBold();
                row.RelativeItem().AlignCenter().Text($"Case No: {_specialRelease.CaseRegister?.CaseNo ?? "N/A"}").FontSize(10).SemiBold();
                row.RelativeItem().AlignRight().Text($"Issued: {_specialRelease.IssuedAt:dd/MM/yyyy HH:mm}").FontSize(10);
            });

            col.Item().LineHorizontal(1.5f).LineColor(Colors.Black);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(12);

            // Important Notice Banner
            col.Item().Background(Colors.Orange.Lighten3).Border(2).BorderColor(Colors.Orange.Darken2).Padding(10).Column(notice =>
            {
                notice.Item().AlignCenter().Text("⚠ CONDITIONAL RELEASE NOTICE").FontSize(12).SemiBold().FontColor(Colors.Orange.Darken3);
                notice.Item().AlignCenter().Text("This is NOT a compliance certificate. Release is granted under special conditions.").FontSize(9).Italic();
            });

            // Vehicle Information
            col.Item().Text("VEHICLE DETAILS").FontSize(12).SemiBold().Underline();

            // Note: Vehicle details would be loaded from CaseRegister context
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Spacing(3);
                    left.Item().Text(t =>
                    {
                        t.Span("Case Number: ").FontSize(10);
                        t.Span(_specialRelease.CaseRegister?.CaseNo ?? "N/A").SemiBold().FontSize(11);
                    });
                    left.Item().Text(t =>
                    {
                        t.Span("Release Type: ").FontSize(10);
                        t.Span(_specialRelease.ReleaseType?.Name ?? "N/A").SemiBold().FontSize(10);
                    });
                });

                row.ConstantItem(20);

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(3);
                    right.Item().Text(t =>
                    {
                        t.Span("Certificate No: ").FontSize(10);
                        t.Span(_specialRelease.CertificateNo).SemiBold().FontSize(10);
                    });
                    right.Item().Text(t =>
                    {
                        t.Span("Issued At: ").FontSize(10);
                        t.Span(_specialRelease.IssuedAt.ToString("dd/MM/yyyy HH:mm")).SemiBold().FontSize(10);
                    });
                });
            });

            // Release Type and Reason
            col.Item().PaddingTop(10).Border(1).BorderColor(KuraBlue).Padding(10).Column(release =>
            {
                release.Spacing(5);
                release.Item().Text("RELEASE DETAILS").FontSize(11).SemiBold().FontColor(KuraBlue);

                release.Item().Row(row =>
                {
                    row.RelativeItem().Text(t =>
                    {
                        t.Span("Release Type: ").FontSize(10);
                        t.Span(_specialRelease.ReleaseType?.Name ?? "Special Release").SemiBold().FontSize(11).FontColor(KuraBlue);
                    });
                });

                if (_specialRelease.OverloadKg.HasValue)
                {
                    release.Item().Text(t =>
                    {
                        t.Span("Original Overload: ").FontSize(10);
                        t.Span($"{_specialRelease.OverloadKg:N0} kg").SemiBold().FontSize(11).FontColor(Colors.Red.Darken2);
                    });
                }

                release.Item().PaddingTop(5).Column(reason =>
                {
                    reason.Item().Text("Reason for Special Release:").FontSize(10).SemiBold();
                    reason.Item().PaddingLeft(10).Text(_specialRelease.Reason).FontSize(10).Italic();
                });
            });

            // Conditions and Requirements
            col.Item().PaddingTop(10).Column(conditions =>
            {
                conditions.Item().Text("CONDITIONS OF RELEASE").FontSize(11).SemiBold().Underline();

                conditions.Item().PaddingLeft(15).Column(items =>
                {
                    items.Spacing(5);

                    if (_specialRelease.RedistributionAllowed)
                    {
                        ComposeCondition(items.Item(), "1", "REDISTRIBUTION PERMITTED",
                            "The vehicle is authorized to redistribute the load to achieve compliance. Redistribution must be completed under supervision.");
                    }

                    if (_specialRelease.ReweighRequired)
                    {
                        ComposeCondition(items.Item(), "2", "REWEIGH MANDATORY",
                            "The vehicle MUST undergo reweighing after redistribution/offloading to verify compliance before proceeding.");
                    }

                    ComposeCondition(items.Item(), _specialRelease.RedistributionAllowed || _specialRelease.ReweighRequired ? "3" : "1",
                        "ROUTE RESTRICTIONS",
                        "Vehicle must proceed directly to the destination or redistribution point via the authorized route only.");

                    ComposeCondition(items.Item(), _specialRelease.RedistributionAllowed || _specialRelease.ReweighRequired ? "4" : "2",
                        "DOCUMENT RETENTION",
                        "This certificate must be carried in the vehicle at all times and produced on demand to any authorized officer.");

                    ComposeCondition(items.Item(), _specialRelease.RedistributionAllowed || _specialRelease.ReweighRequired ? "5" : "3",
                        "VALIDITY PERIOD",
                        "This release is valid for 24 hours from issuance. Extension requires new authorization.");
                });
            });

            // Legal Obligations Box
            col.Item().PaddingTop(10).Border(1).BorderColor(OfficialRed).Background(Colors.Red.Lighten4).Padding(10).Column(legal =>
            {
                legal.Item().Text("⚠ LEGAL OBLIGATIONS & WARNINGS").FontSize(10).SemiBold().FontColor(Colors.Red.Darken3);
                legal.Item().PaddingLeft(10).Column(obligations =>
                {
                    obligations.Spacing(3);
                    obligations.Item().Text("• Violation of any condition voids this release immediately").FontSize(9);
                    obligations.Item().Text("• The driver/owner remains liable for all overload penalties and fines").FontSize(9);
                    obligations.Item().Text("• This release does not constitute an exemption from prosecution").FontSize(9);
                    obligations.Item().Text("• Misuse or alteration of this document is a criminal offense").FontSize(9);
                    obligations.Item().Text("• Random inspections may be conducted to verify compliance").FontSize(9);
                });
            });

            // Reweigh Status (if applicable)
            if (_specialRelease.ReweighWeighingId.HasValue)
            {
                col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Green.Medium).Background(Colors.Green.Lighten4).Padding(8).Column(reweigh =>
                {
                    reweigh.Item().Text("REWEIGH STATUS").FontSize(10).SemiBold().FontColor(Colors.Green.Darken3);
                    reweigh.Item().Text(t =>
                    {
                        t.Span("Reweigh Completed: ").FontSize(9);
                        t.Span(_specialRelease.ComplianceAchieved ? "✓ YES" : "✗ NO").SemiBold().FontSize(9)
                            .FontColor(_specialRelease.ComplianceAchieved ? Colors.Green.Darken3 : Colors.Red.Darken3);
                    });
                    if (_specialRelease.ComplianceAchieved)
                    {
                        reweigh.Item().Text("Vehicle has achieved compliance and may proceed.").FontSize(8).Italic();
                    }
                });
            }

            // Authorization Section
            col.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Authorized Supervisor"));
                row.ConstantItem(30);
                row.RelativeItem().Column(ack =>
                {
                    ack.Spacing(3);
                    ack.Item().Text("_______________________________").FontSize(9);
                    ack.Item().Text("DRIVER/OWNER ACKNOWLEDGMENT").SemiBold().FontSize(8);
                    ack.Item().Text("I acknowledge receipt of this Special Release Certificate").FontSize(7);
                    ack.Item().Text("and agree to comply with all stated conditions.").FontSize(7);
                    ack.Item().PaddingTop(3).Text("Signature & Date: _______________").FontSize(7);
                });
            });

            // Verification Code
            col.Item().PaddingTop(15).AlignCenter().Border(1).BorderColor(Colors.Grey.Medium).Padding(8).Column(verify =>
            {
                verify.Item().AlignCenter().Text("VERIFICATION CODE").FontSize(8).SemiBold();
                verify.Item().AlignCenter().Text(_specialRelease.Id.ToString().ToUpper().Substring(0, 8)).FontSize(10).SemiBold().FontFamily("Courier New");
                verify.Item().AlignCenter().Text("Use this code to verify authenticity online or via SMS").FontSize(7).Italic();
            });

            // Disclaimer
            col.Item().PaddingTop(10).AlignCenter().Background(Colors.Grey.Lighten4).Padding(5)
                .Text("This certificate is issued under administrative discretion and does not absolve the driver/owner from compliance obligations or legal liability.")
                .FontSize(7).Italic();
        });
    }

    private void ComposeCondition(IContainer container, string number, string title, string description)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(25).Text($"{number}.").SemiBold().FontSize(10);
                row.RelativeItem().Column(content =>
                {
                    content.Item().Text(title).SemiBold().FontSize(10).FontColor(Colors.Orange.Darken3);
                    content.Item().Text(description).FontSize(9);
                });
            });
        });
    }
}
