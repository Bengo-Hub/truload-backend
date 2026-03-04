using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Weight Ticket - Official weighing certificate (Portrait A4, KeNHA-style layout)
/// Compliant with Kenya Traffic Act Cap 403 and EAC Vehicle Load Control Act 2016
/// </summary>
public class WeightTicketDocument : BaseDocument
{
    private readonly WeighingTransaction _transaction;
    private readonly string? _organizationName;

    public WeightTicketDocument(WeighingTransaction transaction, string? organizationName = null)
    {
        _transaction = transaction;
        _organizationName = organizationName;
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
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Inter"));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(5).Element(ComposeContent);
                page.Footer().Element(ComposeOfficialFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        ComposeOfficialHeaderWithLogos(
            container,
            BrandingConstants.Logos.KuraLogo,
            BrandingConstants.Logos.CourtOfArmsKenya,
            "WEIGHT CERTIFICATE",
            subtitle: _transaction.Station?.Name ?? "Weighbridge Station",
            referenceNumber: $"Ticket No: {_transaction.TicketNumber}",
            dateText: $"Date: {_transaction.WeighedAt:dd/MM/yyyy HH:mm}",
            organizationName: _organizationName);
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(6);

            // Status indicator with image
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(statusCol =>
                {
                    statusCol.Item().Text("WEIGHING RESULT").FontSize(10).SemiBold();
                    if (!_transaction.IsCompliant && _transaction.OverloadKg > 0)
                    {
                        statusCol.Item().PaddingTop(2).Text(
                            $"Vehicle overloaded by {_transaction.OverloadKg:N0} kg. Remedial action required.")
                            .FontSize(9).FontColor(OfficialRed).SemiBold();
                    }
                    else
                    {
                        statusCol.Item().PaddingTop(2).Text("Vehicle is within permissible weight limits.")
                            .FontSize(9).FontColor(OfficialGreen).SemiBold();
                    }
                });
                row.ConstantItem(80).AlignCenter().Element(c =>
                    ComposeStatusImage(c, _transaction.ControlStatus));
            });

            // Vehicle Information
            col.Item().Element(ComposeVehicleInfo);

            // Axle Weight Details Table (KeNHA style)
            col.Item().Element(ComposeAxleTable);

            // GVW Summary Box
            col.Item().Element(ComposeGvwSummary);

            // Permit Information
            if (_transaction.HasPermit)
            {
                col.Item().Background("#EFF6FF").Padding(6).DefaultTextStyle(x => x.FontSize(9)).Text(text =>
                {
                    text.Span("Permit: ").SemiBold();
                    text.Span("Vehicle has a valid permit.");
                });
            }

            // Remedial Action (for overloaded vehicles)
            if (!_transaction.IsCompliant && _transaction.OverloadKg > 0)
            {
                col.Item().Background("#FEF2F2").Border(1).BorderColor(OfficialRed).Padding(6).Column(rem =>
                {
                    rem.Item().Text("REMEDIAL ACTION REQUIRED").FontSize(10).Bold().FontColor(OfficialRed);
                    rem.Item().PaddingTop(3).Text(
                        $"Excess Gross Vehicle Weight of {_transaction.OverloadKg:N0} KG detected. " +
                        $"Redistribute or offload {_transaction.OverloadKg:N0} KG before proceeding.")
                        .FontSize(9);
                });
            }

            // Signature Blocks
            col.Item().PaddingTop(15).Element(ComposeSignatures);

