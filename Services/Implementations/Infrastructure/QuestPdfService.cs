using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Models.Prosecution;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Services.Interfaces.Infrastructure;
using TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

namespace TruLoad.Backend.Services.Implementations.Infrastructure;

public class QuestPdfService : IPdfService
{
    private readonly TruLoadDbContext _context;
    private readonly Repositories.Weighing.Interfaces.IToleranceRepository _toleranceRepository;

    public QuestPdfService(TruLoadDbContext context, Repositories.Weighing.Interfaces.IToleranceRepository toleranceRepository)
    {
        _context = context;
        _toleranceRepository = toleranceRepository;
        // License is registered in Program.cs
    }

    public async Task<byte[]> GenerateWeightTicketAsync(WeighingTransaction transaction)
    {
        // Resolve organization info from station for branding (logo, colors, name, tenant type)
        string? organizationName = null;
        string? tenantType = null;
        string? orgLogoFile = null;
        string? primaryColor = null;
        string? secondaryColor = null;
        if (transaction.StationId != Guid.Empty)
        {
            var orgInfo = await _context.Stations
                .Where(s => s.Id == transaction.StationId)
                .Select(s => new {
                    s.Organization.Name,
                    s.Organization.TenantType,
                    s.Organization.LogoUrl,
                    s.Organization.PrimaryColor,
                    s.Organization.SecondaryColor
                })
                .FirstOrDefaultAsync();
            organizationName = orgInfo?.Name;
            tenantType = orgInfo?.TenantType;
            primaryColor = orgInfo?.PrimaryColor;
            secondaryColor = orgInfo?.SecondaryColor;
            if (!string.IsNullOrEmpty(orgInfo?.LogoUrl))
            {
                orgLogoFile = Path.GetFileName(orgInfo.LogoUrl);
            }
        }

        // Use persisted compliance results from the transaction (Single Source of Truth)
        int operationalToleranceKg = transaction.OperationalAllowanceUsed;
        int gvwToleranceKg = transaction.GvwToleranceKg;
        string gvwToleranceDisplay = transaction.GvwToleranceDisplay ?? "0%";
        string axleToleranceDisplay = transaction.AxleToleranceDisplay ?? "0%";

        return await Task.Run(() =>
        {
            var document = new WeightTicketDocument(
                transaction, organizationName, tenantType, orgLogoFile,
                operationalToleranceKg, primaryColor, secondaryColor,
                gvwToleranceKg, gvwToleranceDisplay, axleToleranceDisplay);
            return document.Generate();
        });
    }

    public async Task<byte[]> GenerateProhibitionOrderAsync(ProhibitionOrder order)
    {
        // ProhibitionOrder is a legal document - keeps Kenya Police + Court of Arms logos
        return await Task.Run(() =>
        {
            var document = new ProhibitionOrderDocument(order);
            return document.Generate();
        });
    }

    public async Task<byte[]> GeneratePermitAsync(Permit permit)
    {
        return await Task.Run(() =>
        {
            var document = new PermitDocument(permit);
            return document.Generate();
        });
    }

    public async Task<byte[]> GenerateLoadCorrectionMemoAsync(Guid caseRegisterId, WeighingTransaction originalWeighing, WeighingTransaction reweighing)
    {
        return await Task.Run(async () =>
        {
            // Get case number for the document
            var caseRegister = await _context.CaseRegisters
                .Where(c => c.Id == caseRegisterId)
                .Select(c => c.CaseNo)
                .FirstOrDefaultAsync();

            // Resolve org logo from station
            string? orgLogoFile = null;
            if (originalWeighing.StationId.HasValue && originalWeighing.StationId.Value != Guid.Empty)
                orgLogoFile = await ResolveOrgLogoFromStationAsync(originalWeighing.StationId.Value);

            var document = new LoadCorrectionMemoDocument(
                originalWeighing,
                reweighing,
                caseRegister ?? caseRegisterId.ToString(),
                orgLogoFile);
            return document.Generate();
        });
    }

    public async Task<byte[]> GenerateComplianceCertificateAsync(Guid caseRegisterId, WeighingTransaction reweighing)
    {
        return await Task.Run(async () =>
        {
            // Get case number for the document
            var caseRegister = await _context.CaseRegisters
                .Where(c => c.Id == caseRegisterId)
                .Select(c => c.CaseNo)
                .FirstOrDefaultAsync();

            var certificateNo = $"COMP-{caseRegister ?? caseRegisterId.ToString()}";

            // Resolve org logo from station
            string? orgLogoFile = null;
            if (reweighing.StationId.HasValue && reweighing.StationId.Value != Guid.Empty)
                orgLogoFile = await ResolveOrgLogoFromStationAsync(reweighing.StationId.Value);

            var document = new ComplianceCertificateDocument(
                reweighing,
                caseRegister ?? caseRegisterId.ToString(),
                certificateNo,
                orgLogoFile);
            return document.Generate();
        });
    }

