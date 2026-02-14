namespace TruLoad.Backend.Middleware;

/// <summary>
/// Mutable singleton holding rate limit values loaded from database settings.
/// Populated at startup and refreshable via admin endpoint without restart.
/// Thread-safe: values are read atomically (int/TimeSpan are value types).
/// </summary>
public class RateLimitSettings
{
    // Global
    public int GlobalAuthenticatedPermit { get; set; } = 600;
    public int GlobalAuthenticatedWindowMinutes { get; set; } = 1;
    public int GlobalAnonymousPermit { get; set; } = 30;

    // Named policies
    public int DashboardPermit { get; set; } = 800;
    public int ApiPermit { get; set; } = 200;
    public int WeighingPermit { get; set; } = 600;
    public int AutoweighPermit { get; set; } = 1000;
    public int AuthPermit { get; set; } = 10;
    public int AuthWindowMinutes { get; set; } = 5;
    public int ReportsPermit { get; set; } = 30;
    public int SearchPermit { get; set; } = 120;
}
