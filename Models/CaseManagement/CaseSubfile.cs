using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Case subfiles B through J: Document Evidence, Expert Reports, Witness Statements, etc.
/// Supports multi-file uploads with flexible document types per case.
/// </summary>
public class CaseSubfile : BaseEntity
{

    /// <summary>
    /// Case register reference (required)
    /// </summary>
    [Required]
    public Guid CaseRegisterId { get; set; }

    /// <summary>
    /// Subfile type FK (B, C, D, E, F, G, H, I, J)
    /// B = Document Evidence (weight tickets, photos, ANPR footage, permit documents)
    /// C = Expert Reports (engineering/forensic reports)
    /// D = Witness Statements (inspector/driver/witnesses)
    /// E = Accused Statements & Reweigh/Compliance documents
    /// F = Investigation Diary (investigation steps, timelines)
    /// G = Charge Sheets, Bonds, NTAC, Arrest Warrants
    /// H = Accused Records (prior offences, identification documents)
    /// I = Covering Report (prosecutorial summary memo)
    /// J = Minute Sheets & Correspondences (court minutes, adjournments, correspondence)
    /// </summary>
    [Required]
    public Guid SubfileTypeId { get; set; }

    /// <summary>
    /// Subfile document name
    /// </summary>
    [MaxLength(100)]
    public string? SubfileName { get; set; }

    /// <summary>
    /// Document type: evidence, report, statement, diary, charge, bond, minute, etc.
    /// </summary>
    [MaxLength(100)]
    public string? DocumentType { get; set; }

    /// <summary>
    /// Text content (for searchable documents)
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Vector embedding for content (semantic search).
    /// 384 dimensions for all-MiniLM-L12-v2 model.
    /// NotMapped by default - explicitly configured for PostgreSQL only.
    /// </summary>
    [NotMapped]
    public Pgvector.Vector? ContentEmbedding { get; set; }

    /// <summary>
    /// File storage path (relative to configured base path)
    /// </summary>
    [MaxLength(500)]
    public string? FilePath { get; set; }

    /// <summary>
    /// File URL (for external/cloud storage)
    /// </summary>
    [MaxLength(500)]
    public string? FileUrl { get; set; }

    /// <summary>
    /// File MIME type (e.g., application/pdf, image/jpeg, video/mp4)
    /// </summary>
    [MaxLength(100)]
    public string? MimeType { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>
    /// File checksum (SHA-256) for integrity verification
    /// </summary>
    [MaxLength(64)]
    public string? Checksum { get; set; }

    /// <summary>
    /// User who uploaded the file
    /// </summary>
    public Guid? UploadedById { get; set; }

    /// <summary>
    /// Upload timestamp
    /// </summary>
    [Required]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata (JSON format)
    /// </summary>
    public string? Metadata { get; set; }

    // Navigation properties
    public virtual CaseRegister CaseRegister { get; set; } = null!;
    public virtual SubfileType SubfileType { get; set; } = null!;
}
