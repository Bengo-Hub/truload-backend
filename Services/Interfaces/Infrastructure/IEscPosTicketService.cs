using TruLoad.Backend.DTOs.Weighing;

namespace TruLoad.Backend.Services.Interfaces.Infrastructure;

/// <summary>
/// Generates raw ESC/POS byte streams for 80mm thermal receipt printing. Sibling to
/// <see cref="IPdfService"/>'s A4 PDF weight ticket - same source data, different physical
/// output format. TruLoad does not push these bytes to a printer itself; the caller returns
/// them to the client, which hands them to the operator's own OS-level raw/generic printer
/// driver for delivery to the physical device.
/// </summary>
public interface IEscPosTicketService
{
    Task<byte[]> GenerateCommercialThermalTicketAsync(CommercialWeighingResultDto result, Guid stationId);
}
