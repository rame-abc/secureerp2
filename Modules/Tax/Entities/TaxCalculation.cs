using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Tax.Entities
{
    public class TaxCalculation : BaseEntity
    {
        [Required]
        public int TaxRuleId { get; set; }

        [ForeignKey("TaxRuleId")]
        public virtual TaxRule TaxRule { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string DocumentType { get; set; } = string.Empty; // Invoice, Payroll, Purchase

        [Required]
        public int DocumentId { get; set; } // Reference to Invoice, PayrollRun, etc.

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseAmount { get; set; } // Amount before tax

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxableAmount { get; set; } // Amount subject to tax

        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal TaxRate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } // BaseAmount + TaxAmount

        [Required]
        public DateTime CalculationDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Calculated"; // Calculated, Posted, Reversed

        [Required]
        public bool IsRecoverable { get; set; } = false;

        [Required]
        public DateTime? DueDate { get; set; }

        [Required]
        public DateTime? PaidDate { get; set; }

        [StringLength(500)]
        public string Notes { get; set; } = string.Empty;

        // Computed property for tax percentage
        [NotMapped]
        public decimal TaxPercentage => BaseAmount > 0 ? (TaxAmount / BaseAmount) * 100 : 0;

        // Computed property for days overdue
        [NotMapped]
        public int DaysOverdue => DueDate.HasValue && DueDate < DateTime.UtcNow && !PaidDate.HasValue
            ? (int)(DateTime.UtcNow - DueDate.Value).TotalDays
            : 0;

        // Computed property for payment status
        [NotMapped]
        public string PaymentStatus => PaidDate.HasValue ? "Paid" : 
                                      (DueDate.HasValue && DueDate < DateTime.UtcNow ? "Overdue" : "Pending");
    }
}
