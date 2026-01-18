using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Weighing
{
    [Table("vehicle_owners")]
    public class VehicleOwner : BaseEntity
    {

        [Required]
        [Column("id_no_or_passport")]
        [StringLength(50)]
        public string IdNoOrPassport { get; set; }

        [Required]
        [Column("full_name")]
        [StringLength(255)]
        public string FullName { get; set; }

        [Column("phone")]
        [StringLength(50)]
        public string Phone { get; set; }

        [Column("email")]
        [StringLength(255)]
        public string Email { get; set; }

        [Column("address")]
        public string Address { get; set; }

        [Column("ntac_no")]
        [StringLength(50)]
        public string NtacNo { get; set; }

        // Collections
        public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    }
}