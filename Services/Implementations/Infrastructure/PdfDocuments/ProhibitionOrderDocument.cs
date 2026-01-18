using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Prohibition Order - Official vehicle movement prohibition notice
/// Issued under Kenya Traffic Act Cap 403 and EAC Vehicle Load Control Act 2016
/// Legal authority to detain overloaded vehicles
/// </summary>
public class ProhibitionOrderDocument : BaseDocument
{
    private readonly ProhibitionOrder _order;

    public ProhibitionOrderDocument(ProhibitionOrder order)
    {
        _order = order;
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
            col.Item().AlignCenter().Text("THE TRAFFIC ACT (CAP. 403)").FontSize(11);
            col.Item().AlignCenter().Text("EAST AFRICAN COMMUNITY VEHICLE LOAD CONTROL ACT, 2016").FontSize(9);

            col.Item().PaddingVertical(10).AlignCenter()
                .Background(OfficialRed)
                .Padding(10)
                .Text("VEHICLE PROHIBITION ORDER")
                .FontSize(18)
                .SemiBold()
                .FontColor(Colors.White);

            col.Item().PaddingVertical(5).Row(row =>
            {
                row.RelativeItem().Text($"Order No: {_order.ProhibitionNo}").FontSize(10).SemiBold();
                row.RelativeItem().AlignRight().Text($"Issued: {_order.IssuedAt:dd/MM/yyyy HH:mm}").FontSize(10);
            });

            col.Item().LineHorizontal(1.5f).LineColor(Colors.Black);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(12);

            // Addressee
            col.Item().Text(t =>
            {
                t.Span("TO THE DRIVER/OWNER OF VEHICLE REGISTRATION NUMBER: ");
                t.Span(_order.Weighing?.VehicleRegNumber ?? "N/A").SemiBold().FontSize(12);
            });

            // Official notice
            col.Item().Text("TAKE NOTICE that the above-mentioned vehicle has been weighed at an authorized weighbridge and found to be in contravention of the prescribed weight limits as detailed below:");

            // Violation details box
            col.Item().Border(1).BorderColor(OfficialRed).Padding(10).Background(Colors.Red.Lighten4).Column(v =>
            {
                v.Spacing(5);
                v.Item().Text("REASON FOR PROHIBITION:").FontSize(11).SemiBold().FontColor(Colors.Red.Darken2);
                v.Item().Text(_order.Reason).FontSize(10);

                if (_order.Weighing != null)
                {
                    v.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text($"GVW Measured: {_order.Weighing.GvwMeasuredKg:N0} kg").SemiBold();
                        row.RelativeItem().Text($"Legal Limit: {_order.Weighing.GvwPermissibleKg:N0} kg");
                    });
                    v.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Overload: {_order.Weighing.OverloadKg:N0} kg").FontColor(Colors.Red.Darken2).SemiBold().FontSize(12);
                    });
                }
            });

            // Legal authority
            col.Item().Text(t =>
            {
                t.Span("PURSUANT to the powers vested in me under Section 45 of the Traffic Act (Cap. 403) and the EAC Vehicle Load Control Act, 2016, I hereby ");
                t.Span("PROHIBIT").SemiBold().FontColor(Colors.Red.Darken2);
                t.Span(" the movement of this vehicle from this location until the conditions stated below are fully satisfied.");
            });

            // Mandatory requirements
            col.Item().Column(requirements =>
            {
                requirements.Item().Text("MANDATORY REQUIREMENTS:").FontSize(11).SemiBold().Underline();
                requirements.Item().PaddingLeft(15).Column(items =>
                {
                    items.Spacing(5);
                    ComposeRequirement(items.Item(), "1", "The vehicle MUST be moved to the designated holding yard/area immediately.");
                    ComposeRequirement(items.Item(), "2", "The excess load MUST be redistributed or offloaded to achieve compliance.");
                    ComposeRequirement(items.Item(), "3", "A re-weighing MUST be conducted to verify compliance with legal limits.");
                    ComposeRequirement(items.Item(), "4", "ALL applicable fees and charges MUST be paid before vehicle release.");
                    ComposeRequirement(items.Item(), "5", "Any interference with the vehicle, load, or seals without authorization is a criminal offense punishable under law.");
                });
            });

            // Legal consequences warning
            col.Item().Border(1).BorderColor(Colors.Orange.Medium).Background(Colors.Orange.Lighten4).Padding(8).Column(warning =>
            {
                warning.Item().Text("⚠ LEGAL WARNING").FontSize(10).SemiBold().FontColor(Colors.Orange.Darken3);
                warning.Item().Text("Failure to comply with this order may result in:")
                    .FontSize(9).FontColor(Colors.Orange.Darken2);
                warning.Item().PaddingLeft(10).Column(w =>
                {
                    w.Item().Text("• Additional penalties and fines").FontSize(8);
                    w.Item().Text("• Vehicle impoundment and prosecution").FontSize(8);
                    w.Item().Text("• Suspension of operating licenses").FontSize(8);
                    w.Item().Text("• Criminal charges for non-compliance").FontSize(8);
                });
            });

            // Authority and acknowledgment signatures
            col.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Authorized Officer", _order.IssuedBy?.FullName));

                row.ConstantItem(20);

                row.RelativeItem().Column(ack =>
                {
                    ack.Spacing(3);
                    ack.Item().Text("_______________________________").FontSize(9);
                    ack.Item().Text("DRIVER/OWNER ACKNOWLEDGMENT").SemiBold().FontSize(8);
                    ack.Item().Text("I acknowledge receipt of this Prohibition Order").FontSize(7);
                    ack.Item().Text("and understand the requirements stated herein.").FontSize(7);
                    ack.Item().PaddingTop(3).Text("Signature & Date: _______________").FontSize(7);
                });
            });

            // Bottom notice
            col.Item().PaddingTop(10).AlignCenter().Background(Colors.Grey.Lighten4).Padding(5)
                .Text("This order remains in effect until all conditions are met and written clearance is issued by an authorized officer.")
                .FontSize(8).Italic();
        });
    }

    private void ComposeRequirement(IContainer container, string number, string text)
    {
        container.Row(row =>
        {
            row.ConstantItem(20).Text(number + ".").SemiBold();
            row.RelativeItem().Text(text).FontSize(10);
        });
    }
}
