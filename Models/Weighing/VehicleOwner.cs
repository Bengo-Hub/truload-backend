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
        public string IdNoOrPassport { get; set; } = string.Empty;

        [Required]
        [Column("full_name")]
        [StringLength(255)]
        public string FullName { get; set; } = string.Empty;

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

        /// <summary>
        /// Email address linked to the vehicle owner's self-service portal account.
        /// Matched against auth-api user for portal authentication.
        /// Owners log in to view weighing history for their registered vehicles.
        /// </summary>
        [Column("portal_account_email")]
        [StringLength(255)]
        public string? PortalAccountEmail { get; set; }

        /// <summary>
        /// Auth-api user ID linked to this vehicle owner for portal access.
        /// </summary>
        [Column("portal_account_id")]
        public Guid? PortalAccountId { get; set; }

        // Collections
        public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    }
}
