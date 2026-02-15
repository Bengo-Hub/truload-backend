using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.System;

/// <summary>
/// Configurable document naming conventions per document type.
/// Controls how document numbers are generated (prefix, date format, station code inclusion, etc.).
/// Adapted from ERP PrefixSettings pattern for TruLoad document types.
/// </summary>
[Table("document_conventions")]
public class DocumentConvention : BaseEntity
{
    /// <summary>
    /// Organization this convention belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Document type identifier (e.g., "weight_ticket", "invoice", "receipt", "charge_sheet").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for the document type.
    /// </summary>
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Optional prefix for the document number (e.g., "INV", "RCP", "CS").
    /// Empty for weight tickets which use station code as prefix.
    /// </summary>
    [MaxLength(10)]
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Whether to include the station code in the document number.
    /// </summary>
    public bool IncludeStationCode { get; set; } = true;

    /// <summary>
    /// Whether to include the bound direction code in the document number.
    /// Uses Station.BoundACode/BoundBCode if configured, otherwise "A"/"B".
    /// </summary>
    public bool IncludeBound { get; set; }

    /// <summary>
    /// Whether to include the date in the document number.
    /// </summary>
    public bool IncludeDate { get; set; } = true;

    /// <summary>
    /// Date format pattern for the document number (e.g., "yyyyMMdd", "ddMMyy").
    /// </summary>
    [MaxLength(20)]
    public string DateFormat { get; set; } = "yyyyMMdd";

    /// <summary>
    /// Whether to append the vehicle registration number to the document number.
    /// Typically only used for weight tickets.
    /// </summary>
    public bool IncludeVehicleReg { get; set; }

    /// <summary>
    /// Number of digits for zero-padded sequence number (e.g., 4 → "0001").
    /// </summary>
    public int SequencePadding { get; set; } = 4;

    /// <summary>
    /// Separator character between document number parts (e.g., "-", "/").
    /// </summary>
    [MaxLength(5)]
    public string Separator { get; set; } = "-";

    /// <summary>
    /// How often the sequence resets: "daily", "monthly", "yearly", "never".
    /// </summary>
    [MaxLength(20)]
    public string ResetFrequency { get; set; } = "daily";

    // Navigation
    [ForeignKey("OrganizationId")]
    public virtual Organization? Organization { get; set; }
}

/// <summary>
/// Well-known document type constants for type-safe access.
/// </summary>
public static class DocumentTypes
{
    public const string WeightTicket = "weight_ticket";
    public const string Invoice = "invoice";
    public const string Receipt = "receipt";
    public const string ChargeSheet = "charge_sheet";
    public const string ComplianceCertificate = "compliance_certificate";
    public const string ProhibitionOrder = "prohibition_order";
    public const string SpecialRelease = "special_release";
    public const string LoadCorrectionMemo = "load_correction_memo";
    public const string CourtMinutes = "court_minutes";
}
