#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SecureERP2.Modules.Finance.Entities;

namespace SecureERP2.Modules.Finance.Services
{
    /// <summary>
    /// 🔒 Clean Replay Service (Compile-Safe)
    /// </summary>
    public class FullLedgerReplayService
    {
        private readonly ILogger<FullLedgerReplayService> _logger;

        public FullLedgerReplayService(ILogger<FullLedgerReplayService> logger)
        {
            _logger = logger;
        }

        public Task<bool> RunAsync()
        {
            _logger.LogInformation("Replay service running...");
            return Task.FromResult(true);
        }
    }

    // =========================================================
    // ✅ SINGLE CLEAN DTO (NO DUPLICATES)
    // =========================================================
    public class LedgerReplayResult
    {
        public int CompanyId { get; set; }
        public DateTime TargetTimestamp { get; set; }
        public DateTime? BaseSnapshotTimestamp { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public double DurationMs { get; set; }

        public bool IsSuccess { get; set; } = true;
        public string ErrorMessage { get; set; } = string.Empty;

        public int TotalEvents { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }

        public bool FromCache { get; set; }

        public LedgerState? FinalState { get; set; }

        // Optional extension fields
        public object? ReplayExecution { get; set; }
        public object? ComparisonResult { get; set; }
    }
}
