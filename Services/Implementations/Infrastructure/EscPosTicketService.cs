using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Services.Implementations.Infrastructure.ThermalDocuments;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Services.Implementations.Infrastructure;

/// <summary>
/// ESC/POS thermal ticket generator. Mirrors <see cref="QuestPdfService"/>'s
/// GenerateCommercialWeightTicketAsync: same result DTO, same station-to-organisation lookup
/// for branding, and the same first-weight-only-so-far "interim" determination - just a
/// different (byte-stream) output format targeted at 80mm thermal receipt printers instead of
/// an A4 page.
/// </summary>
public class EscPosTicketService : IEscPosTicketService
{
    private readonly TruLoadDbContext _context;

    public EscPosTicketService(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<byte[]> GenerateCommercialThermalTicketAsync(CommercialWeighingResultDto result, Guid stationId)
    {
        string? organizationName = null;
        if (stationId != Guid.Empty)
        {
            organizationName = await _context.Stations
                .Where(s => s.Id == stationId)
                .Select(s => s.Organization.Name)
                .FirstOrDefaultAsync();
        }

        // Interim = first weight captured but second weight not yet done - same rule
        // QuestPdfService uses for the PDF path.
        var isInterim = result.SecondWeightKg == null && result.FirstWeightKg != null;

        var document = new CommercialThermalTicketDocument(result, organizationName, isInterim);
        return document.Generate();
    }
}
