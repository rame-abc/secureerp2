using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Finance.Entities
{
    /// <summary>
    /// 🔒 FINAL ERP FINANCE HARDENING - Audit Trail Entity
    /// Cryptographically secure audit trail records
    /// </summary>
    public class AuditTrail : BaseEntity
    {
        [Required]
        public DateTime TransactionDate { get; set; }
        
        [Required]
        [StringLength(50)]
        public string TransactionType { get; set; } = string.Empty;
        
        public int EntityId { get; set; }
        
        [Required]
        [StringLength(50)]
        public string EntityType { get; set; } = string.Empty;
        
        [Required]
        [StringLength(20)]
        public string Action { get; set; } = string.Empty;
        
        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;
        
        public int UserId { get; set; }
        
        [StringLength(100)]
        public string UserName { get; set; } = string.Empty;
        
        [StringLength(45)]
        public string IPAddress { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string UserAgent { get; set; } = string.Empty;
        
        public string? OldValue { get; set; }
        
        public string? NewValue { get; set; }
        
        [Required]
        [StringLength(64)]
        public string PreviousHash { get; set; } = string.Empty;
        
        [Required]
        [StringLength(64)]
        public string CurrentHash { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 🔒 FINAL ERP FINANCE HARDENING - Financial Transaction Entity
    /// Enhanced financial transaction with audit capabilities
    /// </summary>
    public class FinancialTransaction : BaseEntity
    {
        [Required]
        public DateTime TransactionDate { get; set; }
        
        [Required]
        [StringLength(50)]
        public string TransactionType { get; set; } = string.Empty;
        
        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        
        public int AccountId { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal DebitAmount { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditAmount { get; set; }
        
        [StringLength(20)]
        public string Status { get; set; } = string.Empty;
        
        public string? ReferenceNumber { get; set; }
        
        public string? Notes { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// 🔒 FINAL ERP FINANCE HARDENING - Period Closing Entity
    /// Enhanced period closing with comprehensive tracking
    /// </summary>
    public class PeriodClosing : BaseEntity
    {
        [Required]
        public DateTime ClosingDate { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Description { get; set; } = string.Empty;
        
        public string? Notes { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string ClosedBy { get; set; } = string.Empty;
        
        public DateTime ClosedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsLocked { get; set; }
        
        public DateTime? LockedAt { get; set; }
        
        [StringLength(100)]
        public string LockedBy { get; set; } = string.Empty;
        
        // Closing statistics
        public int JournalEntriesClosed { get; set; }
        public int AccrualsGenerated { get; set; }
        public int ClosingEntriesCreated { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRevenueClosed { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalExpensesClosed { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetIncomeClosed { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// 🔒 FINAL ERP FINANCE HARDENING - Tax Calculation Entity
    /// Enhanced tax calculation with audit trail
    /// </summary>
    public class TaxCalculation : BaseEntity
    {
        [Required]
        public DateTime CalculationDate { get; set; }
        
        [Required]
        [StringLength(50)]
        public string TaxType { get; set; } = string.Empty; // VAT, Income Tax, Withholding Tax, etc.
        
        [Required]
        [StringLength(50)]
        public string CalculationBasis { get; set; } = string.Empty; // Invoice, Payroll, etc.
        
        public int SourceEntityId { get; set; } // Invoice ID, Payroll Run ID, etc.
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxableAmount { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(5,4)")]
        public decimal TaxRate { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }
        
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = string.Empty; // Calculated, Posted, Paid
        
        public DateTime? PostedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? PaidDate { get; set; }
        
        [StringLength(50)]
        public string Jurisdiction { get; set; } = string.Empty; // Federal, State, Local
        
        [StringLength(100)]
        public string TaxAuthority { get; set; } = string.Empty;
        
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        
        // GL posting tracking
        public DateTime? GLPostedDate { get; set; }
        public int? GLJournalEntryId { get; set; }
    }
}
