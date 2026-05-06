using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Invoice.Entities
{
    public class InvoiceItem : BaseEntity
    {
        [Required]
        public int InvoiceId { get; set; }

        [ForeignKey("InvoiceId")]
        public virtual Invoice Invoice { get; set; } = null!;

        [Required]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ItemCode { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Discount cannot be negative")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; } = 0;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [Required]
        [StringLength(50)]
        public string TaxType { get; set; } = "Standard"; // Standard, Exempt, Zero

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; } = 0.10m; // Default 10% tax

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; } = 0;

        // Computed property for effective unit price (after discount)
        [NotMapped]
        public decimal EffectiveUnitPrice => UnitPrice - Discount;

        // Computed property for line total before tax
        [NotMapped]
        public decimal LineTotalBeforeTax => Quantity * EffectiveUnitPrice;
    }
}
