using TruLoad.Backend.DTOs.Weighing;

namespace TruLoad.Backend.Services.Implementations.Infrastructure.ThermalDocuments;

/// <summary>
/// Composes the ESC/POS byte stream for a commercial weight ticket, formatted for an 80mm
/// thermal receipt printer. Mirrors <c>CommercialWeightTicketDocument</c> (the A4 PDF
/// renderer) field-for-field and reuses its same interim/final and billing-visibility
/// conditions, but as a compact single-column receipt layout rather than the A4 page layout -
/// the two are intentionally not styled the same way, since a narrow receipt cannot show the
/// PDF's boxed/tabular sections.
/// </summary>
public sealed class CommercialThermalTicketDocument
{
    private readonly CommercialWeighingResultDto _result;
    private readonly string? _organizationName;
    private readonly bool _isInterim;
    private readonly EscPosBuilder _b = new();

    public CommercialThermalTicketDocument(CommercialWeighingResultDto result, string? organizationName, bool isInterim)
    {
        _result = result;
        _organizationName = organizationName;
        _isInterim = isInterim;
    }

    public byte[] Generate()
    {
        _b.AlignCenter();

        if (_isInterim)
        {
            _b.Bold(true);
            _b.Wrap("*** INTERIM TICKET - FIRST PASS ONLY ***");
            _b.Bold(false);
            _b.Divider();
        }

        if (!string.IsNullOrWhiteSpace(_organizationName))
        {
            _b.Bold(true).Line(_organizationName).Bold(false);
        }
        _b.Line(_result.StationName ?? "Weighbridge Station");
        _b.Bold(true).Line("WEIGHT TICKET").Bold(false);
        _b.Divider();

        _b.AlignLeft();
        _b.KeyValue("Ticket No:", _result.TicketNumber);
        _b.KeyValue("Date:", _result.WeighedAt.ToString("dd/MM/yyyy HH:mm"));
        _b.KeyValue("Status:", StatusLabel());
        _b.Divider();

        _b.KeyValue("Vehicle Reg:", _result.VehicleRegNumber);
        if (!string.IsNullOrWhiteSpace(_result.TrailerRegNo))
            _b.KeyValue("Trailer Reg:", _result.TrailerRegNo);
        if (!string.IsNullOrWhiteSpace(_result.TransporterName))
            _b.KeyValue("Transporter:", _result.TransporterName);
        if (!string.IsNullOrWhiteSpace(_result.DriverName))
            _b.KeyValue("Driver:", _result.DriverName);
        _b.Divider();

        ComposeWeightSummary();

        if (!string.IsNullOrWhiteSpace(_result.Remarks))
        {
            _b.Divider();
            _b.Bold(true).Line("REMARKS").Bold(false);
            _b.Wrap(_result.Remarks);
        }

        // Billing (weighing fee) - only when the org actually charges a fee for this
        // transaction, same condition CommercialWeightTicketDocument uses for its PDF
        // Billing section (third-party-weighbridge organisations with a fee configured).
        if (_result.InvoiceAmountKes.HasValue && _result.InvoiceAmountKes.Value > 0)
        {
            _b.Divider();
            _b.AlignCenter().Bold(true).Line("BILLING").Bold(false).AlignLeft();
            _b.KeyValue("Weighing Fee (KES):", _result.InvoiceAmountKes.Value.ToString("N2"));
            if (!string.IsNullOrWhiteSpace(_result.InvoiceNo))
                _b.KeyValue("Invoice No:", _result.InvoiceNo);
            if (!string.IsNullOrWhiteSpace(_result.InvoiceStatus))
                _b.KeyValue("Invoice Status:", Capitalize(_result.InvoiceStatus));
        }

        _b.Divider();
        _b.KeyValue("Weighed By:", _result.WeighedByUserName ?? "N/A");

        _b.AlignCenter();
        _b.Line($"Printed: {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC");
        _b.Line("Thank you");

        _b.FeedAndCut();

        return _b.ToArray();
    }

    private void ComposeWeightSummary()
    {
        _b.AlignCenter().Bold(true).Line("WEIGHT SUMMARY").Bold(false).AlignLeft();

        var tareLabel = "Tare Weight:";
        var tareValue = FormatWeight(_result.TareWeightKg) + (string.IsNullOrWhiteSpace(_result.TareSource) ? "" : $" ({Capitalize(_result.TareSource)})");
        _b.KeyValue(tareLabel, tareValue);
        _b.KeyValue("Gross Weight:", FormatWeight(_result.GrossWeightKg));

        _b.Divider('.');

        _b.AlignCenter();
        _b.Line("NET WEIGHT");
        _b.DoubleSize(true).Bold(true);
        _b.Line(FormatWeight(_result.NetWeightKg));
        _b.Bold(false).DoubleSize(false);
        _b.AlignLeft();

        if (_result.QualityDeductionKg.HasValue && _result.QualityDeductionKg.Value > 0)
        {
            _b.Divider('.');
            _b.KeyValue("Quality Deduction:", $"-{_result.QualityDeductionKg.Value:N0} kg");
            _b.Bold(true);
            _b.KeyValue("Adjusted Net Weight:", FormatWeight(_result.AdjustedNetWeightKg));
            _b.Bold(false);
        }
    }

    /// <summary>
    /// Same effective-status derivation as CommercialWeightTicketDocument.ComposeStatusBadge:
    /// ControlStatus only ever persists Pending/Complete/ToleranceExceeded/Voided, so the
    /// "first weight captured, second not yet done" stage is inferred from _isInterim.
    /// </summary>
    private string StatusLabel()
    {
        var effectiveStatus = _isInterim && _result.ControlStatus == "Pending"
            ? "FirstWeightCaptured"
            : _result.ControlStatus;

        return effectiveStatus switch
        {
            "Complete" => "COMPLETE",
            "ToleranceExceeded" => "TOLERANCE EXCEEDED",
            "Voided" => "VOIDED",
            "FirstWeightCaptured" => "FIRST WEIGHT CAPTURED",
            "Pending" => "PENDING",
            _ => string.IsNullOrEmpty(effectiveStatus) ? "PENDING" : effectiveStatus.ToUpperInvariant()
        };
    }

    private static string FormatWeight(int? weightKg) => weightKg.HasValue ? $"{weightKg.Value:N0} kg" : "--- kg";

    private static string Capitalize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return char.ToUpper(value[0]) + value[1..];
    }
}