            // Legal Note
            col.Item().PaddingTop(8).Element(ComposeLegalNote);
        });
    }

    private void ComposeVehicleInfo(IContainer container)
    {
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Column(col =>
        {
            col.Item().Background("#F0F4F8").Padding(4)
                .Text("VEHICLE & TRANSPORT INFORMATION").FontSize(9).SemiBold();

            col.Item().Padding(6).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                ComposeInfoCell(table, "Vehicle Registration", _transaction.VehicleRegNumber, true);
                ComposeInfoCell(table, "Vehicle Type", _transaction.Vehicle?.VehicleType ?? "N/A");
                ComposeInfoCell(table, "Vehicle Make", _transaction.Vehicle?.Make ?? "N/A");
                ComposeInfoCell(table, "Axle Configuration", GetAxleConfigurationDisplay());
                ComposeInfoCell(table, "Driver", _transaction.Driver?.FullNames ?? "N/A");
                ComposeInfoCell(table, "Transporter", _transaction.Transporter?.Name ?? "N/A");
                ComposeInfoCell(table, "Origin", _transaction.Origin?.Name ?? "N/A");
                ComposeInfoCell(table, "Destination", _transaction.Destination?.Name ?? "N/A");
                ComposeInfoCell(table, "Cargo", _transaction.Cargo?.Name ?? "N/A");
                ComposeInfoCell(table, "Weighing Type", _transaction.WeighingType?.ToUpperInvariant() ?? "N/A");
                ComposeInfoCell(table, "Bound", _transaction.Bound ?? "N/A");
                ComposeInfoCell(table, "Station Code", _transaction.Station?.Code ?? "N/A");
                ComposeInfoCell(table, "Location (Road)", _transaction.Road != null ? $"{_transaction.Road.Code} – {_transaction.Road.Name}" : (_transaction.LocationTown ?? "N/A"));
                ComposeInfoCell(table, "Town/City", _transaction.LocationTown ?? "N/A");
                ComposeInfoCell(table, "County", _transaction.LocationCounty ?? "N/A");
            });
        });
    }

    /// <summary>
    /// Axle configuration for display: from vehicle when set, otherwise from first weighing axle (e.g. mobile flow where vehicle is auto-created without config).
    /// </summary>
    private string GetAxleConfigurationDisplay()
    {
        var fromVehicle = _transaction.Vehicle?.AxleConfiguration?.AxleCode;
        if (!string.IsNullOrEmpty(fromVehicle))
            return fromVehicle;
        var fromAxle = _transaction.WeighingAxles?
            .OrderBy(a => a.AxleNumber)
            .Select(a => a.AxleConfiguration?.AxleCode)
            .FirstOrDefault(ac => !string.IsNullOrEmpty(ac));
        return fromAxle ?? "N/A";
    }

    private string FeeColumnHeader()
    {
        var currency = _transaction.Act?.ChargingCurrency ?? "KES";
        return $"Fee ({currency})";
    }

    private static void ComposeInfoCell(TableDescriptor table, string label, string value, bool bold = false)
    {
        table.Cell().PaddingVertical(2).Row(row =>
        {
            row.ConstantItem(120).Text(label + ":").FontSize(8).SemiBold().FontColor("#4B5563");
            if (bold)
                row.RelativeItem().Text(value).FontSize(9).Bold();
            else
                row.RelativeItem().Text(value).FontSize(9);
        });
    }

    private void ComposeAxleTable(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Background("#F0F4F8").Padding(4)
                .Text("AXLE WEIGHT MEASUREMENTS (KG)").FontSize(9).SemiBold();

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(35);   // #
                    columns.RelativeColumn(1.2f);  // Axle Type
                    columns.ConstantColumn(50);    // Tyre
                    columns.RelativeColumn();       // Permissible
                    columns.RelativeColumn();       // Actual
                    columns.RelativeColumn();       // Overload
                    columns.ConstantColumn(55);    // Result
                    columns.ConstantColumn(45);    // PDF
                    columns.ConstantColumn(55);    // Fee
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Element(HeaderStyle).Text("#");
                    header.Cell().Element(HeaderStyle).Text("Axle Type");
                    header.Cell().Element(HeaderStyle).Text("Tyre");
                    header.Cell().Element(HeaderStyle).Text("Permissible");
                    header.Cell().Element(HeaderStyle).Text("Actual");
                    header.Cell().Element(HeaderStyle).Text("Overload");
                    header.Cell().Element(HeaderStyle).Text("Result");
                    header.Cell().Element(HeaderStyle).Text("PDF");
                    header.Cell().Element(HeaderStyle).Text(FeeColumnHeader());

                    static IContainer HeaderStyle(IContainer c) =>
                        c.DefaultTextStyle(x => x.SemiBold().FontSize(7).FontColor("#1F2937"))
                         .PaddingVertical(4).PaddingHorizontal(2)
                         .Background("#E5E7EB")
                         .BorderBottom(1).BorderColor(Colors.Black);
                });

                // Axle rows
                var axles = _transaction.WeighingAxles.OrderBy(a => a.AxleNumber).ToList();
                for (var i = 0; i < axles.Count; i++)
                {
                    var axle = axles[i];
                    var isEven = i % 2 == 0;
                    var overload = axle.OverloadKg;
                    var isOverloaded = overload > 0;

                    table.Cell().Element(c => CellStyle(c, isEven)).Text($"{axle.AxleNumber}");
                    table.Cell().Element(c => CellStyle(c, isEven)).Text(axle.AxleType ?? "N/A");
                    table.Cell().Element(c => CellStyle(c, isEven)).Text(axle.TyreType?.Code ?? "N/A");
                    table.Cell().Element(c => CellStyle(c, isEven)).Text($"{axle.PermissibleWeightKg:N0}");
                    table.Cell().Element(c => CellStyle(c, isEven)).Text($"{axle.MeasuredWeightKg:N0}").SemiBold();

                    // Overload column - colored
                    var overloadCell = table.Cell().Element(c => CellStyle(c, isEven));
                    if (isOverloaded)
                        overloadCell.Text($"+{overload:N0}").FontColor(OfficialRed).SemiBold();
                    else
                        overloadCell.Text("0");

                    // Result column - pass/fail with color
                    var resultCell = table.Cell().Element(c => CellStyle(c, isEven));
                    if (isOverloaded)
                        resultCell.Text("FAIL").FontColor(OfficialRed).SemiBold();
                    else
                        resultCell.Text("PASS").FontColor(OfficialGreen).SemiBold();

                    // PDF (Pavement Damage Factor)
                    table.Cell().Element(c => CellStyle(c, isEven))
                        .Text($"{axle.PavementDamageFactor:F2}");

                    // Fee
                    table.Cell().Element(c => CellStyle(c, isEven))
                        .Text(axle.FeeUsd > 0 ? $"{axle.FeeUsd:N2}" : "-");
                }

                static IContainer CellStyle(IContainer c, bool isEven) =>
                    c.PaddingVertical(3).PaddingHorizontal(2)
                     .Background(isEven ? Colors.White : "#F9FAFB")
                     .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                     .DefaultTextStyle(t => t.FontSize(8));
            });
        });
    }

    private void ComposeGvwSummary(IContainer container)
    {
        container.Border(1.5f).BorderColor(_transaction.IsCompliant ? OfficialGreen : OfficialRed)
            .Column(col =>
        {
            col.Item().Background(_transaction.IsCompliant ? "#DCFCE7" : "#FEE2E2")
                .Padding(5).Text("GROSS VEHICLE WEIGHT SUMMARY")
                .FontSize(10).SemiBold()
                .FontColor(_transaction.IsCompliant ? OfficialGreen : OfficialRed);

            col.Item().Padding(8).Row(row =>
            {
                // Left side - weight details
                row.RelativeItem().Column(c =>
                {
                    c.Spacing(4);
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(140).Text("GVW Measured:").FontSize(10).SemiBold();
                        r.RelativeItem().Text($"{_transaction.GvwMeasuredKg:N0} kg").FontSize(10).Bold();
                    });
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(140).Text("GVW Permissible:").FontSize(10).SemiBold();
                        r.RelativeItem().Text($"{_transaction.GvwPermissibleKg:N0} kg").FontSize(10);
                    });
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(140).Text("Overload:").FontSize(10).SemiBold();
                        var overloadText = r.RelativeItem();
                        if (_transaction.OverloadKg > 0)
                            overloadText.Text($"{_transaction.OverloadKg:N0} kg").FontSize(10).Bold()
                                .FontColor(OfficialRed);
                        else
                            overloadText.Text("0 kg (Compliant)").FontSize(10)
                                .FontColor(OfficialGreen);
                    });
                    if (_transaction.TotalFeeUsd > 0)
                    {
                        c.Item().Row(r =>
                        {
                            r.ConstantItem(140).Text("Total Fee:").FontSize(10).SemiBold();
                            var currency = _transaction.Act?.ChargingCurrency ?? "KES";
                            r.RelativeItem().Text($"{currency} {_transaction.TotalFeeUsd:N2}").FontSize(10).Bold();
                        });
                    }
                    if (_transaction.ToleranceApplied)
                    {
                        c.Item().Text("* Tolerance has been applied to weight measurements")
                            .FontSize(7).Italic().FontColor("#6B7280");
                    }
                });

                // Right side - status image
                row.ConstantItem(100).AlignCenter().AlignMiddle().Element(statusContainer =>
                    ComposeStatusImage(statusContainer, _transaction.ControlStatus));
            });
        });
    }

    private void ComposeSignatures(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Weighing Officer",
                _transaction.WeighedByUserId != Guid.Empty ? null : null));
            row.ConstantItem(30);
            row.RelativeItem().Column(c =>
            {
                c.Spacing(3);
                c.Item().Text("_______________________________").FontSize(9);
                c.Item().Text("DRIVER ACKNOWLEDGMENT").SemiBold().FontSize(8);
                c.Item().Text("I acknowledge that the above weights were measured in my presence.").FontSize(7).Italic();
                c.Item().Text("Name, Signature & Date").FontSize(7).Italic();
            });
        });
    }

    private static void ComposeLegalNote(IContainer container)
    {
        container.Background("#F9FAFB").Border(0.5f).BorderColor(Colors.Grey.Lighten1)
            .Padding(6).Column(col =>
        {
            col.Item().Text("NOTES").FontSize(8).SemiBold();
            col.Item().PaddingTop(2).Text(
                "1. Axle group weights are measured in accordance with the EAC Vehicle Load Control Act 2016 " +
                "and Kenya Traffic Act Cap 403.")
                .FontSize(7);
            col.Item().Text(
                "2. Pavement Damage Factor (PDF) is calculated using the fourth power law: (Actual/Permissible)^4.")
                .FontSize(7);
            col.Item().Text(
                "3. Fees are assessed per axle overload as per the gazette schedule.")
                .FontSize(7);
            col.Item().Text(
                "4. A vehicle found to be overloaded must not proceed until corrective action is taken.")
                .FontSize(7);
        });
    }
}
