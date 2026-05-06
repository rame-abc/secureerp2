using System;

namespace SecureERP2.Modules.Finance.Entities
{
    /// <summary>
    /// Result of a ledger replay operation
    /// </summary>
    public class LedgerReplayResult
    {
        public int CompanyId { get; set; }
        public DateTime TargetTimestamp { get; set; }
        public DateTime? BaseSnapshotTimestamp { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public double DurationMs { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public LedgerState? FinalState { get; set; }
        public int TotalEvents { get; set; }
        public int SuccessCount { get; set; }
        
        // Additional property for compatibility
        public decimal FinalBalance { get; set; }
        public int ErrorCount { get; set; }
        public bool FromCache { get; set; }
        public object replayExecution { get; set; }
        public object ComparisonResult { get; set; }
        public bool IsSuccessful { get; set; }
    }
}
