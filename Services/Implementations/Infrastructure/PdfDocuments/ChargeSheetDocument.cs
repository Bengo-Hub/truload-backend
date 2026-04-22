using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;
using TruLoad.Backend.Models.Prosecution;
using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Charge Sheet - Official prosecution charge document
/// Compliant with Kenya Traffic Act Cap 403 and EAC Vehicle Load Control Act 2016
/// </summary>
public class ChargeSheetDocument : BaseDocument
{
    private readonly ProsecutionCase _prosecution;
    private readonly CaseRegister? _caseRegister;

    public ChargeSheetDocument(ProsecutionCase prosecution, CaseRegister? caseRegister = null)
    {
        _prosecution = prosecution;
        _caseRegister = caseRegister ?? prosecution.CaseRegister;
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
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Inter"));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(8).Element(ComposeContent);
                page.Footer().Element(ComposeOfficialFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        // Select logo based on legal framework: EAC Act vs Traffic Act
        var isEac = _prosecution.Act?.Name?.Contains("EAC", StringComparison.OrdinalIgnoreCase) ?? false;
        var primaryLogoFile = isEac ? BrandingConstants.Logos.EacActLogo : BrandingConstants.Logos.KenyaPoliceLogo;

        ComposeOfficialHeaderWithLogos(
            container,
            primaryLogoFile,
            BrandingConstants.Logos.JudicialLogo,
            "CHARGE SHEET",
            subtitle: "IN THE TRAFFIC COURT",
            referenceNumber: $"Cert No: {_prosecution.CertificateNo} | Case No: {_caseRegister?.CaseNo ?? "N/A"}",
            dateText: $"Date: {_prosecution.CreatedAt:dd/MM/yyyy}");
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(8);

            // Vehicle Information Section
            col.Item().Text("1. VEHICLE INFORMATION").FontSize(9.5f).SemiBold();
            col.Item().PaddingLeft(10).Column(vehicle =>
            {
                vehicle.Spacing(2);
                ComposeInfoRow(vehicle.Item(), "Reg No", _prosecution.Weighing?.VehicleRegNumber ?? "N/A", true);
                ComposeInfoRow(vehicle.Item(), "Ticket No", _prosecution.Weighing?.TicketNumber ?? "N/A");
                ComposeInfoRow(vehicle.Item(), "Weighed At", _prosecution.Weighing?.WeighedAt.ToString("dd/MM/yyyy HH:mm") ?? "N/A");
            });

            // Offense Details Section
            col.Item().PaddingTop(4).Text("2. OFFENSE DETAILS").FontSize(9.5f).SemiBold();
            col.Item().PaddingLeft(10).Column(offense =>
            {
                offense.Spacing(2);
                ComposeInfoRow(offense.Item(), "Offense Date", _prosecution.Weighing?.WeighedAt.ToString("dd/MM/yyyy HH:mm") ?? "N/A");
                ComposeInfoRow(offense.Item(), "Legal Framework", _prosecution.Act?.Name ?? "Kenya Traffic Act Cap 403");
                ComposeInfoRow(offense.Item(), "Charge Basis", _prosecution.BestChargeBasis == "gvw" ? "Gross Vehicle Weight" : "Axle Weight");
            });

            // Charge Statement
            col.Item().PaddingTop(6).Border(1).BorderColor(Colors.Black).Padding(8).Column(charge =>
            {
                charge.Item().Text("STATEMENT OF CHARGE").FontSize(9.5f).SemiBold().FontColor(OfficialRed);
                charge.Item().PaddingTop(3).Text(ComposeChargeStatement()).FontSize(9);
            });

            // Weighing Evidence Summary
            col.Item().PaddingTop(6).Text("3. WEIGHING EVIDENCE SUMMARY").FontSize(9.5f).SemiBold();
            col.Item().Element(ComposeWeighingEvidenceTable);

            // Charge Calculation Summary
            col.Item().PaddingTop(6).Text("4. CHARGE CALCULATION").FontSize(9.5f).SemiBold();
            col.Item().Element(ComposeChargeTable);

            // Officer Section
            col.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Prosecution Officer", _prosecution.ProsecutionOfficer?.FullName));
                row.ConstantItem(40);
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Accused/Representative"));
            });

