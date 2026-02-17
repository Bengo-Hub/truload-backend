using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.System;

/// <summary>
/// Tracks auto-incrementing document sequences per organization, station, and document type.
/// Uses database-level concurrency control to ensure unique sequential numbering.
/// Supports configurable reset frequency (daily, monthly, yearly, never).
/// </summary>
[Table("document_sequences")]
public class DocumentSequence : BaseEntity
{
    /// <summary>
    /// Organization this sequence belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Optional station for station-scoped sequences (e.g., weight tickets).
    /// Null for organization-wide sequences (e.g., invoices).
    /// </summary>
    public Guid? StationId { get; set; }

    /// <summary>
    /// Document type this sequence tracks.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Current sequence number (last used value).
    /// </summary>
    public int CurrentSequence { get; set; }

    /// <summary>
    /// How often the sequence resets: "daily", "monthly", "yearly", "never".
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string ResetFrequency { get; set; } = "daily";

    /// <summary>
    /// Date when the sequence was last reset.
    /// Used to determine if a reset is due.
    /// </summary>
    public DateTime LastResetDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Concurrency token for optimistic concurrency control.
    /// Prevents duplicate sequence numbers under concurrent access.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    // Navigation
    [ForeignKey("OrganizationId")]
    public virtual Organization? Organization { get; set; }

    [ForeignKey("StationId")]
    public virtual Station? Station { get; set; }
}
