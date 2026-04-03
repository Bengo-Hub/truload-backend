using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Data class containing all information needed for the Kenya Police case file cover page.
/// </summary>
public class CoverPageData
{
    // File references
    public string PoliceCaseFileNo { get; set; } = string.Empty;
    public string ObNo { get; set; } = string.Empty;
    public string CourtFileNo { get; set; } = string.Empty;
    public string CourtName { get; set; } = string.Empty;

    // Station hierarchy
    public string PoliceStation { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;

    // Court hearings
    public List<CoverPageHearing> Hearings { get; set; } = [];

    // Complainant info
    public string ComplainantName { get; set; } = string.Empty;
    public string ComplainantAddress { get; set; } = string.Empty;
    public string ComplainantRegNo { get; set; } = string.Empty;

    // Accused info
    public string AccusedName { get; set; } = string.Empty;
    public string AccusedAddress { get; set; } = string.Empty;
    public string AccusedRegNo { get; set; } = string.Empty;

    // Charge details
    public string ChargeAndSection { get; set; } = string.Empty;
    public DateTime? DateOfArrest { get; set; }
    public TimeSpan? TimeOfArrest { get; set; }
    public string ResultOfCase { get; set; } = string.Empty;

    // Officers
    public string InvestigatingOfficerName { get; set; } = string.Empty;
    public string InvestigatingOfficerRank { get; set; } = string.Empty;
    public string InvestigationTakenOverBy { get; set; } = string.Empty;

    // Footer officers
    public string DivisionOrProvinceOfficer { get; set; } = string.Empty;
    public string CourtProsecutor { get; set; } = string.Empty;
}

/// <summary>
/// Represents a single court hearing entry for the cover page hearings table.
/// </summary>
public class CoverPageHearing
{
    public DateTime Date { get; set; }
    public TimeSpan? Time { get; set; }

    /// <summary>
    /// Hearing type code abbreviation: M (Mention), H (Hearing), POG (Plea of Guilty), PONGE (Plea of Not Guilty)
    /// </summary>
    public string TypeCode { get; set; } = string.Empty;

    public string Comments { get; set; } = string.Empty;
}

/// <summary>
/// Kenya Police Case File Cover Page document.
/// Generates the official police case file cover with file references,
/// court hearings table, complainant/accused info, charge details, and officer signatures.
/// </summary>
public class CoverPageDocument : BaseDocument
{
    private readonly CoverPageData _data;

    public CoverPageDocument(CoverPageData data)
    {
        _data = data;
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
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        var policeLogo = LoadLogo(BrandingConstants.Logos.KenyaPoliceLogo);

        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                // Left: Police logo
                row.ConstantItem(LogoWidth).AlignMiddle().Column(logoCol =>
                {
                    if (policeLogo != null)
                        logoCol.Item().Width(LogoWidth).Height(LogoHeight).Image(policeLogo, ImageScaling.FitArea);
                });

                // Center: Title
                row.RelativeItem().AlignCenter().PaddingHorizontal(5).Column(center =>
                {
                    center.Item().AlignCenter().Text("THE KENYA POLICE")
                        .FontSize(16).Bold().FontColor(KuraBlack);
                    center.Item().AlignCenter().Text("CASE FILE COVER PAGE")
                        .FontSize(11).SemiBold();
                });

                // Right: Police logo (mirror)
                row.ConstantItem(LogoWidth).AlignMiddle().Column(logoCol =>
                {
                    if (policeLogo != null)
                        logoCol.Item().Width(LogoWidth).Height(LogoHeight).Image(policeLogo, ImageScaling.FitArea);
                });
            });

            col.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(6);

            // File References Section
            col.Item().Element(ComposeFileReferences);

            // Station hierarchy
            col.Item().Element(ComposeStationInfo);

            col.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // Court Hearings Table + Complainant/Accused side by side
            col.Item().Element(ComposeHearingsAndParties);

            col.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // Charge & Arrest details
            col.Item().Element(ComposeChargeDetails);

            col.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // Investigating Officer
            col.Item().Element(ComposeOfficerDetails);

