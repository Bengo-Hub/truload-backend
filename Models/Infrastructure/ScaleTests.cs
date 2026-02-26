using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Infrastructure
{
    [Table("scale_tests")]
    public class ScaleTest : TenantAwareEntity
    {

        [Required]
        [Column("station_id")]
        public Guid StationId { get; set; }

        /// <summary>
        /// Direction/bound for bidirectional stations (A or B).
        /// Scale test is required per station per bound daily.
        /// </summary>
        [Column("bound")]
        [StringLength(10)]
        public string? Bound { get; set; }

        /// <summary>
        /// Type of test: calibration_weight or vehicle
        /// </summary>
        [Column("test_type")]
        [StringLength(50)]
        public string TestType { get; set; } = "calibration_weight";

        /// <summary>
        /// Vehicle plate number for vehicle-based tests
        /// </summary>
        [Column("vehicle_plate")]
        [StringLength(20)]
        public string? VehiclePlate { get; set; }

        /// <summary>
        /// Weighing mode: mobile or multideck
        /// </summary>
        [Column("weighing_mode")]
        [StringLength(20)]
        public string? WeighingMode { get; set; }

        /// <summary>
        /// Expected test weight in kg
        /// </summary>
        [Column("test_weight_kg")]
        public int? TestWeightKg { get; set; }

        /// <summary>
        /// Actual measured weight in kg
        /// </summary>
        [Column("actual_weight_kg")]
        public int? ActualWeightKg { get; set; }

        [Required]
        [Column("result")]
        [StringLength(20)]
        public string Result { get; set; } = "pass"; // pass, fail

        [Column("deviation_kg")]
        public int? DeviationKg { get; set; }

        [Column("details")]
        public string? Details { get; set; }

        [Required]
        [Column("carried_at")]
        public DateTime CarriedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("carried_by_id")]
        public Guid CarriedById { get; set; }

        // Navigation properties
        [ForeignKey("StationId")]
        public virtual Station? Station { get; set; }

        [ForeignKey("CarriedById")]
        public virtual ApplicationUser? CarriedBy { get; set; }
    }
}
