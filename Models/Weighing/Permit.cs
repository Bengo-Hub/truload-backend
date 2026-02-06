using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Weighing
{
    [Table("permits")]
    public class Permit : BaseEntity
    {

        [Required]
        [Column("permit_no")]
        [StringLength(100)]
        public string PermitNo { get; set; } = string.Empty;

        [Required]
        [Column("vehicle_id")]
        public Guid VehicleId { get; set; }

        [Required]
        [Column("permit_type_id")]
        public Guid PermitTypeId { get; set; }

        [Column("axle_extension_kg")]
        public int? AxleExtensionKg { get; set; }

        [Column("gvw_extension_kg")]
        public int? GvwExtensionKg { get; set; }

        [Required]
        [Column("valid_from")]
        public DateTime ValidFrom { get; set; }

        [Required]
        [Column("valid_to")]
        public DateTime ValidTo { get; set; }

        [Column("issuing_authority")]
        [StringLength(255)]
        public string? IssuingAuthority { get; set; }

        [Required]
        [Column("status")]
        [StringLength(20)]
        public string Status { get; set; } = "active"; // active, expired, revoked

        // Navigation properties
        [ForeignKey("VehicleId")]
        public virtual Vehicle? Vehicle { get; set; }

        [ForeignKey("PermitTypeId")]
        public virtual PermitType? PermitType { get; set; }
    }
}
