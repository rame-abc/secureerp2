using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Finance.Entities
{
    /// <summary>
    /// 🛡️ LAYER 3: Maker-Checker (Human Safety)
    /// User A creates journal, User B approves, Only then → POST
    /// This alone prevents most real-world disasters
    /// </summary>
    public class Approval
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public int CompanyId { get; set; }
        
        /// <summary>
        /// 🔥 Entity being approved (Journal, Invoice, etc.)
        /// </summary>
        [Required]
        public Guid EntityId { get; set; }
        
        /// <summary>
        /// 🔥 Entity type for polymorphic approval
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string EntityType { get; set; } = string.Empty;
        
        /// <summary>
        /// 🔥 User who created the entity (Maker)
        /// </summary>
        [Required]
        public string CreatedBy { get; set; } = string.Empty;
        
        /// <summary>
        /// 🔥 User who approved the entity (Checker)
        /// </summary>
        public string ApprovedBy { get; set; } = string.Empty;
        
        /// <summary>
        /// 🔥 Approval status
        /// </summary>
        [Required]
        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
        
        /// <summary>
        /// 🔥 Approval workflow type
        /// </summary>
        [Required]
        public ApprovalWorkflow Workflow { get; set; } = ApprovalWorkflow.Standard;
        
        /// <summary>
        /// 🔥 Comments from approver
        /// </summary>
        [MaxLength(1000)]
        public string ApprovalComments { get; set; } = string.Empty;
        
        /// <summary>
        /// 🔥 Rejection reason
        /// </summary>
        [MaxLength(1000)]
        public string RejectionReason { get; set; } = string.Empty;
        
        /// <summary>
        /// 🔥 When approval was requested
        /// </summary>
        [Required]
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// 🔥 When approval was completed
        /// </summary>
        public DateTime? ApprovedAt { get; set; }
        
        /// <summary>
        /// 🔥 Approval deadline
        /// </summary>
        public DateTime? DeadlineAt { get; set; }
        
        /// <summary>
        /// 🔥 Priority level
        /// </summary>
        [Required]
        public ApprovalPriority Priority { get; set; } = ApprovalPriority.Normal;
        
        /// <summary>
        /// 🔥 Amount threshold for approval
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? AmountThreshold { get; set; }
        
        /// <summary>
        /// 🔥 Actual amount being approved
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? ActualAmount { get; set; }
        
        /// <summary>
        /// 🔥 Department/Division requiring approval
        /// </summary>
        [MaxLength(100)]
        public string Department { get; set; } = string.Empty;
        
        /// <summary>
        /// 🔥 Approval level (1, 2, 3 for multi-level approvals)
        /// </summary>
        public int ApprovalLevel { get; set; } = 1;
        
        /// <summary>
        /// 🔥 Maximum approval level required
        /// </summary>
        public int MaxApprovalLevel { get; set; } = 1;
        
        /// <summary>
        /// 🔥 Whether this is a batch approval
        /// </summary>
        public bool IsBatchApproval { get; set; }
        
        /// <summary>
        /// 🔥 Parent approval for batch operations
        /// </summary>
        public Guid? ParentApprovalId { get; set; }
        
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        [ForeignKey("CompanyId")]
        public virtual Company Company { get; set; }
        
        public virtual Approval ParentApproval { get; set; }
        public virtual ICollection<Approval> ChildApprovals { get; set; } = new List<Approval>();
    }
    
    /// <summary>
    /// Approval status enumeration
    /// </summary>
    public enum ApprovalStatus
    {
        Pending = 1,        // Waiting for approval
        Approved = 2,       // Approved and ready to post
        Rejected = 3,        // Rejected, needs correction
        Cancelled = 4,       // Cancelled by creator
        Expired = 5,         // Approval deadline passed
        Posted = 6           // Successfully posted to ledger
    }
    
    /// <summary>
    /// Approval workflow types
    /// </summary>
    public enum ApprovalWorkflow
    {
        Standard = 1,        // Standard maker-checker
        Emergency = 2,       // Emergency approval (single user)
        HighValue = 3,       // High-value transaction (multiple levels)
        CrossDepartment = 4, // Requires cross-department approval
        Regulatory = 5       // Regulatory compliance approval
    }
    
    /// <summary>
    /// Approval priority levels
    /// </summary>
    public enum ApprovalPriority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Critical = 4,
        Emergency = 5
    }
    
    /// <summary>
    /// Approval request DTO
    /// </summary>
    public class ApprovalRequest
    {
        public Guid EntityId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public ApprovalWorkflow Workflow { get; set; }
        public ApprovalPriority Priority { get; set; }
        public decimal? AmountThreshold { get; set; }
        public decimal? ActualAmount { get; set; }
        public string Department { get; set; } = string.Empty;
        public string Comments { get; set; } = string.Empty;
        public DateTime? DeadlineAt { get; set; }
    }
    
    /// <summary>
    /// Approval response DTO
    /// </summary>
    public class ApprovalResponse
    {
        public Guid Id { get; set; }
        public ApprovalStatus Status { get; set; }
        public string ApprovedBy { get; set; } = string.Empty;
        public string ApprovalComments { get; set; } = string.Empty;
        public string RejectionReason { get; set; } = string.Empty;
        public DateTime ApprovedAt { get; set; }
        public bool CanPost { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Approval statistics
    /// </summary>
    public class ApprovalStatistics
    {
        public int TotalApprovals { get; set; }
        public int PendingApprovals { get; set; }
        public int ApprovedToday { get; set; }
        public int RejectedToday { get; set; }
        public int OverdueApprovals { get; set; }
        public decimal AverageApprovalTimeHours { get; set; }
        public Dictionary<ApprovalStatus, int> StatusBreakdown { get; set; } = new();
        public Dictionary<string, int> DepartmentBreakdown { get; set; } = new();
    }
}
