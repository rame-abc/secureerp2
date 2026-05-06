using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SecureERP2.Modules.Finance.Entities;

namespace SecureERP2.Modules.Finance.Services
{
    /// <summary>
    /// ⚡ PHASE 3.1: Caching/Report Snapshots
    /// Enterprise-grade performance caching for financial reports
    /// </summary>
    public class PerformanceCachingService
    {
        private readonly ERPDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PerformanceCachingService> _logger;

        // Cache settings
        private const string ReportCachePrefix = "report_snapshot:";
        private const string TrialBalancePrefix = "trial_balance:";
        private const string BalanceSheetPrefix = "balance_sheet:";
        private const string IncomeStatementPrefix = "income_statement:";
        private const string DashboardPrefix = "dashboard:";
        
        // Cache durations
        private static readonly TimeSpan TrialBalanceCacheDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan BalanceSheetCacheDuration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan IncomeStatementCacheDuration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan DashboardCacheDuration = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan RealtimeCacheDuration = TimeSpan.FromMinutes(1);

        public PerformanceCachingService(ERPDbContext context, IMemoryCache cache, ILogger<PerformanceCachingService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// ⚡ Get cached trial balance or generate new snapshot
        /// </summary>
        public async Task<TrialBalanceSnapshot> GetTrialBalanceAsync(int companyId, DateTime? asOfDate = null)
        {
            var cacheKey = $"{TrialBalancePrefix}{companyId}_{asOfDate:yyyy-MM-dd}";
            
            if (_cache.TryGetValue(cacheKey, out TrialBalanceSnapshot? cachedSnapshot))
            {
                _logger.LogDebug("Trial balance cache hit for company {CompanyId}", companyId);
                return cachedSnapshot!;
            }

            // Generate new trial balance
            var trialBalance = await GenerateTrialBalanceSnapshotAsync(companyId, asOfDate);
            
            // Cache the result
            _cache.Set(cacheKey, trialBalance, TrialBalanceCacheDuration);
            
            _logger.LogDebug("Trial balance generated and cached for company {CompanyId}", companyId);
            return trialBalance;
        }

        /// <summary>
        /// ⚡ Get cached balance sheet or generate new snapshot
        /// </summary>
        public async Task<BalanceSheetSnapshot> GetBalanceSheetAsync(int companyId, DateTime? asOfDate = null)
        {
            var cacheKey = $"{BalanceSheetPrefix}{companyId}_{asOfDate:yyyy-MM-dd}";
            
            if (_cache.TryGetValue(cacheKey, out BalanceSheetSnapshot? cachedSnapshot))
            {
                _logger.LogDebug("Balance sheet cache hit for company {CompanyId}", companyId);
                return cachedSnapshot!;
            }

            // Generate new balance sheet
            var balanceSheet = await GenerateBalanceSheetSnapshotAsync(companyId, asOfDate);
            
            // Cache the result
            _cache.Set(cacheKey, balanceSheet, BalanceSheetCacheDuration);
            
            _logger.LogDebug("Balance sheet generated and cached for company {CompanyId}", companyId);
            return balanceSheet;
        }

        /// <summary>
        /// ⚡ Get cached income statement or generate new snapshot
        /// </summary>
        public async Task<IncomeStatementSnapshot> GetIncomeStatementAsync(int companyId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var cacheKey = $"{IncomeStatementPrefix}{companyId}_{fromDate:yyyy-MM-dd}_{toDate:yyyy-MM-dd}";
            
            if (_cache.TryGetValue(cacheKey, out IncomeStatementSnapshot? cachedSnapshot))
            {
                _logger.LogDebug("Income statement cache hit for company {CompanyId}", companyId);
                return cachedSnapshot!;
            }

            // Generate new income statement
            var incomeStatement = await GenerateIncomeStatementSnapshotAsync(companyId, fromDate, toDate);
            
            // Cache the result
            _cache.Set(cacheKey, incomeStatement, IncomeStatementCacheDuration);
            
            _logger.LogDebug("Income statement generated and cached for company {CompanyId}", companyId);
            return incomeStatement;
        }

        /// <summary>
        /// ⚡ Get cached dashboard data or generate new snapshot
        /// </summary>
        public async Task<DashboardSnapshot> GetDashboardAsync(int companyId)
        {
            var cacheKey = $"{DashboardPrefix}{companyId}";
            
            if (_cache.TryGetValue(cacheKey, out DashboardSnapshot? cachedSnapshot))
            {
                _logger.LogDebug("Dashboard cache hit for company {CompanyId}", companyId);
                return cachedSnapshot!;
            }

            // Generate new dashboard
            var dashboard = await GenerateDashboardSnapshotAsync(companyId);
            
            // Cache the result
            _cache.Set(cacheKey, dashboard, DashboardCacheDuration);
            
            _logger.LogDebug("Dashboard generated and cached for company {CompanyId}", companyId);
            return dashboard;
        }

        /// <summary>
        /// ⚡ Invalidate all caches for a company
        /// </summary>
        public void InvalidateCompanyCaches(int companyId)
        {
            var keysToRemove = new List<string>
            {
                $"{TrialBalancePrefix}{companyId}",
                $"{BalanceSheetPrefix}{companyId}",
                $"{IncomeStatementPrefix}{companyId}",
                $"{DashboardPrefix}{companyId}"
            };

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }

            _logger.LogInformation("Invalidated all caches for company {CompanyId}", companyId);
        }

