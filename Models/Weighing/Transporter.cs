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

        /// <summary>
        /// Email address linked to the transporter's self-service portal account.
        /// Matched against auth-api user for portal authentication.
        /// </summary>
        [Column("portal_account_email")]
        [StringLength(255)]
        public string? PortalAccountEmail { get; set; }

        /// <summary>
        /// Auth-api user ID linked to this transporter for portal access.
        /// </summary>
        [Column("portal_account_id")]
        public Guid? PortalAccountId { get; set; }

        // ── Treasury billing linkage ────────────────────────────────────────────

        /// <summary>
        /// treasury-api CRM contact reference for this transporter (marketflow Contact.Id).
        /// Stamped onto every commercial-weighing Invoice created for this transporter so
        /// treasury can build a running AR statement across all their weighing sessions,
        /// instead of posting each payment as an anonymous cash entry. Resolved via treasury's
        /// resolve-or-create contact lookup the first time an invoice is created for this
        /// transporter; null until then.
        /// </summary>
        [Column("crm_contact_id")]
        public Guid? CrmContactId { get; set; }

        /// <summary>
        /// Optional credit limit (KES) for on-account billing. Null = no credit extended
        /// (cash/pay-now only, the default). Enforced when OnAccountBilling is true.
        /// </summary>
        [Column("credit_limit_kes")]
        public decimal? CreditLimitKes { get; set; }

        /// <summary>
        /// When true, commercial weighing invoices for this transporter are NOT collected via an
        /// immediate payment intent — they post to treasury AR and the transporter settles later
        /// (portal "Pay outstanding invoice" action creates the intent on demand). Default false
        /// (pay-per-session, the existing behaviour).
        /// </summary>
        [Column("on_account_billing")]
        public bool OnAccountBilling { get; set; } = false;

        // Collections
        public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    }
}
