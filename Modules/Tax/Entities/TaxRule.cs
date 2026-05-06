using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Tax.Entities
{
    public class TaxRule : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string TaxCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string TaxName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string TaxType { get; set; } = string.Empty; // VAT, IncomeTax, WithholdingTax, SalesTax, ServiceTax

        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal TaxRate { get; set; }

        [Required]
        [StringLength(20)]
        public string RateType { get; set; } = "Percentage"; // Percentage, Fixed

        [Required]
        [StringLength(50)]
        public string Jurisdiction { get; set; } = string.Empty; // Federal, State, Local, Country

        [Required]
        public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiryDate { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Active"; // Active, Inactive, Pending

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ThresholdAmount { get; set; } = 0; // Tax applies only above this amount

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxTaxAmount { get; set; } = 0; // Maximum tax amount (0 = no limit)

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public bool IsCompound { get; set; } = false; // Tax is calculated on top of other taxes

        [Required]
        public bool IsRecoverable { get; set; } = false; // Can be claimed as tax credit

        [StringLength(50)]
        public string Applicability { get; set; } = "All"; // All, Goods, Services, Specific

        // Navigation property for tax calculations
        public virtual ICollection<TaxCalculation> TaxCalculations { get; set; } = new List<TaxCalculation>();

        // Computed property for effective rate
        [NotMapped]
        public decimal EffectiveRate => RateType == "Percentage" ? TaxRate / 100 : TaxRate;

        // Computed property for tax description
        [NotMapped]
        public string TaxDescription => $"{TaxName} ({TaxRate}{(RateType == "Percentage" ? "%" : "")}) - {Jurisdiction}";

        // Computed property for validity
        [NotMapped]
        public bool IsValid => Status == "Active" && 
                              EffectiveDate <= DateTime.UtcNow && 
                              (!ExpiryDate.HasValue || ExpiryDate >= DateTime.UtcNow);
    }
}
