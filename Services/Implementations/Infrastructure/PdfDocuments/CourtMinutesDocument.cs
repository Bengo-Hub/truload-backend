using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

/// <summary>
/// Court Minutes Document - Official court hearing minutes
/// Records proceedings, outcomes, and adjournments for traffic court cases
/// </summary>
public class CourtMinutesDocument : BaseDocument
{
    private readonly CourtHearing _hearing;
    private readonly CaseRegister? _caseRegister;

    public CourtMinutesDocument(CourtHearing hearing, CaseRegister? caseRegister = null)
    {
        _hearing = hearing;
        _caseRegister = caseRegister ?? hearing.CaseRegister;
    }

    public override byte[] Generate()
    {
        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(10).Element(ComposeContent);
                page.Footer().Element(ComposeOfficialFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text("REPUBLIC OF KENYA").FontSize(14).SemiBold();
            col.Item().AlignCenter().Text("IN THE TRAFFIC COURT").FontSize(12).SemiBold();
            col.Item().PaddingTop(8).AlignCenter().Text("COURT HEARING MINUTES").FontSize(16).Bold().FontColor(KuraBlue);

            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Case No: {_caseRegister?.CaseNo ?? "N/A"}").FontSize(10).SemiBold();
                    c.Item().Text($"Hearing Ref: {_hearing.Id.ToString()[..8].ToUpper()}").FontSize(9);
                });
                row.ConstantItem(150).AlignRight().Column(c =>
                {
                    c.Item().Text($"Hearing Date: {_hearing.HearingDate:dd/MM/yyyy}").FontSize(10).SemiBold();
                    c.Item().Text($"Time: {_hearing.HearingTime?.ToString(@"hh\:mm") ?? "N/A"}").FontSize(9);
                });
            });

            col.Item().PaddingTop(5).LineHorizontal(1.5f).LineColor(Colors.Black);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(10);

            // Case Details Section
            col.Item().Text("CASE DETAILS").FontSize(11).SemiBold();
            col.Item().PaddingLeft(15).Column(details =>
            {
                details.Spacing(4);
                ComposeInfoRow(details.Item(), "Hearing Type", _hearing.HearingType?.Name ?? "N/A");
                ComposeInfoRow(details.Item(), "Status", GetStatusDisplay(_hearing.HearingStatus?.Code), true);
                ComposeInfoRow(details.Item(), "Presiding Officer", _hearing.PresidingOfficer ?? "N/A");
            });

            // Hearing Minutes/Notes
            col.Item().PaddingTop(10).Text("PROCEEDINGS").FontSize(11).SemiBold();
            col.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(proceedings =>
            {
                proceedings.Item().MinHeight(100).Text(_hearing.MinuteNotes ?? "No minutes recorded.")
                    .FontSize(10);
            });

            // Outcome Section (if hearing is completed)
            if (_hearing.HearingOutcome != null)
            {
                col.Item().PaddingTop(10).Text("OUTCOME").FontSize(11).SemiBold();
                col.Item().Background(GetOutcomeBackgroundColor()).Padding(10).Column(outcome =>
                {
                    outcome.Spacing(4);
                    outcome.Item().Text($"Verdict: {_hearing.HearingOutcome.Name}").FontSize(11).SemiBold();
                });
            }

            // Adjournment Section (if adjourned)
            if (_hearing.HearingStatus?.Code == "adjourned" && _hearing.NextHearingDate.HasValue)
            {
                col.Item().PaddingTop(10).Background(Colors.Yellow.Lighten4).Padding(10).Column(adjourn =>
                {
                    adjourn.Spacing(4);
                    adjourn.Item().Text("ADJOURNMENT").FontSize(11).SemiBold().FontColor(Colors.Orange.Darken2);
                    adjourn.Item().Text($"Matter adjourned to: {_hearing.NextHearingDate:dd/MM/yyyy}").FontSize(10);

                    if (!string.IsNullOrEmpty(_hearing.AdjournmentReason))
                    {
                        adjourn.Item().Text($"Reason: {_hearing.AdjournmentReason}").FontSize(10).Italic();
                    }
                });
            }

            // Signature Section
            col.Item().PaddingTop(25).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Presiding Officer", _hearing.PresidingOfficer));
                row.ConstantItem(40);
                row.RelativeItem().Element(c => ComposeSignatureBlock(c, "Court Clerk"));
            });

            // Legal Notice
            col.Item().PaddingTop(20).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(notice =>
            {
                notice.Item().Text("IMPORTANT NOTICE").FontSize(9).SemiBold();
                notice.Item().Text("This document constitutes an official record of the court proceedings. " +
                    "Any party aggrieved by this decision may file an appeal within 14 days of this hearing date " +
                    "in accordance with the provisions of the Traffic Act Cap 403.").FontSize(8);
            });
        });
    }

    private string GetStatusDisplay(string? statusCode)
    {
        return statusCode switch
        {
            "scheduled" => "SCHEDULED",
            "in_progress" => "IN PROGRESS",
            "completed" => "COMPLETED",
            "adjourned" => "ADJOURNED",
            "cancelled" => "CANCELLED",
            _ => statusCode?.ToUpper() ?? "UNKNOWN"
        };
    }

    private string GetOutcomeBackgroundColor()
    {
        var outcomeCode = _hearing.HearingOutcome?.Code?.ToLower();
        return outcomeCode switch
        {
            "convicted" => Colors.Red.Lighten4,
            "acquitted" => Colors.Green.Lighten4,
            "dismissed" => Colors.Grey.Lighten4,
            "withdrawn" => Colors.Grey.Lighten4,
            _ => Colors.Blue.Lighten4
        };
    }
}