        /// <summary>
        /// ⚡ Invalidate specific report cache
        /// </summary>
        public void InvalidateReportCache(int companyId, string reportType, DateTime? date = null)
        {
            var cacheKey = reportType.ToLower() switch
            {
                "trialbalance" => $"{TrialBalancePrefix}{companyId}_{date:yyyy-MM-dd}",
                "balancesheet" => $"{BalanceSheetPrefix}{companyId}_{date:yyyy-MM-dd}",
                "incomestatement" => $"{IncomeStatementPrefix}{companyId}_{date:yyyy-MM-dd}",
                "dashboard" => $"{DashboardPrefix}{companyId}",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(cacheKey))
            {
                _cache.Remove(cacheKey);
                _logger.LogInformation("Invalidated {ReportType} cache for company {CompanyId}", reportType, companyId);
            }
        }

        /// <summary>
        /// ⚡ Get cache statistics
        /// </summary>
        public CacheStatistics GetCacheStatistics()
        {
            var stats = new CacheStatistics
            {
                GeneratedAt = DateTime.UtcNow
            };

            // Count cached items by type
            var trialBalanceCount = CountCacheEntries(TrialBalancePrefix);
            var balanceSheetCount = CountCacheEntries(BalanceSheetPrefix);
            var incomeStatementCount = CountCacheEntries(IncomeStatementPrefix);
            var dashboardCount = CountCacheEntries(DashboardPrefix);

            stats.CachedReportsCount = trialBalanceCount + balanceSheetCount + incomeStatementCount + dashboardCount;
            stats.TrialBalanceCacheCount = trialBalanceCount;
            stats.BalanceSheetCacheCount = balanceSheetCount;
            stats.IncomeStatementCacheCount = incomeStatementCount;
            stats.DashboardCacheCount = dashboardCount;

            return stats;
        }

        /// <summary>
        /// ⚡ Warm up caches for a company
        /// </summary>
        public async Task WarmUpCachesAsync(int companyId)
        {
            _logger.LogInformation("Warming up caches for company {CompanyId}", companyId);

            var tasks = new[]
            {
                GetTrialBalanceAsync(companyId),
                GetBalanceSheetAsync(companyId),
                GetIncomeStatementAsync(companyId),
                GetDashboardAsync(companyId)
            };

            await Task.WhenAll(tasks);

            _logger.LogInformation("Cache warm-up completed for company {CompanyId}", companyId);
        }

        #region Private Methods

