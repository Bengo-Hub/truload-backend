using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models;

/// <summary>
/// Station entity - Weighbridge/Mobile unit/Yard locations
/// </summary>
public class Station : TenantAwareEntity
{
    /// <summary>
    /// Unique station identifier code (e.g., "NRB-MOBILE-01")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the station
    /// </summary>
    public string Name { get; set; } = string.Empty;


    /// <summary>
    /// Station type: weigh_bridge, mobile_unit, yard
    /// </summary>
    public string StationType { get; set; } = "weigh_bridge";

    /// <summary>
    /// Indicates if this is the default station for new users without explicit assignments
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Indicates if this is the HQ (headquarters) station for the organisation.
    /// Users assigned to HQ can log in to any station and access data across stations when no station filter is set.
    /// </summary>
    public bool IsHq { get; set; } = false;

    public string? Location { get; set; } // Address/location description

    /// <summary>
    /// Road where this station is located (foreign key)
    /// </summary>
    public Guid? RoadId { get; set; }

    /// <summary>
    /// County where this station is located (foreign key)
    /// </summary>
    public Guid? CountyId { get; set; }

    /// <summary>
    /// Subcounty where this station is located (foreign key)
    /// </summary>
    public Guid? SubcountyId { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool SupportsBidirectional { get; set; } = false;

    /// <summary>
    /// Virtual station code for Bound A (for bidirectional stations)
    /// </summary>
    public string? BoundACode { get; set; }

    /// <summary>
    /// Virtual station code for Bound B (for bidirectional stations)
    /// </summary>
    public string? BoundBCode { get; set; }

    // ── Station configuration (setup.md) ──

    /// <summary>
    /// Start of this station's operating hours/shift boundary (EAT local clock), e.g. 06:00.
    /// Null means no configured operating-hours restriction. Uses TimeSpan for consistency with
    /// WeighingQueryHelpers.ApplyTimeOfDayFilter's time-of-day representation.
    /// </summary>
    public TimeSpan? OperatingHoursStart { get; set; }

    /// <summary>
    /// End of this station's operating hours/shift boundary (EAT local clock), e.g. 18:00.
    /// A value earlier than <see cref="OperatingHoursStart"/> means an overnight shift that wraps
    /// past midnight (same convention as ApplyTimeOfDayFilter). Null means no configured
    /// operating-hours restriction.
    /// </summary>
    public TimeSpan? OperatingHoursEnd { get; set; }

    /// <summary>
    /// Printer configuration for this station, stored as JSON (e.g.
    /// {"printerName":"...","model":"...","connection":"..."}). Metadata only for now - no real
    /// printer integration exists yet, so this is not wired to an actual print pipeline.
    /// </summary>
    public string? PrinterConfiguration { get; set; }

    /// <summary>
    /// Selected weight-ticket layout/template name for this station (commercial vs. enforcement
    /// ticket variants). Free-form string - no fixed template-name enum exists elsewhere yet.
    /// </summary>
    public string? TicketTemplate { get; set; }

    /// <summary>
    /// Advisory/informational default weighing mode for this station: "Enforcement" or "Commercial".
    /// Display/reporting/future-use only - does NOT gate the actual enforcement-vs-commercial
    /// routing logic, which is correctly derived from Organization.TenantType and remains
    /// unchanged (used by live enforcement tenants today).
    /// </summary>
    public string? DefaultWeighingMode { get; set; }

    // Navigation properties
    public Roads? Road { get; set; }
    public Counties? County { get; set; }
    public Infrastructure.Subcounty? Subcounty { get; set; }
}
