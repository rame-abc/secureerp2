using System;
using System.Collections.Generic;

namespace SecureERP2.Modules.Finance.Entities
{
    /// <summary>
    /// Represents the state of a ledger at a point in time
    /// </summary>
    public class LedgerState
    {
        public int CompanyId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<int, decimal> AccountBalances { get; set; } = new();
        public int TransactionCount { get; set; }
        public long Version { get; set; }
        public List<Guid> DraftEntries { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public long LastEventVersion { get; set; }
        public decimal TotalBalance { get; set; }
        public List<object> ClosedPeriods { get; set; } = new();
        public int TotalPostedEntries { get; set; }
        public decimal TotalDebits => AccountBalances.Where(x => x.Value > 0).Sum(x => x.Value);
        public decimal TotalCredits => AccountBalances.Where(x => x.Value < 0).Sum(x => Math.Abs(x.Value));
        
        // Additional properties for compatibility
        public List<Guid> PostedEntries { get; set; } = new();
        public List<Guid> VoidedEntries { get; set; } = new();
                public List<TrialBalanceAccount> TrialBalance => AccountBalances.Select(x => new TrialBalanceAccount 
        { 
            Id = x.Key, 
            AccountCode = x.Key.ToString(), 
            AccountName = $"Account {x.Key}", 
            Debit = x > 0 ? x : 0, 
            Credit = x < 0 ? -x : 0 
        }).ToList();
        public List<PeriodStatistic> PeriodStatistics { get; set; } = new();
        public List<AccountBalanceDetail> AccountBalancesDetail { get; set; } = new();
        public int TotalAccounts { get; set; }
        public int ActiveAccounts { get; set; }
        public bool Balances { get; set; }
        public bool HasErrors { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CapturedAt { get; set; }
        public DateTime AsOfDate { get; set; }
        public string CaptureType { get; set; } = string.Empty;
        public DateTime CaptureCompletedAt { get; set; }
        public double CaptureDurationMs { get; set; }
    }
}
