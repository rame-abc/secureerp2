using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureERP2.Modules.Finance.Entities
{
    /// <summary>
    /// 🌍 LAYER 2: Financial Time Engine
    /// Stop using system time. Introduce FinancialTime with logical sequencing.
    /// Ordering = sequence, NOT timestamp. Time = metadata only.
    /// </summary>
    public class FinancialTime
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public int CompanyId { get; set; }
        
        /// <summary>
        /// 🔥 Logical Sequence (BIGINT) - PRIMARY ORDERING
        /// This is the authoritative ordering mechanism
        /// </summary>
        [Required]
        [Column(TypeName = "bigint")]
        public long LogicalSequence { get; set; }
        
        /// <summary>
        /// 🔥 Event Time UTC - METADATA ONLY
        /// Used for "as-of" queries, reporting, human display
        /// </summary>
        [Required]
        public DateTime EventTimeUTC { get; set; }
        
        /// <summary>
        /// 🔥 Time Source ("SYSTEM" | "NTP" | "ATOMIC")
        /// Tracks the reliability of the time source
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Source { get; set; } = "SYSTEM";
        
        /// <summary>
        /// 🔥 Event Type (Journal, Transaction, etc.)
        /// For filtering and categorization
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty;
        
        /// <summary>
        /// 🔥 Event ID (references the actual event)
        /// Links to the source entity
        /// </summary>
        [Required]
        public Guid EventId { get; set; }
        
        /// <summary>
        /// 🔥 Region where event was generated
        /// For multi-region tracking
        /// </summary>
        [MaxLength(50)]
        public string Region { get; set; } = string.Empty;
        
        /// <summary>
        /// 🔥 Node/Server that generated the event
        /// For debugging and traceability
        /// </summary>
        [MaxLength(100)]
        public string NodeId { get; set; } = string.Empty;
        
        /// <summary>
        /// 🔥 Clock drift in milliseconds (if NTP/Atomic)
        /// Measures time source accuracy
        /// </summary>
        public int ClockDriftMs { get; set; }
        
        /// <summary>
        /// 🔥 Previous logical sequence (for chain verification)
        /// Ensures no gaps in sequence
        /// </summary>
        public long PreviousLogicalSequence { get; set; }
        
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        [ForeignKey("CompanyId")]
        public virtual Company Company { get; set; }
    }
    
    /// <summary>
    /// Time source types with reliability levels
    /// </summary>
    public enum TimeSource
    {
        SYSTEM = 1,      // System clock (least reliable)
        NTP = 2,        // Network Time Protocol (moderately reliable)
        ATOMIC = 3,     // Atomic clock (most reliable)
        GPS = 4         // GPS time (highly reliable)
    }
    
    /// <summary>
    /// Financial time query result for "as-of" queries
    /// </summary>
    public class AsOfTimeQuery
    {
        public DateTime AsOfTime { get; set; }
        public long LogicalSequence { get; set; }
        public bool IsExact { get; set; }
        public DateTime ActualEventTime { get; set; }
        public string EventType { get; set; } = string.Empty;
        public Guid EventId { get; set; }
    }
    
    /// <summary>
    /// Multi-region consistency status
    /// </summary>
    public class MultiRegionStatus
    {
        public string Region { get; set; } = string.Empty;
        public long LastLogicalSequence { get; set; }
        public DateTime LastEventTime { get; set; }
        public bool IsHealthy { get; set; }
        public long LagMs { get; set; }
        public int PendingEvents { get; set; }
    }
    
    /// <summary>
    /// Time sync configuration
    /// </summary>
    public class TimeSyncConfiguration
    {
        public TimeSource PrimarySource { get; set; } = TimeSource.NTP;
        public TimeSource FallbackSource { get; set; } = TimeSource.SYSTEM;
        public int SyncIntervalSeconds { get; set; } = 60;
        public int MaxDriftMs { get; set; } = 1000;
        public string[] NtpServers { get; set; } = { "pool.ntp.org", "time.google.com" };
        public bool EnableAtomicClock { get; set; } = false;
        public string AtomicClockEndpoint { get; set; } = string.Empty;
    }
}
