using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.CaseManagement;

/// <summary>
/// Service implementation for managing case parties.
/// Handles adding, updating, and removing parties (officers, defendants, witnesses) from cases.
/// </summary>
public class CasePartyService : ICasePartyService
{
    private readonly TruLoadDbContext _context;

    public CasePartyService(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CasePartyDto>> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        var parties = await _context.CaseParties
            .Include(p => p.CaseRegister)
            .Include(p => p.User)
            .Include(p => p.Driver)
            .Include(p => p.VehicleOwner)
            .Include(p => p.Transporter)
            .Where(p => p.CaseRegisterId == caseRegisterId && p.DeletedAt == null)
            .OrderBy(p => p.AddedAt)
            .ToListAsync(ct);

        return parties.Select(MapToDto);
    }

    public async Task<CasePartyDto> AddPartyAsync(Guid caseRegisterId, AddCasePartyRequest request, CancellationToken ct = default)
    {
        // Verify case exists
        var caseRegister = await _context.CaseRegisters.FindAsync(new object[] { caseRegisterId }, ct)
            ?? throw new InvalidOperationException($"Case {caseRegisterId} not found");

        var party = new CaseParty
        {
            Id = Guid.NewGuid(),
            CaseRegisterId = caseRegisterId,
            PartyRole = request.PartyRole,
            UserId = request.UserId,
            DriverId = request.DriverId,
            VehicleOwnerId = request.VehicleOwnerId,
            TransporterId = request.TransporterId,
            ExternalName = request.ExternalName,
            ExternalIdNumber = request.ExternalIdNumber,
            ExternalPhone = request.ExternalPhone,
            Notes = request.Notes,
            IsCurrentlyActive = true,
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.CaseParties.Add(party);
        await _context.SaveChangesAsync(ct);

        // Reload with navigation properties
        return (await GetPartyByIdAsync(party.Id, ct))!;
    }

    public async Task<CasePartyDto> UpdatePartyAsync(Guid partyId, UpdateCasePartyRequest request, CancellationToken ct = default)
    {
        var party = await _context.CaseParties.FindAsync(new object[] { partyId }, ct)
            ?? throw new InvalidOperationException($"Party {partyId} not found");

        if (party.DeletedAt != null)
            throw new InvalidOperationException("Cannot update a deleted party");

        if (!string.IsNullOrWhiteSpace(request.Notes))
            party.Notes = request.Notes;

        if (request.IsCurrentlyActive.HasValue)
        {
            party.IsCurrentlyActive = request.IsCurrentlyActive.Value;
            if (!request.IsCurrentlyActive.Value)
                party.RemovedAt = DateTime.UtcNow;
        }

        party.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return (await GetPartyByIdAsync(partyId, ct))!;
    }

    public async Task<bool> RemovePartyAsync(Guid partyId, CancellationToken ct = default)
    {
        var party = await _context.CaseParties.FindAsync(new object[] { partyId }, ct);
        if (party == null)
            return false;

        party.IsCurrentlyActive = false;
        party.RemovedAt = DateTime.UtcNow;
        party.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Internal helper to reload a party with navigation properties.
    /// </summary>
    private async Task<CasePartyDto?> GetPartyByIdAsync(Guid id, CancellationToken ct = default)
    {
        var party = await _context.CaseParties
            .Include(p => p.CaseRegister)
            .Include(p => p.User)
            .Include(p => p.Driver)
            .Include(p => p.VehicleOwner)
            .Include(p => p.Transporter)
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, ct);

        return party == null ? null : MapToDto(party);
    }

    private CasePartyDto MapToDto(CaseParty p)
    {
        return new CasePartyDto
        {
            Id = p.Id,
            CaseRegisterId = p.CaseRegisterId,
            PartyRole = p.PartyRole,
            UserId = p.UserId,
            UserName = p.User?.FullName,
            DriverId = p.DriverId,
            DriverName = p.Driver?.FullNames,
            VehicleOwnerId = p.VehicleOwnerId,
            VehicleOwnerName = p.VehicleOwner?.FullName,
            TransporterId = p.TransporterId,
            TransporterName = p.Transporter?.Name,
            ExternalName = p.ExternalName,
            ExternalIdNumber = p.ExternalIdNumber,
            ExternalPhone = p.ExternalPhone,
            Notes = p.Notes,
            IsCurrentlyActive = p.IsCurrentlyActive,
            AddedAt = p.AddedAt,
            RemovedAt = p.RemovedAt
        };
    }
}