        private async Task<TrialBalanceSnapshot> GenerateTrialBalanceSnapshotAsync(int companyId, DateTime? asOfDate)
        {
            var snapshot = new TrialBalanceSnapshot
            {
                CompanyId = companyId,
                AsOfDate = asOfDate ?? DateTime.UtcNow,
                GeneratedAt = DateTime.UtcNow
            };

            try
            {
                var cutoffDate = asOfDate ?? DateTime.MaxValue;
                
                // Get all posted journal entries up to the cutoff date
                var journalEntries = await _context.JournalEntries
                    .Where(j => j.CompanyId == companyId && 
                               j.Status.ToString() == "Posted" && 
                               j.TransactionDate <= cutoffDate)
                    .Include(j => j.JournalLines)
                    .ToListAsync();

                // Group by account and calculate balances
                var accountBalances = new Dictionary<int, decimal>();
                
                foreach (var entry in journalEntries)
                {
                    foreach (var line in entry.JournalLines)
                    {
                        if (!accountBalances.ContainsKey(line.AccountId))
                        {
                            accountBalances[line.AccountId] = 0;
                        }
                        
                        // Debit increases balance, Credit decreases balance
                        accountBalances[line.AccountId] += line.DebitAmount - line.CreditAmount;
                    }
                }

                // Get account details
                var accounts = await _context.FinanceAccounts
                    .Where(a => a.CompanyId == companyId)
                    .ToListAsync();

                snapshot.Accounts = accounts.Select(account => new TrialBalanceAccount
                {
                    AccountId = account.Id,
                    AccountCode = account.AccountCode,
                    AccountName = account.AccountName,
                    AccountType = account.AccountType,
                    Balance = accountBalances.GetValueOrDefault(account.Id, 0),
                    IsDebitBalance = account.AccountType == AccountType.Asset || 
                                    account.AccountType == AccountType.Expense
                }).ToList();

                snapshot.TotalDebits = snapshot.Accounts.Where(a => a.IsDebitBalance).Sum(a => a.Balance);
                snapshot.TotalCredits = snapshot.Accounts.Where(a => !a.IsDebitBalance).Sum(a => a.Balance);
                snapshot.IsValid = Math.Abs(snapshot.TotalDebits - snapshot.TotalCredits) < 0.01m;

                snapshot.GenerationTimeMs = (DateTime.UtcNow - snapshot.GeneratedAt).TotalMilliseconds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate trial balance snapshot for company {CompanyId}", companyId);
                snapshot.IsValid = false;
                snapshot.ErrorMessage = ex.Message;
            }

            return snapshot;
        }

        private async Task<BalanceSheetSnapshot> GenerateBalanceSheetSnapshotAsync(int companyId, DateTime? asOfDate)
        {
            var snapshot = new BalanceSheetSnapshot
            {
                CompanyId = companyId,
                AsOfDate = asOfDate ?? DateTime.UtcNow,
                GeneratedAt = DateTime.UtcNow
            };

            try
            {
                // Get trial balance data
                var trialBalance = await GenerateTrialBalanceSnapshotAsync(companyId, asOfDate);
                
                // Categorize accounts
                var assets = trialBalance.Accounts.Where(a => a.AccountType == AccountType.Asset).ToList();
                var liabilities = trialBalance.Accounts.Where(a => a.AccountType == AccountType.Liability).ToList();
                var equity = trialBalance.Accounts.Where(a => a.AccountType == AccountType.Equity).ToList();

                snapshot.Assets = new BalanceSheetSection
                {
                    Accounts = assets,
                    Total = assets.Sum(a => a.Balance)
                };

                snapshot.Liabilities = new BalanceSheetSection
                {
                    Accounts = liabilities,
                    Total = liabilities.Sum(a => a.Balance)
                };

                snapshot.Equity = new BalanceSheetSection
                {
                    Accounts = equity,
                    Total = equity.Sum(a => a.Balance)
                };

                snapshot.TotalLiabilitiesAndEquity = snapshot.Liabilities.Total + snapshot.Equity.Total;
                snapshot.IsValid = Math.Abs(snapshot.Assets.Total - snapshot.TotalLiabilitiesAndEquity) < 0.01m;

                snapshot.GenerationTimeMs = (DateTime.UtcNow - snapshot.GeneratedAt).TotalMilliseconds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate balance sheet snapshot for company {CompanyId}", companyId);
                snapshot.IsValid = false;
                snapshot.ErrorMessage = ex.Message;
            }

            return snapshot;
        }

