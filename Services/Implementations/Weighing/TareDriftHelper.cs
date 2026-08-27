namespace TruLoad.Backend.Services.Implementations.Weighing;

/// <summary>
/// Shared tare-drift percentage calculation. Originally a private method inside
/// CommercialReportGenerator (extracted there from the Tare Weight Audit report's per-transition
/// >5% drift check so Tare Verification could reuse it). Promoted to this standalone static helper
/// in Stage C so CommercialWeighingService's live tare-anomaly detection (Phase 7 MVP) can reuse the
/// exact same calculation instead of a second copy - both the reports and the live capture paths now
/// call this one implementation.
/// </summary>
public static class TareDriftHelper
{
    public static decimal ComputeTareDriftPercent(int currentTareKg, int previousTareKg)
    {
        return previousTareKg > 0
            ? Math.Abs((decimal)(currentTareKg - previousTareKg) / previousTareKg * 100)
            : 0m;
    }
}