            // Notes
            if (!string.IsNullOrEmpty(_prosecution.CaseNotes))
            {
                col.Item().PaddingTop(15).Column(notes =>
                {
                    notes.Item().Text("ADDITIONAL NOTES:").FontSize(9).SemiBold();
                    notes.Item().PaddingLeft(10).Text(_prosecution.CaseNotes).FontSize(9).Italic();
                });
            }
        });
    }

    private string ComposeChargeStatement()
    {
        var bestBasis = _prosecution.BestChargeBasis == "gvw" ? "Gross Vehicle Weight (GVW)" : "Axle Weight";
        var overloadKg = _prosecution.BestChargeBasis == "gvw" ? _prosecution.GvwOverloadKg : _prosecution.MaxAxleOverloadKg;

        return $"That on {_prosecution.Weighing?.WeighedAt.ToString("dd MMMM yyyy") ?? "the stated date"}, " +
               $"Motor Vehicle Registration Number {_prosecution.Weighing?.VehicleRegNumber ?? "N/A"} was " +
               $"overloaded by {overloadKg:N0} kg ({bestBasis}), contrary to Section 58(1) of the " +
               $"{_prosecution.Act?.Name ?? "Kenya Traffic Act Cap 403"}.";
    }

    private void ComposeWeighingEvidenceTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            // Header
            table.Header(header =>
            {
                header.Cell().Element(HeaderStyle).Text("Measurement");
                header.Cell().Element(HeaderStyle).Text("Measured (kg)");
                header.Cell().Element(HeaderStyle).Text("Legal Limit (kg)");
                header.Cell().Element(HeaderStyle).Text("Overload (kg)");

                static IContainer HeaderStyle(IContainer c) =>
                    c.DefaultTextStyle(x => x.SemiBold().FontSize(9))
                     .Background(Colors.Grey.Lighten3)
                     .PaddingVertical(4)
                     .PaddingHorizontal(3)
                     .BorderBottom(1)
                     .BorderColor(Colors.Black);
            });

            // GVW Row
            table.Cell().Element(CellStyle).Text("Gross Vehicle Weight");
            table.Cell().Element(CellStyle).Text($"{_prosecution.Weighing?.GvwMeasuredKg:N0}");
            table.Cell().Element(CellStyle).Text($"{_prosecution.Weighing?.GvwPermissibleKg:N0}");
            table.Cell().Element(CellStyle).Text($"{_prosecution.GvwOverloadKg:N0}").FontColor(_prosecution.GvwOverloadKg > 0 ? Colors.Red.Medium : Colors.Green.Medium);

            // Max Axle Row
            table.Cell().Element(CellStyle).Text("Maximum Axle Overload");
            table.Cell().Element(CellStyle).Text("-");
            table.Cell().Element(CellStyle).Text("-");
            table.Cell().Element(CellStyle).Text($"{_prosecution.MaxAxleOverloadKg:N0}").FontColor(_prosecution.MaxAxleOverloadKg > 0 ? Colors.Red.Medium : Colors.Green.Medium);

            static IContainer CellStyle(IContainer c) =>
                c.PaddingVertical(3)
                 .PaddingHorizontal(3)
                 .BorderBottom(0.5f)
                 .BorderColor(Colors.Grey.Lighten2)
                 .DefaultTextStyle(t => t.FontSize(9));
        });
    }

    private void ComposeChargeTable(IContainer container)
    {
        // Traffic Act charges natively in KES — suppress USD column
        var isTrafficAct = string.Equals(_prosecution.Act?.ChargingCurrency, "KES", StringComparison.OrdinalIgnoreCase)
                        || (_prosecution.Act == null); // default to KES when act not resolved
        var perPartyKes = _prosecution.TotalFeeKes / 2;
        var perPartyUsd = _prosecution.TotalFeeUsd / 2;
        var basisLabel = _prosecution.BestChargeBasis == "gvw" ? "GVW" : "Axle";

        container.Column(col =>
        {
            col.Item().Border(1).BorderColor(Colors.Black).Table(table =>
            {
                if (isTrafficAct)
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderStyle).Text("Description");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("Amount (KES)");
                        static IContainer HeaderStyle(IContainer c) =>
                            c.DefaultTextStyle(x => x.SemiBold().FontSize(9))
                             .Background(Colors.Grey.Lighten3).PaddingVertical(4).PaddingHorizontal(5)
                             .BorderBottom(1).BorderColor(Colors.Black);
                    });

                    table.Cell().Element(CellStyle).Text($"GVW Overload Fee ({_prosecution.GvwOverloadKg:N0} kg)");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_prosecution.GvwFeeKes:N2}");

                    table.Cell().Element(CellStyle).Text($"Axle Overload Fee ({_prosecution.MaxAxleOverloadKg:N0} kg)");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_prosecution.MaxAxleFeeKes:N2}");

                    table.Cell().Element(CellStyle).Text($"Charge Basis: {basisLabel} (higher of the two)").Italic();
                    table.Cell().Element(CellStyle).Text("");

                    if (_prosecution.PenaltyMultiplier > 1)
                    {
                        table.Cell().Element(CellStyle).Text($"Repeat Offender (Conviction #{_prosecution.OffenseCount + 1})").FontColor(OfficialRed);
                        table.Cell().Element(CellStyle).Text("");
                    }

                    // Joint liability breakdown
                    table.Cell().ColumnSpan(2).Element(SeparatorStyle)
                        .Text("JOINT AND SEVERAL LIABILITY — s.58(1) Traffic Act Cap 403").FontSize(8).SemiBold().FontColor(OfficialRed);

                    table.Cell().Element(CellStyle).Text("Driver's Fine");
                    table.Cell().Element(CellStyle).AlignRight().Text($"KES {perPartyKes:N2}");

                    table.Cell().Element(CellStyle).Text("Registered Owner's Fine");
                    table.Cell().Element(CellStyle).AlignRight().Text($"KES {perPartyKes:N2}");

                    table.Cell().Element(TotalStyle).Text("TOTAL COMBINED CHARGE");
                    table.Cell().Element(TotalStyle).AlignRight().Text($"KES {_prosecution.TotalFeeKes:N2}");
                }
                else
                {
                    // EAC — show both USD and KES
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderStyle).Text("Description");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("Amount (USD)");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("Amount (KES)");
                        static IContainer HeaderStyle(IContainer c) =>
                            c.DefaultTextStyle(x => x.SemiBold().FontSize(9))
                             .Background(Colors.Grey.Lighten3).PaddingVertical(4).PaddingHorizontal(5)
                             .BorderBottom(1).BorderColor(Colors.Black);
                    });

                    table.Cell().Element(CellStyle).Text($"GVW Overload Fee ({_prosecution.GvwOverloadKg:N0} kg)");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_prosecution.GvwFeeUsd:N2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_prosecution.GvwFeeKes:N2}");

                    table.Cell().Element(CellStyle).Text($"Axle Overload Fee ({_prosecution.MaxAxleOverloadKg:N0} kg)");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_prosecution.MaxAxleFeeUsd:N2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{_prosecution.MaxAxleFeeKes:N2}");

                    table.Cell().Element(CellStyle).Text($"Charge Basis: {basisLabel} (higher of the two)").Italic();
                    table.Cell().Element(CellStyle).Text(""); table.Cell().Element(CellStyle).Text("");

                    if (_prosecution.PenaltyMultiplier > 1)
                    {
                        table.Cell().Element(CellStyle).Text($"Repeat Offender (Conviction #{_prosecution.OffenseCount + 1})").FontColor(OfficialRed);
                        table.Cell().Element(CellStyle).Text(""); table.Cell().Element(CellStyle).Text("");
                    }

                    // Joint liability breakdown
                    table.Cell().ColumnSpan(3).Element(SeparatorStyle)
                        .Text("JOINT AND SEVERAL LIABILITY — EAC Vehicle Load Control Act").FontSize(8).SemiBold().FontColor(OfficialRed);

                    table.Cell().Element(CellStyle).Text("Driver's Fine");
                    table.Cell().Element(CellStyle).AlignRight().Text($"USD {perPartyUsd:N2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"KES {perPartyKes:N2}");

                    table.Cell().Element(CellStyle).Text("Registered Owner's Fine");
                    table.Cell().Element(CellStyle).AlignRight().Text($"USD {perPartyUsd:N2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"KES {perPartyKes:N2}");

                    table.Cell().Element(TotalStyle).Text("TOTAL COMBINED CHARGE");
                    table.Cell().Element(TotalStyle).AlignRight().Text($"USD {_prosecution.TotalFeeUsd:N2}");
                    table.Cell().Element(TotalStyle).AlignRight().Text($"KES {_prosecution.TotalFeeKes:N2}");
                }

                static IContainer CellStyle(IContainer c) =>
                    c.PaddingVertical(3).PaddingHorizontal(5)
                     .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                     .DefaultTextStyle(t => t.FontSize(9));

                static IContainer SeparatorStyle(IContainer c) =>
                    c.PaddingVertical(3).PaddingHorizontal(5)
                     .Background(Colors.Red.Lighten5)
                     .DefaultTextStyle(t => t.FontSize(8));

                static IContainer TotalStyle(IContainer c) =>
                    c.PaddingVertical(5).PaddingHorizontal(5)
                     .Background(Colors.Grey.Lighten4)
                     .DefaultTextStyle(t => t.FontSize(10).SemiBold());
            });

            if (!isTrafficAct)
                col.Item().PaddingTop(3).Text($"Exchange Rate: 1 USD = {_prosecution.ForexRate:N2} KES")
                    .FontSize(8).Italic();
        });
    }
}