    public async Task<byte[]> GenerateSpecialReleaseCertificateAsync(SpecialRelease specialRelease)
    {
        // Resolve org logo from the case register's weighing station
        string? orgLogoFile = null;
        var weighingId = specialRelease.CaseRegister?.WeighingId;
        if (weighingId.HasValue && weighingId.Value != Guid.Empty)
        {
            var stationId = specialRelease.CaseRegister?.Weighing?.StationId
                ?? await _context.WeighingTransactions
                    .Where(w => w.Id == weighingId.Value)
                    .Select(w => w.StationId)
                    .FirstOrDefaultAsync();

            if (stationId.HasValue && stationId.Value != Guid.Empty)
                orgLogoFile = await ResolveOrgLogoFromStationAsync(stationId.Value);
        }

        return await Task.Run(() =>
        {
            var document = new SpecialReleaseCertificateDocument(specialRelease, orgLogoFile);
            return document.Generate();
        });
    }

    public async Task<byte[]> GenerateChargeSheetAsync(Guid prosecutionCaseId, CancellationToken ct = default)
    {
        var prosecutionCase = await _context.ProsecutionCases
            .Include(p => p.CaseRegister)
            .Include(p => p.Weighing)
                .ThenInclude(w => w!.Vehicle)
            .Include(p => p.Weighing)
                .ThenInclude(w => w!.Station)
            .Include(p => p.ProsecutionOfficer)
            .Include(p => p.Act)
            .FirstOrDefaultAsync(p => p.Id == prosecutionCaseId && p.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Prosecution case {prosecutionCaseId} not found");

        var document = new ChargeSheetDocument(prosecutionCase, prosecutionCase.CaseRegister);
        return document.Generate();
    }

    public async Task<byte[]> GenerateCourtMinutesAsync(Guid hearingId, CancellationToken ct = default)
    {
        var hearing = await _context.CourtHearings
            .Include(h => h.CaseRegister)
            .Include(h => h.HearingType)
            .Include(h => h.HearingStatus)
            .Include(h => h.HearingOutcome)
            .FirstOrDefaultAsync(h => h.Id == hearingId && h.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Court hearing {hearingId} not found");

        var document = new CourtMinutesDocument(hearing, hearing.CaseRegister);
        return document.Generate();
    }

    public async Task<byte[]> GenerateCoverPageAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        var caseRegister = await _context.CaseRegisters
            .Include(c => c.Weighing)
                .ThenInclude(w => w!.Station)
            .Include(c => c.ComplainantOfficer)
            .Include(c => c.DetentionStation)
            .Include(c => c.CourtHearings)
                .ThenInclude(h => h.HearingType)
            .FirstOrDefaultAsync(c => c.Id == caseRegisterId && c.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Case register {caseRegisterId} not found");

        // Resolve court name
        var courtName = caseRegister.CourtId.HasValue
            ? await _context.Set<Models.CaseManagement.Court>()
                .Where(c => c.Id == caseRegister.CourtId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct) ?? "N/A"
            : "N/A";

        // Resolve investigating officer
        var investigatingOfficer = caseRegister.InvestigatingOfficerId.HasValue
            ? await _context.Users
                .Where(u => u.Id == caseRegister.InvestigatingOfficerId.Value)
                .Select(u => new { u.FullName })
                .FirstOrDefaultAsync(ct)
            : null;

        // Resolve driver (accused) info
        var driver = caseRegister.DriverId.HasValue
            ? await _context.Set<Models.Weighing.Driver>()
                .Where(d => d.Id == caseRegister.DriverId.Value)
                .Select(d => new { d.FullNames, d.Surname })
                .FirstOrDefaultAsync(ct)
            : null;
        var accusedName = driver != null ? $"{driver.FullNames} {driver.Surname}".Trim() : string.Empty;

        // Resolve prosecution case for charge details
        var prosecutionCase = await _context.ProsecutionCases
            .Include(p => p.Act)
            .Include(p => p.ProsecutionOfficer)
            .FirstOrDefaultAsync(p => p.CaseRegisterId == caseRegisterId && p.DeletedAt == null, ct);

        // Build hearing entries
        var hearings = caseRegister.CourtHearings
            .OrderBy(h => h.HearingDate)
            .Select(h => new CoverPageHearing
            {
                Date = h.HearingDate,
                Time = h.HearingTime,
                TypeCode = MapHearingTypeCode(h.HearingType?.Code),
                Comments = h.MinuteNotes ?? string.Empty
            })
            .ToList();

        var stationName = caseRegister.DetentionStation?.Name
            ?? caseRegister.Weighing?.Station?.Name
            ?? "N/A";

        var data = new CoverPageData
        {
            PoliceCaseFileNo = caseRegister.PoliceCaseFileNo ?? "N/A",
            ObNo = caseRegister.ObNo ?? "N/A",
            CourtFileNo = caseRegister.CourtCaseNo ?? "N/A",
            CourtName = courtName,
            PoliceStation = stationName,
            Division = string.Empty,
            Province = string.Empty,
            Hearings = hearings,
            ComplainantName = caseRegister.ComplainantOfficer?.FullName ?? string.Empty,
            AccusedName = accusedName,
            ChargeAndSection = prosecutionCase?.Act?.Name ?? caseRegister.ViolationDetails ?? "N/A",
            ResultOfCase = caseRegister.ClosingReason ?? string.Empty,
            InvestigatingOfficerName = investigatingOfficer?.FullName ?? string.Empty,
            CourtProsecutor = prosecutionCase?.ProsecutionOfficer?.FullName ?? string.Empty,
        };

        var document = new CoverPageDocument(data);
        return document.Generate();
    }

    /// <summary>
    /// Maps hearing type codes to cover page abbreviations.
    /// M = Mention, H = Hearing, POG = Plea of Guilty, PONGE = Plea of Not Guilty
    /// </summary>
    private static string MapHearingTypeCode(string? code)
    {
        if (string.IsNullOrEmpty(code)) return string.Empty;
        return code.ToUpperInvariant() switch
        {
            "MENTION" => "M",
            "HEARING" => "H",
            "PLEA_OF_GUILTY" or "POG" => "POG",
            "PLEA_OF_NOT_GUILTY" or "PONGE" => "PONGE",
            _ => code
        };
    }

    public async Task<byte[]> GenerateInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.ProsecutionCase)
                .ThenInclude(p => p!.CaseRegister)
            .Include(i => i.ProsecutionCase)
                .ThenInclude(p => p!.Weighing)
                    .ThenInclude(w => w!.Station)
            .Include(i => i.Receipts)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found");

        // Get organization name from station or default
        var organizationName = invoice.ProsecutionCase?.Weighing?.Station?.Name;

        // Resolve org logo from station
        string? orgLogoFile = null;
        var stationId = invoice.ProsecutionCase?.Weighing?.StationId;
        if (stationId.HasValue && stationId.Value != Guid.Empty)
        {
            orgLogoFile = await ResolveOrgLogoFromStationAsync(stationId.Value);
        }

        // Commercial/treasury invoices should not show the eCitizen secondary logo
        var showSecondaryLogo = invoice.InvoiceType != "commercial_weighing_fee";
        var document = new InvoiceDocument(invoice, organizationName, orgLogoFile: orgLogoFile, showSecondaryLogo: showSecondaryLogo);
        return document.Generate();
    }

