#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureERP2.Modules.Finance.Entities;

namespace SecureERP2.Modules.Finance.Services
{
    /// <summary>
    /// 🔬 PHASE 1.3: Real-time Imbalance Detector
    /// Continuous monitoring for mathematical correctness as transactions occur
    /// </summary>
    public class RealTimeImbalanceDetector
    {
        private readonly ERPDbContext _context;
        private readonly AccountingEngine _accountingEngine;

        public RealTimeImbalanceDetector(ERPDbContext context, AccountingEngine accountingEngine)
        {
            _context = context;
            _accountingEngine = accountingEngine;
        }

        /// <summary>
        /// 🔬 Real-time balance check for a single transaction
        /// </summary>
        public async Task<RealTimeBalanceCheckResult> CheckTransactionBalanceAsync(Transaction journalEntry)
        {
            var result = new RealTimeBalanceCheckResult
            {
                JournalEntryId = journalEntry.Id,
                CompanyId = journalEntry.CompanyId,
                CheckedAt = DateTime.UtcNow
            };

            try
            {
                // 🔬 Verify double-entry balance using LedgerEntries
                var ledgerEntries = await _context.LedgerEntries
                    .Where(le => le.TransactionId == journalEntry.Id)
                    .ToListAsync();
                
                var totalDebits = ledgerEntries.Sum(l => l.DebitAmount);
                var totalCredits = ledgerEntries.Sum(l => l.CreditAmount);
                var balanceDifference = Math.Abs(totalDebits - totalCredits);

                result.TotalDebits = totalDebits;
                result.TotalCredits = totalCredits;
                result.BalanceDifference = balanceDifference;
                result.IsBalanced = balanceDifference <= 0.01m;

                // 🔬 Check account type validity
                result.AccountTypeViolations = await ValidateAccountTypesAsync(journalEntry);

                // 🔬 Check for potential fraud patterns
                result.FraudRiskIndicators = await DetectFraudRiskPatternsAsync(journalEntry);

                // 🔬 Check business logic validity
                result.BusinessLogicViolations = await ValidateBusinessLogicAsync(journalEntry);

                // 🔬 Calculate overall risk score
                result.OverallRiskScore = CalculateRiskScore(result);
                result.RiskLevel = DetermineRiskLevel(result.OverallRiskScore);

                // 🔬 Determine if transaction should be blocked
                result.ShouldBlock = result.RiskLevel >= RiskLevel.High || !result.IsBalanced;

                result.Status = result.IsBalanced && result.RiskLevel < RiskLevel.High ?
                    BalanceCheckStatus.Passed : BalanceCheckStatus.Failed;
            }
            catch (Exception ex)
            {
                result.Status = BalanceCheckStatus.Error;
                result.ErrorMessage = $"Real-time balance check failed: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔬 Continuous system balance monitoring
        /// </summary>
        public async Task<SystemBalanceMonitorResult> MonitorSystemBalanceAsync(int companyId)
        {
            var result = new SystemBalanceMonitorResult
            {
                CompanyId = companyId,
                MonitoredAt = DateTime.UtcNow
            };

            try
            {
                // 🔬 Get current trial balance
                var trialBalance = await _accountingEngine.GetTrialBalanceAsync(companyId, DateTime.MinValue, DateTime.UtcNow);

                // 🔬 Check fundamental accounting equation
                var assets = trialBalance.Accounts
                    .Where(a => a.AccountType == AccountType.Asset)
                    .Sum(a => a.Balance);

                var liabilities = trialBalance.Accounts
                    .Where(a => a.AccountType == AccountType.Liability)
                    .Sum(a => a.Balance);

                var equity = trialBalance.Accounts
                    .Where(a => a.AccountType == AccountType.Equity)
                    .Sum(a => a.Balance);

                result.TotalAssets = assets;
                result.TotalLiabilities = liabilities;
                result.TotalEquity = equity;
                result.BalanceEquationDifference = Math.Abs(assets - (liabilities + equity));
                result.IsSystemBalanced = result.BalanceEquationDifference <= 0.01m;

                // 🔬 Check for account balance anomalies
                result.BalanceAnomalies = await DetectBalanceAnomaliesAsync(trialBalance);

                // 🔬 Check for unusual transaction patterns
                result.UnusualPatterns = await DetectUnusualTransactionPatternsAsync(companyId);

                // 🔬 Check for period integrity issues
                result.PeriodIntegrityIssues = await CheckPeriodIntegrityAsync(companyId);

                // 🔬 Calculate system health score
                result.SystemHealthScore = CalculateSystemHealthScore(result);
                result.SystemHealthLevel = DetermineSystemHealthLevel(result.SystemHealthScore);

                result.Status = result.IsSystemBalanced && result.SystemHealthLevel >= SystemHealthLevel.Good ?
                    MonitorStatus.Healthy : MonitorStatus.Warning;
            }
            catch (Exception ex)
            {
                result.Status = MonitorStatus.Error;
                result.ErrorMessage = $"System balance monitoring failed: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔬 Validate account types for a transaction
        /// </summary>
        private async Task<List<AccountTypeViolation>> ValidateAccountTypesAsync(Transaction journalEntry)
        {
            var violations = new List<AccountTypeViolation>();

            // 🔬 Get ledger entries for this transaction
            var ledgerEntries = await _context.LedgerEntries
                .Where(le => le.TransactionId == journalEntry.Id)
                .ToListAsync();
            
            foreach (var line in ledgerEntries)
            {
                var account = await _context.FinanceAccounts
                    .FirstOrDefaultAsync(a => a.Id == line.AccountId);

                if (account == null)
                {
                    violations.Add(new AccountTypeViolation
                    {
                        AccountId = line.AccountId,
                        Description = "Account not found",
                        Severity = ViolationSeverity.Critical
                    });
                    continue;
                }

                // 🔬 Check for unusual account type combinations
                var otherLines = ledgerEntries
                    .Where(l => l.AccountId != line.AccountId)
                    .ToList();

                foreach (var otherLine in otherLines)
                {
                    var otherAccount = await _context.FinanceAccounts
                        .FirstOrDefaultAsync(a => a.Id == otherLine.AccountId);

                    if (otherAccount != null)
                    {
                        var violation = ValidateAccountTypeCombination(account, otherAccount, line, otherLine);
                        if (violation != null)
                        {
                            violations.Add(violation);
                        }
                    }
                }
            }

            return violations;
        }

        /// <summary>
        /// 🔬 Detect fraud risk patterns in a transaction
        /// </summary>
        private async Task<List<FraudRiskIndicator>> DetectFraudRiskPatternsAsync(Transaction journalEntry)
        {
            var indicators = new List<FraudRiskIndicator>();

            // 🔬 Get ledger entries for this transaction
            var ledgerEntries = await _context.LedgerEntries
                .Where(le => le.TransactionId == journalEntry.Id)
                .ToListAsync();
            
            // 🔬 Check for round numbers (potential manual adjustments)
            foreach (var line in ledgerEntries)
            {
                if (IsRoundNumber(line.DebitAmount) || IsRoundNumber(line.CreditAmount))
                {
                    indicators.Add(new FraudRiskIndicator
                    {
                        Type = "RoundNumberAmount",
                        Description = $"Round number amount detected: {Math.Max(line.DebitAmount, line.CreditAmount):C}",
                        AccountId = line.AccountId,
                        Amount = Math.Max(line.DebitAmount, line.CreditAmount),
                        RiskScore = 20
                    });
                }
            }

            // 🔬 Check for transactions outside business hours
            var entryHour = journalEntry.CreatedAt.Hour;
            if (entryHour < 6 || entryHour > 22)
            {
                indicators.Add(new FraudRiskIndicator
                {
                    Type = "OutsideBusinessHours",
                    Description = $"Transaction created at {entryHour:D2}:00",
                    RiskScore = 15
                });
            }

            // 🔬 Check for unusual transaction amounts
            var totalAmount = ledgerEntries.Sum(l => l.DebitAmount + l.CreditAmount);
            var averageAmount = await GetAverageTransactionAmountAsync(journalEntry.CompanyId);
            
            if (totalAmount > averageAmount * 10) // More than 10x average
            {
                indicators.Add(new FraudRiskIndicator
                {
                    Type = "UnusualAmount",
                    Description = $"Transaction amount {totalAmount:C} is {totalAmount / averageAmount:F1}x average",
                    Amount = totalAmount,
                    RiskScore = 25
                });
            }

            // 🔬 Check for weekend transactions
            if (journalEntry.CreatedAt.DayOfWeek == DayOfWeek.Saturday ||
                journalEntry.CreatedAt.DayOfWeek == DayOfWeek.Sunday)
            {
                indicators.Add(new FraudRiskIndicator
                {
                    Type = "WeekendTransaction",
                    Description = "Transaction created on weekend",
                    RiskScore = 10
                });
            }

            // 🔬 Check for rapid successive transactions
            var recentTransactions = await _context.Transactions
                .Where(j => j.CompanyId == journalEntry.CompanyId &&
                           j.CreatedAt >= journalEntry.CreatedAt.AddMinutes(-5) &&
                           j.Id != journalEntry.Id)
                .CountAsync();

            if (recentTransactions > 5)
            {
                indicators.Add(new FraudRiskIndicator
                {
                    Type = "RapidSuccession",
                    Description = $"{recentTransactions} transactions in last 5 minutes",
                    RiskScore = 15
                });
            }

            return indicators;
        }

        /// <summary>
        /// 🔬 Validate business logic for a transaction
        /// </summary>
        private async Task<List<BusinessLogicViolation>> ValidateBusinessLogicAsync(Transaction journalEntry)
        {
            var violations = new List<BusinessLogicViolation>();

            // 🔬 Check for duplicate descriptions
            var duplicateDescriptions = await _context.Transactions
                .Where(j => j.CompanyId == journalEntry.CompanyId &&
                           j.Description == journalEntry.Description &&
                           j.CreatedAt >= DateTime.UtcNow.AddDays(-7) &&
                           j.Id != journalEntry.Id)
                .CountAsync();

            if (duplicateDescriptions > 3)
            {
                violations.Add(new BusinessLogicViolation
                {
                    Type = "DuplicateDescription",
                    Description = $"Same description used {duplicateDescriptions} times in last week",
                    Severity = ViolationSeverity.Medium
                });
            }

            // 🔬 Check for future-dated transactions
            if (journalEntry.TransactionDate > DateTime.UtcNow.AddDays(1))
            {
                violations.Add(new BusinessLogicViolation
                {
                    Type = "FutureDatedTransaction",
                    Description = $"Transaction dated in future: {journalEntry.TransactionDate:yyyy-MM-dd}",
                    Severity = ViolationSeverity.High
                });
            }

            // 🔬 Check for backdated transactions beyond reasonable period
            var backdateDays = (DateTime.UtcNow - journalEntry.TransactionDate).Days;
            if (backdateDays > 90)
            {
                violations.Add(new BusinessLogicViolation
                {
                    Type = "ExcessiveBackdating",
                    Description = $"Transaction backdated by {backdateDays} days",
                    Severity = ViolationSeverity.High
                });
            }

            // 🔬 Check for transactions without proper references
            if (string.IsNullOrWhiteSpace(journalEntry.Description) || journalEntry.Description.Length < 5)
            {
                violations.Add(new BusinessLogicViolation
                {
                    Type = "InsufficientDescription",
                    Description = "Transaction has insufficient description",
                    Severity = ViolationSeverity.Medium
                });
            }

            return violations;
        }

        /// <summary>
        /// 🔬 Detect balance anomalies in trial balance
        /// </summary>
        private async Task<List<BalanceAnomaly>> DetectBalanceAnomaliesAsync(object trialBalance)
        {
            var anomalies = new List<BalanceAnomaly>();

            // 🔬 Simplified balance anomaly detection
            // TODO: Implement proper trial balance analysis when TrialBalance entity is available
            return anomalies;
        }

        /// <summary>
        /// 🔬 Detect unusual transaction patterns
        /// </summary>
        private async Task<List<UnusualPattern>> DetectUnusualTransactionPatternsAsync(int companyId)
        {
            var patterns = new List<UnusualPattern>();

            // 🔬 Check for transaction volume spikes
            var today = DateTime.UtcNow.Date;
            var todayCount = await _context.Transactions
                .Where(j => j.CompanyId == companyId && j.CreatedAt.Date == today)
                .CountAsync();

            var averageDailyCount = await GetAverageDailyTransactionCountAsync(companyId);
            
            if (todayCount > averageDailyCount * 2)
            {
                patterns.Add(new UnusualPattern
                {
                    Type = "TransactionVolumeSpike",
                    Description = $"Today's transaction count ({todayCount}) is {todayCount / averageDailyCount:F1}x average",
                    Severity = PatternSeverity.Medium
                });
            }

            // 🔬 Check for unusual transaction amounts
            var recentAmounts = await _context.Transactions
                .Where(j => j.CompanyId == companyId && j.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                .SelectMany(j => _context.LedgerEntries.Where(le => le.TransactionId == j.Id))
                .Select(l => l.DebitAmount + l.CreditAmount)
                .ToListAsync();

            if (recentAmounts.Any())
            {
                var averageAmount = recentAmounts.Average();
                var maxAmount = recentAmounts.Max();

                if (maxAmount > averageAmount * 50)
                {
                    patterns.Add(new UnusualPattern
                    {
                        Type = "LargeTransactionAmount",
                        Description = $"Maximum transaction amount ({maxAmount:C}) is {maxAmount / averageAmount:F1}x average",
                        Severity = PatternSeverity.High
                    });
                }
            }

            // 🔬 Check for unusual user patterns
            var userTransactions = await _context.Transactions
                .Where(j => j.CompanyId == companyId && j.CreatedAt >= DateTime.UtcNow.AddDays(-1))
                .GroupBy(j => j.CreatedByUserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToListAsync();

            var userAverage = userTransactions.Average(ut => ut.Count);
            var suspiciousUsers = userTransactions.Where(ut => ut.Count > userAverage * 5).ToList();

            foreach (var user in suspiciousUsers)
            {
                patterns.Add(new UnusualPattern
                {
                    Type = "UnusualUserActivity",
                    Description = $"User {user.UserId} created {user.Count} transactions (average: {userAverage:F1})",
                    Severity = PatternSeverity.Medium
                });
            }

            return patterns;
        }

        /// <summary>
        /// 🔬 Check period integrity issues
        /// </summary>
        private async Task<List<PeriodIntegrityIssue>> CheckPeriodIntegrityAsync(int companyId)
        {
            var issues = new List<PeriodIntegrityIssue>();

            // 🔬 Check for transactions in locked periods
            var lockedPeriods = await _context.PeriodClosings
                .Where(pc => pc.CompanyId == companyId && pc.IsLocked)
                .ToListAsync();

            foreach (var period in lockedPeriods)
            {
                var entriesAfterLock = await _context.Transactions
                    .Where(j => j.CompanyId == companyId &&
                               j.TransactionDate <= period.ClosingDate &&
                               j.CreatedAt > period.ClosedAt)
                    .CountAsync();

                if (entriesAfterLock > 0)
                {
                    issues.Add(new PeriodIntegrityIssue
                    {
                        Type = "LockedPeriodModification",
                        Description = $"{entriesAfterLock} entries created after period lock",
                        PeriodClosingDate = period.ClosingDate,
                        Severity = IntegrityIssueSeverity.Critical
                    });
                }
            }

            // 🔬 Check for unclosed periods
            var lastClosingDate = lockedPeriods.OrderByDescending(pc => pc.ClosingDate).FirstOrDefault()?.ClosingDate;
            if (lastClosingDate.HasValue)
            {
                var monthsSinceLastClosing = (DateTime.UtcNow.Year - lastClosingDate.Value.Year) * 12 +
                                             (DateTime.UtcNow.Month - lastClosingDate.Value.Month);

                if (monthsSinceLastClosing > 3)
                {
                    issues.Add(new PeriodIntegrityIssue
                    {
                        Type = "UnclosedPeriods",
                        Description = $"{monthsSinceLastClosing} months since last period closing",
                        Severity = IntegrityIssueSeverity.High
                    });
                }
            }

            return issues;
        }

        // Helper methods
        private AccountTypeViolation ValidateAccountTypeCombination(FinanceAccount account1, FinanceAccount account2, LedgerEntry line1, LedgerEntry line2)
        {
            // 🔬 Check for unusual account type combinations
            if (account1.AccountType == AccountType.Asset && account2.AccountType == AccountType.Asset)
            {
                // Asset to asset transactions are unusual
                if (line1.DebitAmount > 0 && line2.CreditAmount > 0)
                {
                    return new AccountTypeViolation
                    {
                        AccountId = line1.AccountId,
                        Description = "Asset-to-asset transaction (unusual)",
                        Severity = ViolationSeverity.Low
                    };
                }
            }

            if (account1.AccountType == AccountType.Revenue && account2.AccountType == AccountType.Revenue)
            {
                // Revenue to revenue transactions are very unusual
                return new AccountTypeViolation
                {
                    AccountId = line1.AccountId,
                    Description = "Revenue-to-revenue transaction (very unusual)",
                    Severity = ViolationSeverity.Medium
                };
            }

            return null;
        }

        private bool IsRoundNumber(decimal amount)
        {
            if (amount == 0) return false;
            
            // Check if amount is divisible by 1000, 100, 10, or 5
            return amount % 1000 == 0 || amount % 100 == 0 || amount % 10 == 0 || amount % 5 == 0;
        }

        private async Task<decimal> GetAverageTransactionAmountAsync(int companyId)
        {
            var amounts = await _context.Transactions
                .Where(j => j.CompanyId == companyId && j.CreatedAt >= DateTime.UtcNow.AddDays(-30))
                .SelectMany(j => _context.LedgerEntries.Where(le => le.TransactionId == j.Id))
                .Select(l => l.DebitAmount + l.CreditAmount)
                .ToListAsync();

            return amounts.Any() ? amounts.Average() : 0;
        }

        private async Task<decimal> GetAverageAccountBalanceAsync(int accountId)
        {
            var balances = await _context.LedgerEntries
                .Where(jl => jl.AccountId == accountId && jl.Transaction.CreatedAt >= DateTime.UtcNow.AddDays(-30))
                .Select(jl => jl.DebitAmount - jl.CreditAmount)
                .ToListAsync();

            return balances.Any() ? Math.Abs(balances.Average()) : 0;
        }

        private async Task<double> GetAverageDailyTransactionCountAsync(int companyId)
        {
            var dailyCounts = await _context.Transactions
                .Where(j => j.CompanyId == companyId && j.CreatedAt >= DateTime.UtcNow.AddDays(-30))
                .GroupBy(j => j.CreatedAt.Date)
                .Select(g => g.Count())
                .ToListAsync();

            return dailyCounts.Any() ? dailyCounts.Average() : 1;
        }

        private int CalculateRiskScore(RealTimeBalanceCheckResult result)
        {
            var score = 0;

            if (!result.IsBalanced) score += 50;
            score += result.AccountTypeViolations.Sum(v => v.Severity == ViolationSeverity.Critical ? 20 : 
                                                               v.Severity == ViolationSeverity.High ? 15 : 
                                                               v.Severity == ViolationSeverity.Medium ? 10 : 5);
            score += result.FraudRiskIndicators.Sum(i => i.RiskScore);
            score += result.BusinessLogicViolations.Sum(v => v.Severity == ViolationSeverity.Critical ? 15 : 
                                                             v.Severity == ViolationSeverity.High ? 10 : 
                                                             v.Severity == ViolationSeverity.Medium ? 5 : 2);

            return Math.Min(100, score);
        }

        private RiskLevel DetermineRiskLevel(int score)
        {
            if (score >= 80) return RiskLevel.Critical;
            if (score >= 60) return RiskLevel.High;
            if (score >= 40) return RiskLevel.Medium;
            if (score >= 20) return RiskLevel.Low;
            return RiskLevel.Minimal;
        }

        private int CalculateSystemHealthScore(SystemBalanceMonitorResult result)
        {
            var score = 100;

            if (!result.IsSystemBalanced) score -= 30;
            score -= result.BalanceAnomalies.Sum(a => a.Severity == AnomalySeverity.Critical ? 15 : 
                                                   a.Severity == AnomalySeverity.High ? 10 : 
                                                   a.Severity == AnomalySeverity.Medium ? 5 : 2);
            score -= result.UnusualPatterns.Sum(p => p.Severity == PatternSeverity.Critical ? 10 : 
                                                   p.Severity == PatternSeverity.High ? 7 : 
                                                   p.Severity == PatternSeverity.Medium ? 4 : 2);
            score -= result.PeriodIntegrityIssues.Sum(i => i.Severity == IntegrityIssueSeverity.Critical ? 20 : 
                                                          i.Severity == IntegrityIssueSeverity.High ? 15 : 
                                                          i.Severity == IntegrityIssueSeverity.Medium ? 10 : 5);

            return Math.Max(0, score);
        }

        private SystemHealthLevel DetermineSystemHealthLevel(int score)
        {
            if (score >= 90) return SystemHealthLevel.Excellent;
            if (score >= 80) return SystemHealthLevel.Good;
            if (score >= 70) return SystemHealthLevel.Fair;
            if (score >= 60) return SystemHealthLevel.Poor;
            return SystemHealthLevel.Critical;
        }

        // Supporting classes
        public class RealTimeBalanceCheckResult
        {
            public int JournalEntryId { get; set; }
            public int CompanyId { get; set; }
            public DateTime CheckedAt { get; set; }
            public BalanceCheckStatus Status { get; set; }
            public string? ErrorMessage { get; set; }

            public decimal TotalDebits { get; set; }
            public decimal TotalCredits { get; set; }
            public decimal BalanceDifference { get; set; }
            public bool IsBalanced { get; set; }

            public List<AccountTypeViolation> AccountTypeViolations { get; set; } = new();
            public List<FraudRiskIndicator> FraudRiskIndicators { get; set; } = new();
            public List<BusinessLogicViolation> BusinessLogicViolations { get; set; } = new();

            public int OverallRiskScore { get; set; }
            public RiskLevel RiskLevel { get; set; }
            public bool ShouldBlock { get; set; }
        }

        public class SystemBalanceMonitorResult
        {
            public int CompanyId { get; set; }
            public DateTime MonitoredAt { get; set; }
            public MonitorStatus Status { get; set; }
            public string? ErrorMessage { get; set; }

            public decimal TotalAssets { get; set; }
            public decimal TotalLiabilities { get; set; }
            public decimal TotalEquity { get; set; }
            public decimal BalanceEquationDifference { get; set; }
            public bool IsSystemBalanced { get; set; }

            public List<BalanceAnomaly> BalanceAnomalies { get; set; } = new();
            public List<UnusualPattern> UnusualPatterns { get; set; } = new();
            public List<PeriodIntegrityIssue> PeriodIntegrityIssues { get; set; } = new();

            public int SystemHealthScore { get; set; }
            public SystemHealthLevel SystemHealthLevel { get; set; }
        }

        public class AccountTypeViolation
        {
            public int AccountId { get; set; }
            public string Description { get; set; } = string.Empty;
            public ViolationSeverity Severity { get; set; }
        }

        public class FraudRiskIndicator
        {
            public string Type { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int? AccountId { get; set; }
            public decimal? Amount { get; set; }
            public int RiskScore { get; set; }
        }

        public class BusinessLogicViolation
        {
            public string Type { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public ViolationSeverity Severity { get; set; }
        }

        public class BalanceAnomaly
        {
            public string Type { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int AccountId { get; set; }
            public string AccountName { get; set; } = string.Empty;
            public decimal Balance { get; set; }
            public AnomalySeverity Severity { get; set; }
        }

        public class UnusualPattern
        {
            public string Type { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public PatternSeverity Severity { get; set; }
        }

        public class PeriodIntegrityIssue
        {
            public string Type { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public DateTime? PeriodClosingDate { get; set; }
            public IntegrityIssueSeverity Severity { get; set; }
        }

        // Enums
        public enum BalanceCheckStatus
        {
            Passed,
            Failed,
            Error
        }

        public enum MonitorStatus
        {
            Healthy,
            Warning,
            Error
        }

        public enum RiskLevel
        {
            Minimal,
            Low,
            Medium,
            High,
            Critical
        }

        public enum SystemHealthLevel
        {
            Excellent,
            Good,
            Fair,
            Poor,
            Critical
        }

        public enum ViolationSeverity
        {
            Low,
            Medium,
            High,
            Critical
        }

        public enum AnomalySeverity
        {
            Low,
            Medium,
            High,
            Critical
        }

        public enum PatternSeverity
        {
            Low,
            Medium,
            High,
            Critical
        }

        public enum IntegrityIssueSeverity
        {
            Low,
            Medium,
            High,
            Critical
        }
    }
}
