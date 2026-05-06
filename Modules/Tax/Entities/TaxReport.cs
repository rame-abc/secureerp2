using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Tax.Entities
{
    public class TaxReport : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string ReportNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ReportName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string ReportType { get; set; } = string.Empty; // Monthly, Quarterly, Annual

        [Required]
        public DateTime PeriodStart { get; set; }

        [Required]
        public DateTime PeriodEnd { get; set; }

        [Required]
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Draft"; // Draft, Submitted, Approved, Rejected

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRevenue { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalVATCollected { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalVATPaid { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetVATLiability { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalIncomeTaxWithheld { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalWithholdingTaxCollected { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalTaxPayable { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalTaxPaid { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxBalance { get; set; }

        [Required]
        public DateTime? DueDate { get; set; }

        [Required]
        public DateTime? FiledDate { get; set; }

        [Required]
        public DateTime? PaymentDate { get; set; }

        [StringLength(1000)]
        public string Notes { get; set; } = string.Empty;

        [StringLength(500)]
        public string GeneratedBy { get; set; } = string.Empty;

        // Navigation property for tax report details
        public virtual ICollection<TaxReportDetail> TaxReportDetails { get; set; } = new List<TaxReportDetail>();

        // Computed property for period description
        [NotMapped]
        public string PeriodDescription => $"{PeriodStart:MMM dd, yyyy} - {PeriodEnd:MMM dd, yyyy}";

        // Computed property for tax efficiency
        [NotMapped]
        public decimal TaxEfficiency => TotalRevenue > 0 ? (TotalTaxPayable / TotalRevenue) * 100 : 0;

        // Computed property for days until due
        [NotMapped]
        public int DaysUntilDue => DueDate.HasValue ? (int)(DueDate.Value - DateTime.UtcNow).TotalDays : 0;

        // Computed property for filing status
        [NotMapped]
        public string FilingStatus => FiledDate.HasValue ? "Filed" : 
                                      (DueDate.HasValue && DueDate < DateTime.UtcNow ? "Overdue" : "Pending");
    }

    public class TaxReportDetail : BaseEntity
    {
        [Required]
        public int TaxReportId { get; set; }

        [ForeignKey("TaxReportId")]
        public virtual TaxReport TaxReport { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string TaxType { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxableAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxRate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxPaid { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxBalance { get; set; }

        [Required]
        public int TransactionCount { get; set; }

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        // Computed property for effective tax rate
        [NotMapped]
        public decimal EffectiveTaxRate => TaxableAmount > 0 ? (TaxAmount / TaxableAmount) * 100 : 0;
    }
}
