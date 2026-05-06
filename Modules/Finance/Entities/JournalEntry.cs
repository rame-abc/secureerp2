using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Finance.Entities
{
    public class JournalEntry
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; }
        public DateTime TransactionDate { get; set; }
        public DateTime JournalDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public JournalStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? UpdatedBy { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        
        // Additional properties for compatibility
        public string TransactionNumber { get; set; } = string.Empty;
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public bool IsBalanced { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string BatchNumber { get; set; } = string.Empty;
        public string SourceDocument { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovedBy { get; set; }
        public string? Notes { get; set; }
        
        // 🔥 Database Relationships
        public virtual ICollection<JournalLine> JournalLines { get; set; } = new List<JournalLine>();
        
        // Navigation properties
        [ForeignKey("CompanyId")]
        public virtual Company Company { get; set; } = null!;
    }

    public enum JournalStatus
    {
        Draft,
        Posted,
        Reversed,
        Cancelled
    }
}
