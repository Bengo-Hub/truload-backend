using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Weighing;

[Table("prohibition_orders")]
public class ProhibitionOrder : BaseEntity
{

    [Required]
    public Guid WeighingId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ProhibitionNo { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public Guid IssuedById { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Open"; // Open, Closed

    public string Reason { get; set; } = string.Empty;

    public DateTime? ClosedAt { get; set; }

    // Navigation Properties
    [ForeignKey("WeighingId")]
    public virtual WeighingTransaction Weighing { get; set; } = null!;

    [ForeignKey("IssuedById")]
    public virtual ApplicationUser IssuedBy { get; set; } = null!;
}
