using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Infrastructure
{
    [Table("scale_tests")]
    public class ScaleTest : BaseEntity
    {

        [Required]
        [Column("station_id")]
        public Guid StationId { get; set; }

        [Column("test_weight_kg")]
        public int? TestWeightKg { get; set; }

        [Required]
        [Column("result")]
        [StringLength(20)]
        public string Result { get; set; } = "pass"; // pass, fail

        [Column("deviation_kg")]
        public int? DeviationKg { get; set; }

        [Column("details")]
        public string Details { get; set; }

        [Required]
        [Column("carried_at")]
        public DateTime CarriedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("carried_by_id")]
        public Guid CarriedById { get; set; }

        // Navigation properties
        [ForeignKey("StationId")]
        public virtual Station Station { get; set; }

        [ForeignKey("CarriedById")]
        public virtual ApplicationUser CarriedBy { get; set; }
    }
}