        private async Task<IncomeStatementSnapshot> GenerateIncomeStatementSnapshotAsync(int companyId, DateTime? fromDate, DateTime? toDate)
        {
            var snapshot = new IncomeStatementSnapshot
            {
                CompanyId = companyId,
                FromDate = fromDate ?? DateTime.MinValue,
                ToDate = toDate ?? DateTime.UtcNow,
                GeneratedAt = DateTime.UtcNow
            };

            try
            {
                var fromDateValue = fromDate ?? DateTime.MinValue;
                var toDateValue = toDate ?? DateTime.MaxValue;

                // Get all posted journal entries in the period
                var journalEntries = await _context.JournalEntries
                    .Where(j => j.CompanyId == companyId && 
                               j.Status.ToString() == "Posted" && 
                               j.TransactionDate >= fromDateValue && 
                               j.TransactionDate <= toDateValue)
                    .Include(j => j.JournalLines)
                    .ToListAsync();

                // Group by account and calculate balances
                var accountBalances = new Dictionary<int, decimal>();
                
                foreach (var entry in journalEntries)
                {
                    foreach (var line in entry.JournalLines)
                    {
                        if (!accountBalances.ContainsKey(line.AccountId))
                        {
                            accountBalances[line.AccountId] = 0;
                        }
                        
                        accountBalances[line.AccountId] += line.CreditAmount - line.DebitAmount;
                    }
                }

                // Get account details
                var accounts = await _context.FinanceAccounts
                    .Where(a => a.CompanyId == companyId)
                    .ToListAsync();

                var revenueAccounts = accounts.Where(a => a.AccountType == AccountType.Revenue).ToList();
                var expenseAccounts = accounts.Where(a => a.AccountType == AccountType.Expense).ToList();

                snapshot.Revenue = new IncomeStatementSection
                {
                    Accounts = revenueAccounts.Select(account => new IncomeStatementAccount
                    {
                        AccountId = account.Id,
                        AccountCode = account.AccountCode,
                        AccountName = account.AccountName,
                        Amount = accountBalances.GetValueOrDefault(account.Id, 0)
                    }).ToList(),
                    Total = revenueAccounts.Sum(a => accountBalances.GetValueOrDefault(a.Id, 0))
                };

                snapshot.Expenses = new IncomeStatementSection
                {
                    Accounts = expenseAccounts.Select(account => new IncomeStatementAccount
                    {
                        AccountId = account.Id,
                        AccountCode = account.AccountCode,
                        AccountName = account.AccountName,
                        Amount = accountBalances.GetValueOrDefault(account.Id, 0)
                    }).ToList(),
                    Total = expenseAccounts.Sum(a => accountBalances.GetValueOrDefault(a.Id, 0))
                };

                snapshot.NetIncome = snapshot.Revenue.Total - snapshot.Expenses.Total;
                snapshot.GrossProfit = snapshot.Revenue.Total; // Simplified for now

                snapshot.GenerationTimeMs = (DateTime.UtcNow - snapshot.GeneratedAt).TotalMilliseconds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate income statement snapshot for company {CompanyId}", companyId);
                snapshot.IsValid = false;
                snapshot.ErrorMessage = ex.Message;
            }

            return snapshot;
        }

