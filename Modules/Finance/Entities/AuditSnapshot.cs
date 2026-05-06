using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Finance.Entities
{
    /// <summary>
    /// 🔒 LAYER 1: External Audit Proof System
    /// This is what auditors and regulators care about.
    /// </summary>
    public class AuditSnapshot
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public int CompanyId { get; set; }
        
        [Required]
        [MaxLength(64)]
        public string SnapshotHash { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(64)]
        public string PreviousHash { get; set; } = string.Empty;
        
        [Required]
        public DateTime GeneratedAt { get; set; }
        
        [Required]
        [MaxLength(512)]
        public string Signature { get; set; } = string.Empty;
        
        [Required]
        public string LedgerData { get; set; } = string.Empty;
        
        [Required]
        public string Metadata { get; set; } = string.Empty;
        
        [Required]
        public string PublicKey { get; set; } = string.Empty;
        
        // Navigation properties
        [ForeignKey("CompanyId")]
        public virtual Company Company { get; set; }
    }
    
    /// <summary>
    /// Audit snapshot verification result
    /// </summary>
    public class AuditVerificationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime VerifiedAt { get; set; }
        public string VerifiedBy { get; set; } = string.Empty;
        public int TotalTransactions { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public string ComputedHash { get; set; } = string.Empty;
        public bool HashChainValid { get; set; }
        public bool SignatureValid { get; set; }
    }
    
    /// <summary>
    /// Audit snapshot export format
    /// </summary>
    public class AuditSnapshotExport
    {
        public Guid Id { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string SnapshotHash { get; set; } = string.Empty;
        public string PreviousHash { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public string Signature { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
        public string LedgerData { get; set; } = string.Empty;
        public string Metadata { get; set; } = string.Empty;
        public string Algorithm { get; set; } = "SHA256";
        public string Version { get; set; } = "1.0";
    }
}
