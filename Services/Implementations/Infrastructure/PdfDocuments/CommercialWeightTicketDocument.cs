using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;
using TruLoad.Backend.DTOs.Weighing;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Commercial Weight Ticket - A clean, professional weight ticket for commercial weighing operations.
/// No enforcement/government markings. Net weight is the most prominent element.
/// </summary>
public class CommercialWeightTicketDocument : BaseDocument
{
    private readonly CommercialWeighingResultDto _result;
    private readonly string? _organizationName;
    private readonly string? _orgLogoFile;
    private readonly string? _primaryColor;
    private readonly string? _secondaryColor;
    private readonly byte[]? _qrCodeBytes;

    public CommercialWeightTicketDocument(
        CommercialWeighingResultDto result,
        string? organizationName = null,
        string? orgLogoFile = null,
        string? primaryColor = null,
        string? secondaryColor = null,
        byte[]? qrCodeBytes = null)
    {
        _result = result;
        _organizationName = organizationName;
        _orgLogoFile = orgLogoFile;
        _primaryColor = primaryColor;
        _secondaryColor = secondaryColor;
        _qrCodeBytes = qrCodeBytes;
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
                page.DefaultTextStyle(x => x.FontSize(8.5f).FontFamily("Inter"));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(4).Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        var primaryLogo = ResolveOrgLogo(_orgLogoFile, isCommercial: true);

        ComposeOfficialHeaderWithLogos(
            container,
            primaryLogo,
            secondaryLogoFile: null, // No coat of arms for commercial
            "WEIGHT TICKET",
            subtitle: _result.StationName ?? "Weighbridge Station",
            referenceNumber: $"Ticket No: {_result.TicketNumber}",
            dateText: $"Date: {_result.WeighedAt:dd/MM/yyyy HH:mm}",
            titleColor: _primaryColor,
            organizationName: _organizationName,
            isEnforcement: false);
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(6);

            // Vehicle Information
            col.Item().Element(ComposeVehicleInfo);

            // Cargo Information
            col.Item().Element(ComposeCargoInfo);

            // Weight Summary (prominent center section)
            col.Item().Element(ComposeWeightSummary);

            // Per-deck / per-axle weights (if captured)
            if ((_result.FirstPassAxles != null && _result.FirstPassAxles.Count > 0) ||
                (_result.SecondPassAxles != null && _result.SecondPassAxles.Count > 0))
            {
                col.Item().Element(ComposeAxleWeights);
            }

            // Discrepancy section (if expected weight provided)
            if (_result.ExpectedNetWeightKg.HasValue && _result.NetWeightKg.HasValue)
            {
                col.Item().Element(ComposeDiscrepancy);
            }

            // Remarks
            if (!string.IsNullOrEmpty(_result.Remarks))
            {
                col.Item().Element(ComposeRemarks);
            }

            // Signature blocks
            col.Item().PaddingTop(10).Element(ComposeSignatures);
        });
    }

    private void ComposeVehicleInfo(IContainer container)
    {
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Column(col =>
        {
            col.Item().Background("#F0F4F8").Padding(3)
                .Text("VEHICLE INFORMATION").FontSize(8.5f).SemiBold();

            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                ComposeInfoCell(table, "Reg No", _result.VehicleRegNumber, true);
                ComposeInfoCell(table, "Trailer Reg", _result.TrailerRegNo ?? "N/A");
                ComposeInfoCell(table, "Vehicle", FormatVehicle());

                ComposeInfoCell(table, "Transporter", _result.TransporterName ?? "N/A");
                ComposeInfoCell(table, "Driver", _result.DriverName ?? "N/A");
                ComposeInfoCell(table, "Operator", _result.WeighedByUserName ?? "N/A");
            });
        });
    }

    private void ComposeCargoInfo(IContainer container)
    {
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Column(col =>
        {
            col.Item().Background("#F0F4F8").Padding(3)
                .Text("CARGO INFORMATION").FontSize(8.5f).SemiBold();

            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                ComposeInfoCell(table, "Cargo Type", _result.CargoType ?? "N/A");
                ComposeInfoCell(table, "Consignment No", _result.ConsignmentNo ?? "N/A");
                ComposeInfoCell(table, "Order Ref", _result.OrderReference ?? "N/A");

                ComposeInfoCell(table, "Origin", _result.SourceLocation ?? "N/A");
                ComposeInfoCell(table, "Destination", _result.DestinationLocation ?? "N/A");
                ComposeInfoCell(table, "Seal Numbers", _result.SealNumbers ?? "N/A");
            });
        });
    }

    private void ComposeWeightSummary(IContainer container)
    {
        var accentColor = _primaryColor ?? KuraBlue;

        container.Border(1.5f).BorderColor(accentColor).Column(col =>
        {
            col.Item().Background(accentColor).Padding(5)
                .Text("WEIGHT SUMMARY").FontSize(10).Bold().FontColor(Colors.White);

            col.Item().Padding(8).Column(weightCol =>
            {
                weightCol.Spacing(4);

                // First and Second weights side by side
                weightCol.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("First Weight").FontSize(7.5f).SemiBold().FontColor("#4B5563");
                        c.Item().Row(r =>
                        {
                            r.AutoItem().Text($"{FormatWeight(_result.FirstWeightKg)} kg").FontSize(10).SemiBold();
                            if (!string.IsNullOrEmpty(_result.FirstWeightType))
                                r.AutoItem().PaddingLeft(4).Text($"({Capitalize(_result.FirstWeightType)})").FontSize(8).FontColor("#6B7280");
                        });
                        if (_result.FirstWeightAt.HasValue)
                            c.Item().Text($"{_result.FirstWeightAt.Value:dd/MM/yyyy HH:mm}").FontSize(7).FontColor("#9CA3AF");
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Second Weight").FontSize(7.5f).SemiBold().FontColor("#4B5563");
                        c.Item().Row(r =>
                        {
                            r.AutoItem().Text($"{FormatWeight(_result.SecondWeightKg)} kg").FontSize(10).SemiBold();
                            if (!string.IsNullOrEmpty(_result.SecondWeightType))
                                r.AutoItem().PaddingLeft(4).Text($"({Capitalize(_result.SecondWeightType)})").FontSize(8).FontColor("#6B7280");
                        });
                        if (_result.SecondWeightAt.HasValue)
                            c.Item().Text($"{_result.SecondWeightAt.Value:dd/MM/yyyy HH:mm}").FontSize(7).FontColor("#9CA3AF");
                    });
                });

                // Tare and Gross side by side
                weightCol.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Tare Weight").FontSize(7.5f).SemiBold().FontColor("#4B5563");
                        c.Item().Row(r =>
                        {
                            r.AutoItem().Text($"{FormatWeight(_result.TareWeightKg)} kg").FontSize(10).SemiBold();
                            if (!string.IsNullOrEmpty(_result.TareSource))
                                r.AutoItem().PaddingLeft(4).Text($"({Capitalize(_result.TareSource)})").FontSize(8).FontColor("#6B7280");
                        });
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Gross Weight").FontSize(7.5f).SemiBold().FontColor("#4B5563");
                        c.Item().Text($"{FormatWeight(_result.GrossWeightKg)} kg").FontSize(10).SemiBold();
                    });
                });

                // Separator
                weightCol.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                // NET WEIGHT - large and prominent
                weightCol.Item().AlignCenter().Column(c =>
                {
                    c.Item().AlignCenter().Text("NET WEIGHT").FontSize(10).SemiBold().FontColor("#4B5563");
                    c.Item().AlignCenter().Text($"{FormatWeight(_result.NetWeightKg)} kg")
                        .FontSize(22).Bold().FontColor(accentColor);
                });

                // Quality deduction and adjusted net (if applicable)
                if (_result.QualityDeductionKg.HasValue && _result.QualityDeductionKg.Value > 0)
                {
                    weightCol.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                    weightCol.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Quality Deduction").FontSize(7.5f).SemiBold().FontColor("#4B5563");
                            c.Item().Text($"-{_result.QualityDeductionKg.Value:N0} kg").FontSize(10).SemiBold().FontColor(OfficialRed);
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Adjusted Net Weight").FontSize(7.5f).SemiBold().FontColor("#4B5563");
                            c.Item().Text($"{FormatWeight(_result.AdjustedNetWeightKg)} kg")
                                .FontSize(14).Bold().FontColor(accentColor);
                        });
                    });
                }
            });
        });
    }

    private void ComposeAxleWeights(IContainer container)
    {
        var firstAxles = _result.FirstPassAxles ?? new List<CommercialAxleWeightDto>();
        var secondAxles = _result.SecondPassAxles ?? new List<CommercialAxleWeightDto>();
        var isMultideck = firstAxles.Count <= 4 && firstAxles.Count > 1;
        var label = isMultideck ? "DECK WEIGHTS" : "AXLE WEIGHTS";

        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Column(col =>
        {
            col.Item().Background("#F0F4F8").Padding(3)
                .Text(label).FontSize(8.5f).SemiBold();

            col.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(60);  // Deck/Axle
                    columns.RelativeColumn();     // First pass
                    if (secondAxles.Count > 0)
                        columns.RelativeColumn(); // Second pass
                });

                // Header
                table.Header(header =>
                {
                    var hLabel = isMultideck ? "Deck" : "Axle";
                    header.Cell().Background("#E5E7EB").Padding(3).Text(hLabel).FontSize(7.5f).SemiBold();
                    header.Cell().Background("#E5E7EB").Padding(3)
                        .Text($"First ({Capitalize(_result.FirstWeightType ?? "pass")}) kg").FontSize(7.5f).SemiBold();
                    if (secondAxles.Count > 0)
                        header.Cell().Background("#E5E7EB").Padding(3)
                            .Text($"Second ({Capitalize(_result.SecondWeightType ?? "pass")}) kg").FontSize(7.5f).SemiBold();
                });

                // Rows
                var maxCount = Math.Max(firstAxles.Count, secondAxles.Count);
                for (var i = 0; i < maxCount; i++)
                {
                    var isEven = i % 2 == 0;
                    var bg = isEven ? "#FFFFFF" : "#F9FAFB";
                    var axleLabel = isMultideck ? $"Deck {i + 1}" : $"Axle {i + 1}";
                    var firstW = i < firstAxles.Count ? $"{firstAxles[i].WeightKg:N0}" : "---";
                    var secondW = i < secondAxles.Count ? $"{secondAxles[i].WeightKg:N0}" : "---";

                    table.Cell().Background(bg).Padding(2).Text(axleLabel).FontSize(7.5f).SemiBold();
                    table.Cell().Background(bg).Padding(2).Text(firstW).FontSize(7.5f);
                    if (secondAxles.Count > 0)
                        table.Cell().Background(bg).Padding(2).Text(secondW).FontSize(7.5f);
                }

                // GVW total row
                var firstGvw = firstAxles.Count > 0 ? firstAxles.Sum(a => a.WeightKg) : (int?)null;
                var secondGvw = secondAxles.Count > 0 ? secondAxles.Sum(a => a.WeightKg) : (int?)null;

                table.Cell().Background("#E5E7EB").Padding(2).Text("GVW").FontSize(7.5f).Bold();
                table.Cell().Background("#E5E7EB").Padding(2)
                    .Text(firstGvw.HasValue ? $"{firstGvw.Value:N0}" : "---").FontSize(7.5f).Bold();
                if (secondAxles.Count > 0)
                    table.Cell().Background("#E5E7EB").Padding(2)
                        .Text(secondGvw.HasValue ? $"{secondGvw.Value:N0}" : "---").FontSize(7.5f).Bold();
            });
        });
    }

    private void ComposeDiscrepancy(IContainer container)
    {
        var expectedKg = _result.ExpectedNetWeightKg!.Value;
        var actualKg = _result.AdjustedNetWeightKg ?? _result.NetWeightKg ?? 0;
        var varianceKg = actualKg - expectedKg;
        var variancePct = expectedKg != 0 ? (double)varianceKg / expectedKg * 100 : 0;

        var isOver = varianceKg > 0;
        var bgColor = Math.Abs(variancePct) > 2 ? "#FEF3C7" : "#F0FDF4";
        var textColor = Math.Abs(variancePct) > 2 ? "#B45309" : OfficialGreen;

        container.Background(bgColor).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(col =>
        {
            col.Item().Text("WEIGHT DISCREPANCY").FontSize(9).SemiBold();

            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Expected Weight").FontSize(7.5f).SemiBold().FontColor("#4B5563");
                    c.Item().Text($"{expectedKg:N0} kg").FontSize(10).SemiBold();
                });

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Actual Weight").FontSize(7.5f).SemiBold().FontColor("#4B5563");
                    c.Item().Text($"{actualKg:N0} kg").FontSize(10).SemiBold();
                });

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Variance").FontSize(7.5f).SemiBold().FontColor("#4B5563");
                    c.Item().Text($"{(isOver ? "+" : "")}{varianceKg:N0} kg ({variancePct:F1}%)")
                        .FontSize(10).SemiBold().FontColor(textColor);
                });
            });
        });
    }

    private void ComposeRemarks(IContainer container)
    {
        container.Background("#F9FAFB").Border(0.5f).BorderColor(Colors.Grey.Lighten1)
            .Padding(6).Column(col =>
        {
            col.Item().Text("REMARKS").FontSize(8.5f).SemiBold();
            col.Item().PaddingTop(2).Text(_result.Remarks).FontSize(8.5f);
        });
    }

    private void ComposeSignatures(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Spacing(3);
                c.Item().Text("_______________________________").FontSize(8.5f);
                c.Item().Text("WEIGHBRIDGE OPERATOR").SemiBold().FontSize(7.5f);
                if (!string.IsNullOrEmpty(_result.WeighedByUserName))
                    c.Item().Text(_result.WeighedByUserName).FontSize(8);
                c.Item().Text("Name, Signature & Date").FontSize(6.5f).Italic();
            });

            row.ConstantItem(25);

            row.RelativeItem().Column(c =>
            {
                c.Spacing(3);
                c.Item().Text("_______________________________").FontSize(8.5f);
                c.Item().Text("DRIVER / TRANSPORTER REPRESENTATIVE").SemiBold().FontSize(7.5f);
                if (!string.IsNullOrEmpty(_result.DriverName))
                    c.Item().Text(_result.DriverName).FontSize(8);
                c.Item().Text("Name, Signature & Date").FontSize(6.5f).Italic();
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.PaddingTop(10).Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            col.Item().PaddingTop(3).Row(row =>
            {
                // QR code for verification (if provided)
                if (_qrCodeBytes != null)
                {
                    row.ConstantItem(50).Height(50).Image(_qrCodeBytes, ImageScaling.FitArea);
                }

                row.RelativeItem().Column(footerCol =>
                {
                    footerCol.Item().Row(footerRow =>
                    {
                        footerRow.RelativeItem().DefaultTextStyle(x => x.FontSize(7)).Text(t =>
                        {
                            t.Span("Page ");
                            t.CurrentPageNumber();
                            t.Span(" of ");
                            t.TotalPages();
                        });

                        footerRow.RelativeItem().AlignCenter().DefaultTextStyle(x => x.FontSize(7).Italic())
                            .Text(BrandingConstants.DocumentFooter.GeneratedByText);

                        footerRow.RelativeItem().AlignRight().DefaultTextStyle(x => x.FontSize(7))
                            .Text($"Printed: {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC");
                    });

                    footerCol.Item().PaddingTop(2).AlignCenter().DefaultTextStyle(x => x.FontSize(6).Italic())
                        .Text(BrandingConstants.DocumentFooter.DisclaimerText);
                });
            });
        });
    }

    // ── Helpers ──

    private string FormatVehicle()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(_result.VehicleMake)) parts.Add(_result.VehicleMake);
        if (!string.IsNullOrEmpty(_result.VehicleModel)) parts.Add(_result.VehicleModel);
        return parts.Count > 0 ? string.Join(" ", parts) : "N/A";
    }

    private static string FormatWeight(int? weightKg)
    {
        return weightKg.HasValue ? $"{weightKg.Value:N0}" : "---";
    }

    private static string Capitalize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return char.ToUpper(value[0]) + value[1..];
    }

    private static void ComposeInfoCell(TableDescriptor table, string label, string value, bool bold = false)
    {
        table.Cell().PaddingVertical(1).Row(row =>
        {
            row.ConstantItem(80).Text(label + ":").FontSize(7.5f).SemiBold().FontColor("#4B5563");
            if (bold)
                row.RelativeItem().Text(value).FontSize(8.5f).Bold();
            else
                row.RelativeItem().Text(value).FontSize(8.5f);
        });
    }
}