            col.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // Footer officer signatures
            col.Item().Element(ComposeOfficerSignatures);
        });
    }

    private void ComposeFileReferences(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(3);
            col.Item().Text("FILE REFERENCES").FontSize(10).SemiBold().Underline();

            col.Item().PaddingLeft(10).Column(refs =>
            {
                refs.Spacing(2);
                ComposeInfoRow(refs.Item(), "Police Case File No", _data.PoliceCaseFileNo, true);
                ComposeInfoRow(refs.Item(), "O.B No", _data.ObNo);
                ComposeInfoRow(refs.Item(), "Court File No", _data.CourtFileNo);
                ComposeInfoRow(refs.Item(), "Court Name", _data.CourtName);
            });
        });
    }

    private void ComposeStationInfo(IContainer container)
    {
        container.PaddingLeft(10).Column(col =>
        {
            col.Spacing(2);
            ComposeInfoRow(col.Item(), "Police Station", _data.PoliceStation);
            ComposeInfoRow(col.Item(), "Division", _data.Division);
            ComposeInfoRow(col.Item(), "Province", _data.Province);
        });
    }

    private void ComposeHearingsAndParties(IContainer container)
    {
        container.Row(row =>
        {
            // Left: Court Hearings Table
            row.RelativeItem(3).Element(ComposeHearingsTable);

            row.ConstantItem(10); // spacer

            // Right: Complainant & Accused
            row.RelativeItem(2).Element(ComposePartiesInfo);
        });
    }

    private void ComposeHearingsTable(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Text("COURT HEARINGS").FontSize(10).SemiBold().Underline();
            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2); // DATE-TIME
                    columns.RelativeColumn(1); // M/H
                    columns.RelativeColumn(3); // Comments
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderStyle).Text("DATE-TIME");
                    header.Cell().Element(HeaderStyle).Text("M/H");
                    header.Cell().Element(HeaderStyle).Text("Comments");

                    static IContainer HeaderStyle(IContainer c) =>
                        c.DefaultTextStyle(x => x.SemiBold().FontSize(8))
                         .Background(Colors.Grey.Lighten3)
                         .PaddingVertical(3)
                         .PaddingHorizontal(3)
                         .BorderBottom(1)
                         .BorderColor(Colors.Black);
                });

                if (_data.Hearings.Count > 0)
                {
                    foreach (var hearing in _data.Hearings)
                    {
                        var dateTime = hearing.Time.HasValue
                            ? $"{hearing.Date:dd/MM/yyyy} {hearing.Time.Value:hh\\:mm}"
                            : $"{hearing.Date:dd/MM/yyyy}";

                        table.Cell().Element(CellStyle).Text(dateTime);
                        table.Cell().Element(CellStyle).Text(hearing.TypeCode);
                        table.Cell().Element(CellStyle).Text(hearing.Comments);
                    }
                }
                else
                {
                    // Empty rows for manual filling
                    for (var i = 0; i < 8; i++)
                    {
                        table.Cell().Element(CellStyle).Text("");
                        table.Cell().Element(CellStyle).Text("");
                        table.Cell().Element(CellStyle).Text("");
                    }
                }

                static IContainer CellStyle(IContainer c) =>
                    c.PaddingVertical(3)
                     .PaddingHorizontal(3)
                     .BorderBottom(0.5f)
                     .BorderColor(Colors.Grey.Lighten2)
                     .DefaultTextStyle(t => t.FontSize(8));
            });
        });
    }

    private void ComposePartiesInfo(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(6);

            // Complainant
            col.Item().Column(comp =>
            {
                comp.Item().Text("COMPLAINANT").FontSize(9).SemiBold().Underline();
                comp.Item().PaddingTop(3).Column(details =>
                {
                    details.Spacing(2);
                    details.Item().Text($"Name: {_data.ComplainantName}").FontSize(8);
                    details.Item().Text($"Address: {_data.ComplainantAddress}").FontSize(8);
                    details.Item().Text($"Reg No: {_data.ComplainantRegNo}").FontSize(8);
                });
            });

            // Accused
            col.Item().Column(acc =>
            {
                acc.Item().Text("ACCUSED").FontSize(9).SemiBold().Underline();
                acc.Item().PaddingTop(3).Column(details =>
                {
                    details.Spacing(2);
                    details.Item().Text($"Name: {_data.AccusedName}").FontSize(8);
                    details.Item().Text($"Address: {_data.AccusedAddress}").FontSize(8);
                    details.Item().Text($"Reg No: {_data.AccusedRegNo}").FontSize(8);
                });
            });
        });
    }

    private void ComposeChargeDetails(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(3);
            col.Item().Text("CHARGE & CASE DETAILS").FontSize(10).SemiBold().Underline();

            col.Item().PaddingLeft(10).Column(details =>
            {
                details.Spacing(2);
                ComposeInfoRow(details.Item(), "Charge & Section of Law", _data.ChargeAndSection);

                var arrestDateTime = _data.DateOfArrest.HasValue
                    ? _data.TimeOfArrest.HasValue
                        ? $"{_data.DateOfArrest.Value:dd/MM/yyyy} at {_data.TimeOfArrest.Value:hh\\:mm}"
                        : $"{_data.DateOfArrest.Value:dd/MM/yyyy}"
                    : "N/A";
                ComposeInfoRow(details.Item(), "Date & Time of Arrest", arrestDateTime);

                ComposeInfoRow(details.Item(), "Result of Case", _data.ResultOfCase);
            });
        });
    }

    private void ComposeOfficerDetails(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(3);
            col.Item().Text("INVESTIGATING OFFICER").FontSize(10).SemiBold().Underline();

            col.Item().PaddingLeft(10).Column(details =>
            {
                details.Spacing(2);
                var ioNameRank = string.IsNullOrEmpty(_data.InvestigatingOfficerRank)
                    ? _data.InvestigatingOfficerName
                    : $"{_data.InvestigatingOfficerRank} {_data.InvestigatingOfficerName}";
                ComposeInfoRow(details.Item(), "Name & Rank of I.O", ioNameRank);
                ComposeInfoRow(details.Item(), "Investigation Taken Over By", _data.InvestigationTakenOverBy);
            });
        });
    }

    private void ComposeOfficerSignatures(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(8);

            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Element(c =>
                    ComposeSignatureBlock(c, "Officer i/c Division or Province", _data.DivisionOrProvinceOfficer));
                row.ConstantItem(40);
                row.RelativeItem().Element(c =>
                    ComposeSignatureBlock(c, "Court Prosecutor", _data.CourtProsecutor));
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            col.Item().PaddingTop(4).Text("*M = Mention | H = Hearing | POG = Plea of Guilty | PONGE = Plea of Not Guilty")
                .FontSize(7).Italic();

            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().DefaultTextStyle(x => x.FontSize(7)).Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });

                row.RelativeItem().AlignRight().DefaultTextStyle(x => x.FontSize(7))
                    .Text($"Generated: {DateTime.UtcNow:dd/MM/yyyy HH:mm} EAT");
            });
        });
    }
}
