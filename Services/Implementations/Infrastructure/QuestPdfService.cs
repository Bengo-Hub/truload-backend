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

    public QuestPdfService(TruLoadDbContext context)
    {
        _context = context;
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

        // Get operational tolerance from settings
        var toleranceSetting = await _context.ApplicationSettings
            .Where(s => s.SettingKey == "weighing.operational_tolerance_kg")
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync();
        int operationalToleranceKg = int.TryParse(toleranceSetting, out var tol) ? tol : 200;

        return await Task.Run(() =>
        {
            var document = new WeightTicketDocument(
                transaction, organizationName, tenantType, orgLogoFile,
                operationalToleranceKg, primaryColor, secondaryColor);
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
