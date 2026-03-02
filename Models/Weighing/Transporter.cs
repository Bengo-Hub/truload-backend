using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Weighing
{
    [Table("transporters")]
    public class Transporter : BaseEntity
    {

        /// <summary>
        /// Transporter code. Optional on create; when empty, backend auto-generates from name + suffix.
        /// </summary>
        [Column("code")]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [Column("name")]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [Column("registration_no")]
        [StringLength(100)]
        public string? RegistrationNo { get; set; }

        [Column("phone")]
        [StringLength(50)]
        public string? Phone { get; set; }

        [Column("email")]
        [StringLength(255)]
        public string? Email { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        [Column("ntac_no")]
        [StringLength(50)]
        public string? NtacNo { get; set; }

        // Collections
        public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    }
}
