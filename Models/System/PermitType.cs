using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Models.System
{
    /// <summary>
    /// Permit type master data (2A, 3A, 3B, Overload, Special)
    /// Defines weight extensions and validity rules for different permit types
    /// </summary>
    [Table("permit_types")]
    public class PermitType
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Permit type code (e.g., "2A", "3A", "3B", "OVERLOAD", "SPECIAL")
        /// </summary>
        [Required]
        [Column("code")]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Permit type name
        /// </summary>
        [Required]
        [Column("name")]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of permit type and applicability
        /// </summary>
        [Column("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Axle weight extension in kg (e.g., +3000 for 2A permit)
        /// </summary>
        [Required]
        [Column("axle_extension_kg")]
        public int AxleExtensionKg { get; set; }

        /// <summary>
        /// GVW extension in kg (e.g., +1000, +2000)
        /// </summary>
        [Required]
        [Column("gvw_extension_kg")]
        public int GvwExtensionKg { get; set; }

        /// <summary>
        /// Typical validity period in days
        /// </summary>
        [Column("validity_days")]
        public int? ValidityDays { get; set; }

        /// <summary>
        /// Whether this permit type is currently in use
        /// </summary>
        [Required]
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<Permit> Permits { get; set; } = new List<Permit>();
    }
}
