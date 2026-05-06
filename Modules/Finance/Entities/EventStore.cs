using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SecureERP2.Modules.Finance.Entities
{
    /// <summary>
    /// 🏗️ STEP 6.1: Event Store (NEW CORE)
    /// Ultimate source of truth for distributed financial system
    /// </summary>
    public class EventStore
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public int CompanyId { get; set; }
        
        [ForeignKey("CompanyId")]
        public Company Company { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string AggregateId { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string AggregateType { get; set; } = string.Empty;
        
        [Required]
        public string Payload { get; set; } = string.Empty;
        
        [Required]
        public long Version { get; set; }
        
        [Required]
        public DateTime CreatedAt { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;
        
        // Event metadata
        [MaxLength(500)]
        public string CorrelationId { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string CausationId { get; set; } = string.Empty;
        
        // Event processing metadata
        public DateTime? ProcessedAt { get; set; }
        public bool IsProcessed { get; set; }
        public int RetryCount { get; set; }
        public string ProcessingError { get; set; } = string.Empty;
        
        // Optimistic concurrency
        [Timestamp]
        public byte[] RowVersion { get; set; }
        
        // Partitioning key for performance
        public string PartitionKey => $"{CompanyId}_{EventType}_{CreatedAt:yyyyMM}";
    }
    
    /// <summary>
    /// 🏗️ STEP 6.2: Idempotency Keys (CRITICAL)
    /// Prevent duplicate operations in distributed system
    /// </summary>
    public class IdempotencyKey
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(500)]
        public string Key { get; set; } = string.Empty;
        
        [Required]
        public int CompanyId { get; set; }
        
        [ForeignKey("CompanyId")]
        public Company Company { get; set; }
        
        [Required]
        public string Response { get; set; } = string.Empty;
        
        [Required]
        public DateTime CreatedAt { get; set; }
        
        [Required]
        public DateTime ExpiresAt { get; set; }
        
        // Request metadata
        [MaxLength(100)]
        public string Endpoint { get; set; } = string.Empty;
        [MaxLength(100)]
        public string HttpMethod { get; set; } = string.Empty;
        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty;
        
        // Unique constraint to prevent duplicates
        [Index(nameof(Key), nameof(CompanyId), IsUnique = true)]
        public class UniqueIndex { }
    }
    
    /// <summary>
    /// 🏗️ STEP 6.3: Event Snapshot (Performance Optimization)
    /// Pre-computed state at specific points in time
    /// </summary>
    public class EventSnapshot
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public int CompanyId { get; set; }
        
        [ForeignKey("CompanyId")]
        public Company Company { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string AggregateId { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string AggregateType { get; set; } = string.Empty;
        
        [Required]
        public string State { get; set; } = string.Empty;
        
        [Required]
        public long Version { get; set; }
        
        [Required]
        public DateTime SnapshotAt { get; set; }
        
        [Required]
        public DateTime CreatedAt { get; set; }
        
        // Snapshot metadata
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
    
    /// <summary>
    /// 🏗️ STEP 6.4: Event Projection (CQRS Read Models)
    /// Materialized views for fast queries
    /// </summary>
    public class EventProjection
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public int CompanyId { get; set; }
        
        [ForeignKey("CompanyId")]
        public Company Company { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ProjectionName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string AggregateId { get; set; } = string.Empty;
        
        [Required]
        public string Data { get; set; } = string.Empty;
        
        [Required]
        public DateTime LastUpdated { get; set; }
        
        [Required]
        public long LastEventVersion { get; set; }
        
        // Projection metadata
        public bool IsActive { get; set; }
        public DateTime? LastProcessedAt { get; set; }
        public string ProcessingStatus { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// 🏗️ STEP 6.5: Dead Letter Queue (Failure Handling)
    /// Failed events for manual inspection and retry
    /// </summary>
    public class DeadLetterEvent
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public int CompanyId { get; set; }
        
        [ForeignKey("CompanyId")]
        public Company Company { get; set; }
        
        [Required]
        public Guid OriginalEventId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty;
        
        [Required]
        public string Payload { get; set; } = string.Empty;
        
        [Required]
        public DateTime FailedAt { get; set; }
        
        [Required]
        [MaxLength(1000)]
        public string FailureReason { get; set; } = string.Empty;
        
        [Required]
        public int RetryCount { get; set; }
        
        public DateTime? LastRetryAt { get; set; }
        public DateTime? NextRetryAt { get; set; }
        
        // Resolution metadata
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        [MaxLength(1000)]
        public string ResolutionNotes { get; set; } = string.Empty;
        [MaxLength(100)]
        public string ResolvedBy { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// 🏗️ STEP 6.6: Event Subscription (Event-Driven Architecture)
    /// Track which services subscribe to which events
    /// </summary>
    public class EventSubscription
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ServiceName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(500)]
        public string Endpoint { get; set; } = string.Empty;
        
        [Required]
        public DateTime CreatedAt { get; set; }
        
        public bool IsActive { get; set; }
        public DateTime? LastProcessedAt { get; set; }
        public long ProcessedCount { get; set; }
        public long FailedCount { get; set; }
        
        // Subscription configuration
        public string FilterCriteria { get; set; } = string.Empty;
        public int MaxRetries { get; set; }
        public TimeSpan RetryDelay { get; set; }
        public TimeSpan Timeout { get; set; }
    }
    
    /// <summary>
    /// 🏗️ STEP 6.7: Event Metadata (Enhanced Tracking)
    /// Additional metadata for events
    /// </summary>
    public class EventMetadata
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public Guid EventId { get; set; }
        
        [ForeignKey("EventId")]
        public EventStore Event { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Key { get; set; } = string.Empty;
        
        [Required]
        public string Value { get; set; } = string.Empty;
        
        [Required]
        public DateTime CreatedAt { get; set; }
    }
    
    /// <summary>
    /// 🏗️ STEP 6.8: Event Index (Performance Optimization)
    /// Optimized indexes for common queries
    /// </summary>
    public class EventIndex
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public Guid EventId { get; set; }
        
        [ForeignKey("EventId")]
        public EventStore Event { get; set; }
        
        [Required]
        public int CompanyId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string AggregateId { get; set; } = string.Empty;
        
        [Required]
        public DateTime EventDate { get; set; }
        
        // Index-specific fields for fast lookup
        [MaxLength(500)]
        public string SearchText { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        
        // Partitioning
        public string PartitionKey => $"{CompanyId}_{EventType}_{EventDate:yyyyMM}";
    }
}
