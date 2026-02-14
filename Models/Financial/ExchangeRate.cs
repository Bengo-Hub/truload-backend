using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Financial;

/// <summary>
/// Exchange rate record for currency conversion (primarily USD/KES).
/// Stores daily rates from manual entry or automated API sync.
/// </summary>
[Table("exchange_rates")]
public class ExchangeRate : BaseEntity
{
    /// <summary>
    /// Source currency (ISO 4217), e.g., "USD"
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string FromCurrency { get; set; } = "USD";

    /// <summary>
    /// Target currency (ISO 4217), e.g., "KES"
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string ToCurrency { get; set; } = "KES";

    /// <summary>
    /// Exchange rate value (e.g., 130.50 means 1 USD = 130.50 KES)
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal Rate { get; set; }

    /// <summary>
    /// Date this rate is effective for
    /// </summary>
    public DateOnly EffectiveDate { get; set; }

    /// <summary>
    /// Source of the rate: "manual", "api", "bank"
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Source { get; set; } = "manual";
}
