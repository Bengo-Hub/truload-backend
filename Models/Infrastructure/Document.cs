using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Infrastructure;

[Table("documents")]
public class Document : BaseEntity
{

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string MimeType { get; set; } = string.Empty;

    [Required]
    public long FileSize { get; set; }

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? FileUrl { get; set; }

    [MaxLength(64)]
    public string? Checksum { get; set; } // SHA-256

    [Required]
    [MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty; // e.g., WeightTicket, ProhibitionOrder, Invoice

    /// <summary>
    /// Polymorphic association or specific FKs can be added here.
    /// For simplicity in MVP, we track the related entity type and ID.
    /// </summary>
    [MaxLength(100)]
    public string? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }

    public Guid? UploadedById { get; set; }

    // Navigation Properties
    [ForeignKey("UploadedById")]
    public virtual ApplicationUser? UploadedBy { get; set; }
}
