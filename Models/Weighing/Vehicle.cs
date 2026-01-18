using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Weighing
{
    [Table("vehicles")]
    public class Vehicle : BaseEntity
    {

        [Required]
        [Column("reg_no")]
        [StringLength(50)]
        public string RegNo { get; set; }

        [Column("make")]
        [StringLength(100)]
        public string Make { get; set; }

        [Column("model")]
        [StringLength(100)]
        public string Model { get; set; }

        [Column("vehicle_type")]
        [StringLength(50)]
        public string VehicleType { get; set; }

        [Column("color")]
        [StringLength(50)]
        public string Color { get; set; }

        [Column("year_of_manufacture")]
        public int? YearOfManufacture { get; set; }

        [Column("chassis_no")]
        [StringLength(100)]
        public string ChassisNo { get; set; }

        [Column("engine_no")]
        [StringLength(100)]
        public string EngineNo { get; set; }

        [Column("owner_id")]
        public Guid? OwnerId { get; set; }

        [Column("transporter_id")]
        public Guid? TransporterId { get; set; }

        [Column("axle_configuration_id")]
        public Guid? AxleConfigurationId { get; set; }

        [Column("description")]
        public string Description { get; set; }

        /// <summary>
        /// Vector embedding for vehicle description (semantic search).
        /// 384 dimensions for all-MiniLM-L12-v2 model.
        /// NotMapped by default - explicitly configured for PostgreSQL only.
        /// </summary>
        [NotMapped]
        public Pgvector.Vector? DescriptionEmbedding { get; set; }

        [Column("is_flagged")]
        public bool IsFlagged { get; set; } = false;

        // Navigation properties
        [ForeignKey("OwnerId")]
        public virtual VehicleOwner Owner { get; set; }

        [ForeignKey("TransporterId")]
        public virtual Transporter Transporter { get; set; }

        [ForeignKey("AxleConfigurationId")]
        public virtual AxleConfiguration AxleConfiguration { get; set; }

        // Collections
        public virtual ICollection<Permit> Permits { get; set; } = new List<Permit>();
    }
}