        private async Task<DashboardSnapshot> GenerateDashboardSnapshotAsync(int companyId)
        {
            var snapshot = new DashboardSnapshot
            {
                CompanyId = companyId,
                GeneratedAt = DateTime.UtcNow
            };

            try
            {
                // Get recent transactions
                var recentTransactions = await _context.JournalEntries
                    .Where(j => j.CompanyId == companyId && 
                               j.Status.ToString() == "Posted")
                    .OrderByDescending(j => j.TransactionDate)
                    .Take(10)
                    .ToListAsync();

                snapshot.RecentTransactions = recentTransactions;

                // Get account summary
                var accounts = await _context.FinanceAccounts
                    .Where(a => a.CompanyId == companyId)
                    .GroupBy(a => a.AccountType)
                    .Select(g => new AccountSummary
                    {
                        AccountType = g.Key,
                        Count = g.Count(),
                        TotalBalance = 0 // Would need to calculate from journal entries
                    })
                    .ToListAsync();

                snapshot.AccountSummary = accounts;

                // Get period status
                var currentPeriod = DateTime.Now.ToString("yyyy-MM");
                var periodClose = await _context.PeriodClosings
                    .Where(p => p.CompanyId == companyId && p.Period == currentPeriod)
                    .FirstOrDefaultAsync();

                snapshot.CurrentPeriodClosed = periodClose != null;

                snapshot.GenerationTimeMs = (DateTime.UtcNow - snapshot.GeneratedAt).TotalMilliseconds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate dashboard snapshot for company {CompanyId}", companyId);
                snapshot.IsValid = false;
                snapshot.ErrorMessage = ex.Message;
            }

            return snapshot;
        }

        private int CountCacheEntries(string prefix)
        {
            // This is a simplified implementation
            // In a real implementation, you'd need to track cache entries differently
            return 0;
        }

        #endregion
    }

    #region Supporting Classes

    public class TrialBalanceSnapshot
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<TrialBalanceAccount> Accounts { get; set; } = new();
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public double GenerationTimeMs { get; set; }
    }

    public class TrialBalanceAccount
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public AccountType AccountType { get; set; }
        public decimal Balance { get; set; }
        public bool IsDebitBalance { get; set; }
    }

    public class BalanceSheetSnapshot
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public DateTime GeneratedAt { get; set; }
        public BalanceSheetSection Assets { get; set; } = new();
        public BalanceSheetSection Liabilities { get; set; } = new();
        public BalanceSheetSection Equity { get; set; } = new();
        public decimal TotalLiabilitiesAndEquity { get; set; }
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public double GenerationTimeMs { get; set; }
    }

    public class BalanceSheetSection
    {
        public List<TrialBalanceAccount> Accounts { get; set; } = new();
        public decimal Total { get; set; }
    }

    public class IncomeStatementSnapshot
    {
        public int CompanyId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateTime GeneratedAt { get; set; }
        public IncomeStatementSection Revenue { get; set; } = new();
        public IncomeStatementSection Expenses { get; set; } = new();
        public decimal GrossProfit { get; set; }
        public decimal NetIncome { get; set; }
        public bool IsValid { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public double GenerationTimeMs { get; set; }
    }

    public class IncomeStatementSection
    {
        public List<IncomeStatementAccount> Accounts { get; set; } = new();
        public decimal Total { get; set; }
    }

    public class IncomeStatementAccount
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class DashboardSnapshot
    {
        public int CompanyId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<JournalEntry> RecentTransactions { get; set; } = new();
        public List<AccountSummary> AccountSummary { get; set; } = new();
        public bool CurrentPeriodClosed { get; set; }
        public bool IsValid { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public double GenerationTimeMs { get; set; }
    }

    public class AccountSummary
    {
        public AccountType AccountType { get; set; }
        public int Count { get; set; }
        public decimal TotalBalance { get; set; }
    }

    public class CacheStatistics
    {
        public DateTime GeneratedAt { get; set; }
        public int CachedReportsCount { get; set; }
        public int TrialBalanceCacheCount { get; set; }
        public int BalanceSheetCacheCount { get; set; }
        public int IncomeStatementCacheCount { get; set; }
        public int DashboardCacheCount { get; set; }
    }

    #endregion
}
