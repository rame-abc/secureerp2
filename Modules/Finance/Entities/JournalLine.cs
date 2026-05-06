using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Finance.Entities
{
    public class JournalLine
    {
        public int Id { get; set; }
        public int JournalEntryId { get; set; }
        public int AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string CostCenter { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string BusinessUnit { get; set; } = string.Empty;
        public string ProductLine { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string TaxCode { get; set; } = string.Empty;
        public decimal TaxAmount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; }
        public decimal ForeignAmount { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string ReconciliationKey { get; set; } = string.Empty;
        public bool IsReconciled { get; set; }
        public DateTime? ReconciledAt { get; set; }
        public string? ReconciledBy { get; set; }
        
        // 🔥 Database Relationships
        [ForeignKey("JournalEntryId")]
        public virtual JournalEntry JournalEntry { get; set; } = null!;
        
        // Additional navigation properties
        [ForeignKey("AccountId")]
        public virtual FinanceAccount Account { get; set; } = null!;
    }
}
