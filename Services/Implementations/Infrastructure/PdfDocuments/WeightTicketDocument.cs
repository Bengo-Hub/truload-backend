using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Weight Ticket - Official weighing certificate
/// Compliant with Kenya Traffic Act Cap 403 and EAC Vehicle Load Control Act 2016
/// </summary>
public class WeightTicketDocument : BaseDocument
{
    private readonly WeighingTransaction _transaction;

    public WeightTicketDocument(WeighingTransaction transaction)
    {
        _transaction = transaction;
    }

    public override byte[] Generate()
    {
        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(5).Element(ComposeContent);
                page.Footer().Element(ComposeOfficialFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("REPUBLIC OF KENYA").FontSize(12).SemiBold();
                    c.Item().Text("WEIGHT CERTIFICATE").FontSize(14).SemiBold().FontColor("#0066CC");
                    c.Item().Text(_transaction.Station?.Name ?? "Weighbridge Station").FontSize(9);
                });

                row.ConstantItem(100).AlignRight().Column(c =>
                {
                    c.Item().Text($"No: {_transaction.TicketNumber}").FontSize(10).SemiBold();
                    c.Item().Text($"{_transaction.WeighedAt:dd/MM/yyyy}").FontSize(8);
                    c.Item().Text($"{_transaction.WeighedAt:HH:mm}").FontSize(8);
                });
            });

            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(4);

            // Vehicle Information
            col.Item().Background(Colors.Grey.Lighten4).Padding(4).Column(v =>
            {
                v.Item().Text("VEHICLE INFORMATION").FontSize(9).SemiBold();
                v.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Registration: {_transaction.VehicleRegNumber}").SemiBold();
                    row.RelativeItem().Text($"Vehicle Type: {_transaction.Vehicle?.VehicleType ?? "N/A"}");
                });
                v.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Driver: {_transaction.Driver?.FullNames ?? "N/A"}");
                    row.RelativeItem().Text($"Transporter: {_transaction.Transporter?.Name ?? "N/A"}");
                });
            });

            // Axle Weight Details Table
            col.Item().Text("AXLE WEIGHT MEASUREMENTS (KG)").FontSize(9).SemiBold();

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.ConstantColumn(50);
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Element(HeaderStyle).Text("Axle");
                    header.Cell().Element(HeaderStyle).Text("Measured");
                    header.Cell().Element(HeaderStyle).Text("Legal Limit");
                    header.Cell().Element(HeaderStyle).Text("Difference");
                    header.Cell().Element(HeaderStyle).Text("Status");

                    static IContainer HeaderStyle(IContainer c) =>
                        c.DefaultTextStyle(x => x.SemiBold().FontSize(8))
                         .PaddingVertical(3)
                         .BorderBottom(1)
                         .BorderColor(Colors.Black);
                });

                // Axle rows
                foreach (var axle in _transaction.WeighingAxles.OrderBy(a => a.AxleNumber))
                {
                    table.Cell().Element(CellStyle).Text($"{axle.AxleNumber}");
                    table.Cell().Element(CellStyle).Text($"{axle.MeasuredWeightKg:N0}");
                    table.Cell().Element(CellStyle).Text($"{axle.PermissibleWeightKg:N0}");

                    var diff = axle.MeasuredWeightKg - axle.PermissibleWeightKg;
                    var diffCell = table.Cell().Element(CellStyle);
                    if (diff > 0)
                        diffCell.Text($"+{diff:N0}").FontColor(Colors.Red.Medium);
                    else
                        diffCell.Text($"{diff:N0}").FontColor(Colors.Green.Medium);

                    var statusCell = table.Cell().Element(CellStyle);
                    if (diff > 0)
                        statusCell.Text("✗").FontColor(Colors.Red.Medium).SemiBold();
                    else
                        statusCell.Text("✓").FontColor(Colors.Green.Medium).SemiBold();
                }

                static IContainer CellStyle(IContainer c) =>
                    c.PaddingVertical(2)
                     .BorderBottom(0.5f)
                     .BorderColor(Colors.Grey.Lighten2)
                     .DefaultTextStyle(t => t.FontSize(8));
            });

            // GVW Summary
            col.Item().PaddingTop(4).Border(1).BorderColor(Colors.Black).Padding(5).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("GROSS VEHICLE WEIGHT").FontSize(9).SemiBold();
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(80).Text("Measured:");
                        r.RelativeItem().Text($"{_transaction.GvwMeasuredKg:N0} kg").SemiBold();
                    });
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(80).Text("Legal Limit:");
                        r.RelativeItem().Text($"{_transaction.GvwPermissibleKg:N0} kg");
                    });
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(80).Text("Overload:");
                        var overload = r.RelativeItem();
                        if (_transaction.OverloadKg > 0)
                            overload.Text($"{_transaction.OverloadKg:N0} kg").FontColor(Colors.Red.Medium).SemiBold();
                        else
                            overload.Text("0 kg (Compliant)").FontColor(Colors.Green.Medium);
                    });
                });

                row.ConstantItem(100).AlignCenter().Column(c =>
                {
                    c.Item().Text("STATUS").FontSize(8).SemiBold();
                    c.Item().PaddingTop(5).AlignCenter().Element(statusContainer =>
                    {
                        if (_transaction.IsCompliant)
                        {
                            statusContainer.Background(Colors.Green.Lighten3)
                                .Padding(8)
                                .Text("COMPLIANT")
                                .FontSize(11)
                                .SemiBold()
                                .FontColor(Colors.Green.Darken2);
                        }
                        else
                        {
                            statusContainer.Background(Colors.Red.Lighten3)
                                .Padding(8)
                                .Text("OVERLOAD")
                                .FontSize(11)
                                .SemiBold()
                                .FontColor(Colors.Red.Darken2);
                        }
                    });
                });
            });

            // Officer signature
            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Weighing Officer"));
                row.ConstantItem(20);
                row.RelativeItem().Column(c =>
                {
                    c.Spacing(3);
                    c.Item().Text("_______________________________").FontSize(9);
                    c.Item().Text("DRIVER ACKNOWLEDGMENT").SemiBold().FontSize(8);
                    c.Item().Text("Signature & Date").FontSize(7).Italic();
                });
            });
        });
    }
}
