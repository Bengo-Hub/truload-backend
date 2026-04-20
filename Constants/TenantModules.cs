namespace TruLoad.Backend.Constants;

/// <summary>
/// Module keys for tenant-based feature visibility. Used by sidebar and API.
/// </summary>
public static class TenantModules
{
    public const string Dashboard = "dashboard";
    public const string Weighing = "weighing";
    public const string Cases = "cases";
    public const string CaseManagement = "case_management";
    public const string SpecialReleases = "special_releases";
    public const string Prosecution = "prosecution";
    public const string Reporting = "reporting";
    public const string Users = "users";
    public const string Shifts = "shifts";
    public const string Technical = "technical";
    public const string FinancialInvoices = "financial_invoices";
    public const string FinancialReceipts = "financial_receipts";
    public const string SetupSecurity = "setup_security";
    public const string SetupAxle = "setup_axle";
    public const string SetupWeighingMetadata = "setup_weighing_metadata";
    public const string SetupActs = "setup_acts";
    public const string SetupSettings = "setup_settings";
    public const string SetupSystemConfig = "setup_system_config";
    // Commercial-only modules
    public const string TareRegister = "tare_register";
    public const string SetupTolerance = "setup_tolerance";

    /// <summary>All modules available to axle-load enforcement tenants.</summary>
    public static readonly IReadOnlyList<string> AllModules = new[]
    {
        Dashboard, Weighing, Cases, CaseManagement, SpecialReleases, Prosecution, Reporting,
        Users, Shifts, Technical, FinancialInvoices, FinancialReceipts,
        SetupSecurity, SetupAxle, SetupWeighingMetadata, SetupActs, SetupSettings, SetupSystemConfig
    };

    /// <summary>
    /// Default enabled modules for Commercial Weighing tenants.
    /// Excludes enforcement-only modules (Cases, Prosecution, SpecialReleases, SetupAxle, SetupActs).
    /// Includes commercial-only modules (TareRegister, SetupTolerance).
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultCommercialWeighingModules = new[]
    {
        Dashboard, Weighing, Reporting, Users, Shifts, Technical,
        FinancialInvoices, FinancialReceipts,
        SetupWeighingMetadata, SetupSettings, SetupSystemConfig, SetupSecurity,
        TareRegister, SetupTolerance
    };

    public const string TenantTypeCommercialWeighing = "CommercialWeighing";
    public const string TenantTypeAxleLoadEnforcement = "AxleLoadEnforcement";
}