    public async Task<byte[]> GenerateReceiptAsync(Guid receiptId, CancellationToken ct = default)
    {
        var receipt = await _context.Receipts
            .Include(r => r.Invoice)
                .ThenInclude(i => i!.ProsecutionCase)
                    .ThenInclude(p => p!.CaseRegister)
            .Include(r => r.Invoice)
                .ThenInclude(i => i!.ProsecutionCase)
                    .ThenInclude(p => p!.Weighing)
                        .ThenInclude(w => w!.Vehicle)
            .Include(r => r.Invoice)
                .ThenInclude(i => i!.ProsecutionCase)
                    .ThenInclude(p => p!.Weighing)
                        .ThenInclude(w => w!.Station)
            .Include(r => r.Invoice)
                .ThenInclude(i => i!.Receipts)
            .Include(r => r.ReceivedBy)
            .FirstOrDefaultAsync(r => r.Id == receiptId && r.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Receipt {receiptId} not found");

        // Resolve org logo from station
        string? orgLogoFile = null;
        var stationId = receipt.Invoice?.ProsecutionCase?.Weighing?.StationId;
        if (stationId.HasValue && stationId.Value != Guid.Empty)
        {
            orgLogoFile = await ResolveOrgLogoFromStationAsync(stationId.Value);
        }

        // Commercial/treasury receipts should not show the eCitizen secondary logo
        var showSecondaryLogo = receipt.Invoice?.InvoiceType != "commercial_weighing_fee";
        var document = new ReceiptDocument(receipt, orgLogoFile: orgLogoFile, showSecondaryLogo: showSecondaryLogo);
        return document.Generate();
    }

    /// <summary>
    /// Resolves the organization logo filename from a station's organization.
    /// Extracts the filename from Organization.LogoUrl for use in PDF rendering.
    /// </summary>
    private async Task<string?> ResolveOrgLogoFromStationAsync(Guid stationId)
    {
        if (stationId == Guid.Empty) return null;

        var logoUrl = await _context.Stations
            .Where(s => s.Id == stationId)
            .Select(s => s.Organization.LogoUrl)
            .FirstOrDefaultAsync();

        return !string.IsNullOrEmpty(logoUrl) ? Path.GetFileName(logoUrl) : null;
    }
}
