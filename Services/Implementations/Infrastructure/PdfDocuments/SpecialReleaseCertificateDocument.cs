using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;
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
    private readonly string _orgLogoFile;

    public SpecialReleaseCertificateDocument(SpecialRelease specialRelease, string? orgLogoFile = null)
    {
        _specialRelease = specialRelease;
        _orgLogoFile = ResolveOrgLogo(orgLogoFile);
    }

    public override byte[] Generate()
    {
        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0.8f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(8f).FontFamily("Inter"));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(3).Element(ComposeContent);
                page.Footer().Element(ComposeOfficialFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        var primaryLogo = LoadLogo(_orgLogoFile);

        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(SmallLogoWidth).AlignMiddle().Column(logoCol =>
                {
                    if (primaryLogo != null)
                        logoCol.Item().Height(SmallLogoHeight).Image(primaryLogo, ImageScaling.FitArea);
                });

                row.RelativeItem().AlignCenter().PaddingHorizontal(3).Column(center =>
                {
                    center.Item().AlignCenter().Text($"{BrandingConstants.Organization.RepublicOfKenya} — {BrandingConstants.Organization.KenyaRoadsAuthority}")
                        .FontSize(8).SemiBold();
                    center.Item().AlignCenter().Text("VEHICLE LOAD CONTROL UNIT").FontSize(7f);
                });

                row.ConstantItem(SmallLogoWidth); // No secondary logo for special release
            });

            col.Item().PaddingVertical(2).AlignCenter()
                .Background(Colors.Orange.Darken2)
                .PaddingHorizontal(8).PaddingVertical(3)
                .Text("SPECIAL RELEASE CERTIFICATE")
                .FontSize(12)
                .SemiBold()
                .FontColor(Colors.White);

            col.Item().PaddingVertical(2).Row(row =>
            {
                row.RelativeItem().Text($"Certificate No: {_specialRelease.CertificateNo}").FontSize(8).SemiBold();
                row.RelativeItem().AlignCenter().Text($"Case No: {_specialRelease.CaseRegister?.CaseNo ?? "N/A"}").FontSize(8).SemiBold();
                row.RelativeItem().AlignRight().Text($"Issued: {_specialRelease.IssuedAt:dd/MM/yyyy HH:mm}").FontSize(8);
            });

            col.Item().LineHorizontal(1f).LineColor(Colors.Black);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(4);

            // Important Notice Banner - single line
            col.Item().Background(Colors.Orange.Lighten3).BorderBottom(1).BorderColor(Colors.Orange.Darken2)
                .PaddingHorizontal(6).PaddingVertical(2).Text(t =>
                {
                    t.Span("⚠ CONDITIONAL RELEASE: ").FontSize(7.5f).SemiBold().FontColor(Colors.Orange.Darken3);
                    t.Span("This is NOT a compliance certificate. Release is granted under special conditions.").FontSize(7).Italic().FontColor(Colors.Orange.Darken3);
                });

            // Vehicle Details - compact 2-column
            col.Item().Text("VEHICLE DETAILS").FontSize(9).SemiBold().Underline();
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Spacing(1);
                    left.Item().Text(t =>
                    {
                        t.Span("Vehicle Registration: ").FontSize(8);
                        t.Span(_specialRelease.CaseRegister?.Weighing?.VehicleRegNumber ?? "N/A").SemiBold().FontSize(8);
                    });
                    left.Item().Text(t =>
                    {
                        t.Span("Case Number: ").FontSize(8);
                        t.Span(_specialRelease.CaseRegister?.CaseNo ?? "N/A").SemiBold().FontSize(8);
                    });
                    left.Item().Text(t =>
                    {
                        t.Span("Release Type: ").FontSize(8);
                        t.Span(_specialRelease.ReleaseType?.Name ?? "N/A").SemiBold().FontSize(8);
                    });
                });

                row.ConstantItem(15);

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(1);
                    right.Item().Text(t =>
                    {
                        t.Span("Certificate No: ").FontSize(8);
                        t.Span(_specialRelease.CertificateNo).SemiBold().FontSize(8);
                    });
                    right.Item().Text(t =>
                    {
                        t.Span("Issued At: ").FontSize(8);
                        t.Span(_specialRelease.IssuedAt.ToString("dd/MM/yyyy HH:mm")).SemiBold().FontSize(8);
                    });
                });
            });

            // Release Details - compact box
            col.Item().Border(0.75f).BorderColor(KuraBlue).Padding(5).Column(release =>
            {
                release.Spacing(2);
                release.Item().Text("RELEASE DETAILS").FontSize(8.5f).SemiBold().FontColor(KuraBlue);

                release.Item().Text(t =>
                {
                    t.Span("Release Type: ").FontSize(8);
                    t.Span(_specialRelease.ReleaseType?.Name ?? "Special Release").SemiBold().FontSize(8).FontColor(KuraBlue);
                });

                if (_specialRelease.OverloadKg.HasValue)
                {
                    release.Item().Text(t =>
                    {
                        t.Span("Original Overload: ").FontSize(8);
                        t.Span($"{_specialRelease.OverloadKg:N0} kg").SemiBold().FontSize(8).FontColor(Colors.Red.Darken2);
                    });
                }

                release.Item().Column(reason =>
                {
                    reason.Item().Text(t =>
                    {
                        t.Span("Reason: ").FontSize(8).SemiBold();
                        t.Span(_specialRelease.Reason).FontSize(8).Italic();
                    });
                });
            });

            // Conditions of Release - compact numbered list
            col.Item().Column(conditions =>
            {
                conditions.Item().Text("CONDITIONS OF RELEASE").FontSize(8.5f).SemiBold().Underline();

                conditions.Item().PaddingLeft(8).Column(items =>
                {
                    items.Spacing(2);

                    if (_specialRelease.RedistributionAllowed)
                    {
                        ComposeCondition(items.Item(), "1", "REDISTRIBUTION PERMITTED",
                            "Vehicle authorized to redistribute load under supervision.");
                    }

                    if (_specialRelease.ReweighRequired)
                    {
                        ComposeCondition(items.Item(), "2", "REWEIGH MANDATORY",
                            "Vehicle MUST undergo reweighing after redistribution/offloading before proceeding.");
                    }

                    ComposeCondition(items.Item(), _specialRelease.RedistributionAllowed || _specialRelease.ReweighRequired ? "3" : "1",
                        "ROUTE RESTRICTIONS",
                        "Proceed directly to destination or redistribution point via authorized route only.");

                    ComposeCondition(items.Item(), _specialRelease.RedistributionAllowed || _specialRelease.ReweighRequired ? "4" : "2",
                        "DOCUMENT RETENTION",
                        "Certificate must be carried at all times and produced on demand to any authorized officer.");

                    ComposeCondition(items.Item(), _specialRelease.RedistributionAllowed || _specialRelease.ReweighRequired ? "5" : "3",
                        "VALIDITY PERIOD",
                        "Valid for 24 hours from issuance. Extension requires new authorization.");
                });
            });

            // Legal Obligations - compact gray box
            col.Item().Border(0.75f).BorderColor(OfficialRed).Background(Colors.Red.Lighten4).Padding(4).Column(legal =>
            {
                legal.Item().Text("LEGAL OBLIGATIONS & WARNINGS").FontSize(7.5f).SemiBold().FontColor(Colors.Red.Darken3);
                legal.Item().PaddingLeft(5).Column(obligations =>
                {
                    obligations.Spacing(1);
                    obligations.Item().Text("• Violation of any condition voids this release immediately").FontSize(7);
                    obligations.Item().Text("• Driver/owner remains liable for all overload penalties and fines").FontSize(7);
                    obligations.Item().Text("• This release does not constitute an exemption from prosecution").FontSize(7);
                    obligations.Item().Text("• Misuse or alteration of this document is a criminal offense").FontSize(7);
                    obligations.Item().Text("• Random inspections may be conducted to verify compliance").FontSize(7);
                });
            });

            // Reweigh Status (if applicable) - compact
            if (_specialRelease.ReweighWeighingId.HasValue)
            {
                col.Item().Border(0.75f).BorderColor(Colors.Green.Medium).Background(Colors.Green.Lighten4).PaddingHorizontal(5).PaddingVertical(3).Column(reweigh =>
                {
                    reweigh.Item().Text(t =>
                    {
                        t.Span("REWEIGH STATUS: ").FontSize(7.5f).SemiBold().FontColor(Colors.Green.Darken3);
                        t.Span(_specialRelease.ComplianceAchieved ? "✓ Completed" : "✗ Not Completed").SemiBold().FontSize(7.5f)
                            .FontColor(_specialRelease.ComplianceAchieved ? Colors.Green.Darken3 : Colors.Red.Darken3);
                        if (_specialRelease.ComplianceAchieved)
                            t.Span(" — Vehicle has achieved compliance and may proceed.").FontSize(7).Italic();
                    });
                });
            }

            // Authorization Section - 2-column compact
            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Authorized Supervisor"));
                row.ConstantItem(20);
                row.RelativeItem().Column(ack =>
                {
                    ack.Spacing(2);
                    ack.Item().Text("_______________________________").FontSize(8);
                    ack.Item().Text("DRIVER/OWNER ACKNOWLEDGMENT").SemiBold().FontSize(7);
                    ack.Item().Text("I acknowledge receipt and agree to comply with all stated conditions.").FontSize(6.5f);
                    ack.Item().PaddingTop(2).Text("Signature & Date: _______________").FontSize(6.5f);
                });
            });

            // Verification Code - compact
            col.Item().PaddingTop(4).AlignCenter().Border(0.75f).BorderColor(Colors.Grey.Medium).PaddingHorizontal(6).PaddingVertical(3).Column(verify =>
            {
                verify.Item().AlignCenter().Text(t =>
                {
                    t.Span("VERIFICATION CODE: ").FontSize(7).SemiBold();
                    t.Span(_specialRelease.Id.ToString().ToUpper().Substring(0, 8)).FontSize(8).SemiBold().FontFamily("Courier New");
                    t.Span("  —  Verify authenticity online or via SMS").FontSize(6).Italic();
                });
            });

            // Disclaimer - compact
            col.Item().PaddingTop(2).AlignCenter().Background(Colors.Grey.Lighten4).PaddingHorizontal(4).PaddingVertical(2)
                .Text("This certificate is issued under administrative discretion and does not absolve the driver/owner from compliance obligations or legal liability.")
                .FontSize(6).Italic();
        });
    }

    private void ComposeCondition(IContainer container, string number, string title, string description)
    {
        container.Row(row =>
        {
            row.ConstantItem(15).Text($"{number}.").SemiBold().FontSize(7.5f);
            row.RelativeItem().Text(t =>
            {
                t.Span($"{title}: ").SemiBold().FontSize(7.5f).FontColor(Colors.Orange.Darken3);
                t.Span(description).FontSize(7.5f);
            });
        });
    }
}
