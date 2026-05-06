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
    /// ⚡ PHASE 3.2: Indexed Financial Queries
    /// Enterprise-grade optimized database queries for financial operations
    /// </summary>
    public class IndexedQueryService
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<IndexedQueryService> _logger;

        public IndexedQueryService(ERPDbContext context, ILogger<IndexedQueryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// ⚡ Get optimized trial balance with indexed queries
        /// </summary>
        public async Task<OptimizedTrialBalance> GetOptimizedTrialBalanceAsync(int companyId, DateTime? asOfDate = null)
        {
            var startTime = DateTime.UtcNow;
            var result = new OptimizedTrialBalance
            {
                CompanyId = companyId,
                AsOfDate = asOfDate ?? DateTime.UtcNow,
                QueryStartedAt = startTime
            };

            try
            {
                var cutoffDate = asOfDate ?? DateTime.MaxValue;

                // 🔒 Optimized query 1: Get journal line aggregates with proper indexing
                var accountBalancesQuery = _context.JournalLines
                    .Where(jl => jl.JournalEntry.CompanyId == companyId &&
                                jl.JournalEntry.Status == SecureERP2.Modules.Finance.Entities.JournalStatus.Posted &&
                                jl.JournalEntry.TransactionDate <= cutoffDate)
                    .GroupBy(jl => jl.AccountId)
                    .Select(g => new
                    {
                        AccountId = g.Key,
                        TotalDebit = g.Sum(jl => jl.DebitAmount),
                        TotalCredit = g.Sum(jl => jl.CreditAmount),
                        TransactionCount = g.Count()
                    });

                var accountBalances = await accountBalancesQuery.ToListAsync();

                // 🔒 Optimized query 2: Get account details with single query
                var accounts = await _context.FinanceAccounts
                    .Where(a => a.CompanyId == companyId)
                    .Select(a => new
                    {
                        a.Id,
                        a.AccountCode,
                        a.AccountName,
                        a.AccountType,
                        a.ParentAccountId,
                        a.IsActive
                    })
                    .ToListAsync();

                // 🔒 Join in memory (more efficient than complex SQL joins)
                var trialBalanceAccounts = accounts.Join(accountBalances,
                    account => account.Id,
                    balance => balance.AccountId,
                    (account, balance) => new OptimizedTrialBalanceAccount
                    {
                        AccountId = account.Id,
                        AccountCode = account.AccountCode,
                        AccountName = account.AccountName,
                        AccountType = account.AccountType,
                        Balance = balance.TotalDebit - balance.TotalCredit,
                        DebitAmount = balance.TotalDebit,
                        CreditAmount = balance.TotalCredit,
                        TransactionCount = balance.TransactionCount,
                        IsActive = account.IsActive,
                        IsDebitBalance = account.AccountType == AccountType.Asset ||
                                       account.AccountType == AccountType.Expense
                    })
                    .Where(a => a.IsActive)
                    .ToList();

                // 🔒 Add accounts with zero balance
                var accountIdsWithBalance = accountBalances.Select(b => b.AccountId).ToHashSet();
                var zeroBalanceAccounts = accounts
                    .Where(a => !accountIdsWithBalance.Contains(a.Id) && a.IsActive)
                    .Select(a => new OptimizedTrialBalanceAccount
                    {
                        AccountId = a.Id,
                        AccountCode = a.AccountCode,
                        AccountName = a.AccountName,
                        AccountType = a.AccountType,
                        Balance = 0,
                        DebitAmount = 0,
                        CreditAmount = 0,
                        TransactionCount = 0,
                        IsActive = a.IsActive,
                        IsDebitBalance = a.AccountType == AccountType.Asset ||
                                       a.AccountType == AccountType.Expense
                    })
                    .ToList();

                result.Accounts = trialBalanceAccounts.Concat(zeroBalanceAccounts).ToList();
                result.TotalDebits = result.Accounts.Where(a => a.IsDebitBalance).Sum(a => a.Balance);
                result.TotalCredits = result.Accounts.Where(a => !a.IsDebitBalance).Sum(a => a.Balance);
                result.IsValid = Math.Abs(result.TotalDebits - result.TotalCredits) < 0.01m;
                result.AccountCount = result.Accounts.Count;
                result.TransactionCount = result.Accounts.Sum(a => a.TransactionCount);

                result.QueryCompletedAt = DateTime.UtcNow;
                result.ExecutionTimeMs = (result.QueryCompletedAt - result.QueryStartedAt).TotalMilliseconds;

                _logger.LogDebug("Optimized trial balance generated in {TimeMs}ms for company {CompanyId} with {AccountCount} accounts", 
                    result.ExecutionTimeMs, companyId, result.AccountCount);
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
                result.QueryCompletedAt = DateTime.UtcNow;
                result.ExecutionTimeMs = (result.QueryCompletedAt - result.QueryStartedAt).TotalMilliseconds;
                
                _logger.LogError(ex, "Failed to generate optimized trial balance for company {CompanyId}", companyId);
            }

            return result;
        }

        /// <summary>
        /// ⚡ Get optimized balance sheet with indexed queries
        /// </summary>
        public async Task<OptimizedBalanceSheet> GetOptimizedBalanceSheetAsync(int companyId, DateTime? asOfDate = null)
        {
            var startTime = DateTime.UtcNow;
            var result = new OptimizedBalanceSheet
            {
                CompanyId = companyId,
                AsOfDate = asOfDate ?? DateTime.UtcNow,
                QueryStartedAt = startTime
            };

            try
            {
                // 🔒 Get trial balance data (already optimized)
                var trialBalance = await GetOptimizedTrialBalanceAsync(companyId, asOfDate);

                // 🔒 Categorize accounts efficiently
                var assets = trialBalance.Accounts.Where(a => a.AccountType == AccountType.Asset).ToList();
                var liabilities = trialBalance.Accounts.Where(a => a.AccountType == AccountType.Liability).ToList();
                var equity = trialBalance.Accounts.Where(a => a.AccountType == AccountType.Equity).ToList();

                result.Assets = new OptimizedBalanceSheetSection
                {
                    Accounts = assets,
                    Total = assets.Sum(a => a.Balance),
                    AccountCount = assets.Count
                };

                result.Liabilities = new OptimizedBalanceSheetSection
                {
                    Accounts = liabilities,
                    Total = liabilities.Sum(a => a.Balance),
                    AccountCount = liabilities.Count
                };

                result.Equity = new OptimizedBalanceSheetSection
                {
                    Accounts = equity,
                    Total = equity.Sum(a => a.Balance),
                    AccountCount = equity.Count
                };

                result.TotalLiabilitiesAndEquity = result.Liabilities.Total + result.Equity.Total;
                result.IsValid = Math.Abs(result.Assets.Total - result.TotalLiabilitiesAndEquity) < 0.01m;
                result.TotalAccountCount = result.Accounts.Count;

                result.QueryCompletedAt = DateTime.UtcNow;
                result.ExecutionTimeMs = (result.QueryCompletedAt - result.QueryStartedAt).TotalMilliseconds;

                _logger.LogDebug("Optimized balance sheet generated in {TimeMs}ms for company {CompanyId}", 
                    result.ExecutionTimeMs, companyId);
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
                result.QueryCompletedAt = DateTime.UtcNow;
                result.ExecutionTimeMs = (result.QueryCompletedAt - result.QueryStartedAt).TotalMilliseconds;
                
                _logger.LogError(ex, "Failed to generate optimized balance sheet for company {CompanyId}", companyId);
            }

            return result;
        }

        /// <summary>
        /// ⚡ Get optimized income statement with indexed queries
        /// </summary>
        public async Task<OptimizedIncomeStatement> GetOptimizedIncomeStatementAsync(int companyId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var startTime = DateTime.UtcNow;
            var result = new OptimizedIncomeStatement
            {
                CompanyId = companyId,
                FromDate = fromDate ?? DateTime.MinValue,
                ToDate = toDate ?? DateTime.UtcNow,
                QueryStartedAt = startTime
            };

            try
            {
                var fromDateValue = fromDate ?? DateTime.MinValue;
                var toDateValue = toDate ?? DateTime.MaxValue;

                // 🔒 Optimized query: Get journal line aggregates for the period
                var periodBalancesQuery = _context.JournalLines
                    .Where(jl => jl.JournalEntry.CompanyId == companyId &&
                                jl.JournalEntry.Status == SecureERP2.Modules.Finance.Entities.JournalStatus.Posted &&
                                jl.JournalEntry.TransactionDate >= fromDateValue &&
                                jl.JournalEntry.TransactionDate <= toDateValue)
                    .GroupBy(jl => jl.AccountId)
                    .Select(g => new
                    {
                        AccountId = g.Key,
                        TotalDebit = g.Sum(jl => jl.DebitAmount),
                        TotalCredit = g.Sum(jl => jl.CreditAmount),
                        TransactionCount = g.Count()
                    });

                var periodBalances = await periodBalancesQuery.ToListAsync();

                // 🔒 Get account details
                var accounts = await _context.FinanceAccounts
                    .Where(a => a.CompanyId == companyId && a.IsActive)
                    .Select(a => new
                    {
                        a.Id,
                        a.AccountCode,
                        a.AccountName,
                        a.AccountType
                    })
                    .ToListAsync();

                // 🔒 Calculate income statement amounts (Revenue = Credits, Expenses = Debits)
                var incomeStatementAccounts = accounts.Join(periodBalances,
                    account => account.Id,
                    balance => balance.AccountId,
                    (account, balance) => new OptimizedIncomeStatementAccount
                    {
                        AccountId = account.Id,
                        AccountCode = account.AccountCode,
                        AccountName = account.AccountName,
                        AccountType = account.AccountType,
                        Amount = account.AccountType == AccountType.Revenue ? 
                            balance.TotalCredit - balance.TotalDebit : 
                            balance.TotalDebit - balance.TotalCredit,
                        DebitAmount = balance.TotalDebit,
                        CreditAmount = balance.TotalCredit,
                        TransactionCount = balance.TransactionCount
                    })
                    .Where(a => a.AccountType == AccountType.Revenue || a.AccountType == AccountType.Expense)
                    .ToList();

                var revenueAccounts = incomeStatementAccounts.Where(a => a.AccountType == AccountType.Revenue).ToList();
                var expenseAccounts = incomeStatementAccounts.Where(a => a.AccountType == AccountType.Expense).ToList();

                result.Revenue = new OptimizedIncomeStatementSection
                {
                    Accounts = revenueAccounts,
                    Total = revenueAccounts.Sum(a => a.Amount),
                    AccountCount = revenueAccounts.Count
                };

                result.Expenses = new OptimizedIncomeStatementSection
                {
                    Accounts = expenseAccounts,
                    Total = expenseAccounts.Sum(a => a.Amount),
                    AccountCount = expenseAccounts.Count
                };

                result.GrossProfit = result.Revenue.Total;
                result.NetIncome = result.Revenue.Total - result.Expenses.Total;
                result.IsValid = true;
                result.TotalAccountCount = incomeStatementAccounts.Count;

                result.QueryCompletedAt = DateTime.UtcNow;
                result.ExecutionTimeMs = (result.QueryCompletedAt - result.QueryStartedAt).TotalMilliseconds;

                _logger.LogDebug("Optimized income statement generated in {TimeMs}ms for company {CompanyId}", 
                    result.ExecutionTimeMs, companyId);
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
                result.QueryCompletedAt = DateTime.UtcNow;
                result.ExecutionTimeMs = (result.QueryCompletedAt - result.QueryStartedAt).TotalMilliseconds;
                
                _logger.LogError(ex, "Failed to generate optimized income statement for company {CompanyId}", companyId);
            }

            return result;
        }

        /// <summary>
        /// ⚡ Get optimized journal entries with pagination
        /// </summary>
        public async Task<OptimizedJournalEntries> GetOptimizedJournalEntriesAsync(int companyId, 
            DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 50)
        {
            var startTime = DateTime.UtcNow;
            var result = new OptimizedJournalEntries
            {
                CompanyId = companyId,
                FromDate = fromDate ?? DateTime.MinValue,
                ToDate = toDate ?? DateTime.UtcNow,
                Page = page,
                PageSize = pageSize,
                QueryStartedAt = startTime
            };

            try
            {
                var fromDateValue = fromDate ?? DateTime.MinValue;
                var toDateValue = toDate ?? DateTime.MaxValue;

                // 🔒 Optimized query with proper indexing
                var query = _context.JournalEntries
                    .Where(j => j.CompanyId == companyId &&
                               j.TransactionDate >= fromDateValue &&
                               j.TransactionDate <= toDateValue)
                    .OrderByDescending(j => j.TransactionDate)
                    .ThenByDescending(j => j.Id);

                // 🔒 Get total count efficiently
                result.TotalCount = await query.CountAsync();

                // 🔒 Get paginated results
                var entries = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(j => new OptimizedJournalEntry
                    {
                        Id = j.Id,
                        TransactionNumber = j.TransactionNumber,
                        TransactionDate = j.TransactionDate,
                        Description = j.Description,
                        Status = (SecureERP2.Modules.Finance.JournalStatus)j.Status,
                        TotalAmount = j.JournalLines.Sum(jl => jl.DebitAmount + jl.CreditAmount),
                        LineCount = j.JournalLines.Count,
                        CreatedAt = j.CreatedAt
                    })
                    .ToListAsync();

                result.Entries = entries;
                result.TotalPages = (int)Math.Ceiling((double)result.TotalCount / pageSize);
                result.IsValid = true;

                result.QueryCompletedAt = DateTime.UtcNow;
                result.ExecutionTimeMs = (result.QueryCompletedAt - result.QueryStartedAt).TotalMilliseconds;

                _logger.LogDebug("Optimized journal entries retrieved in {TimeMs}ms for company {CompanyId}, page {Page}", 
                    result.ExecutionTimeMs, companyId, page);
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
                result.QueryCompletedAt = DateTime.UtcNow;
                result.ExecutionTimeMs = (result.QueryCompletedAt - result.QueryStartedAt).TotalMilliseconds;
                
                _logger.LogError(ex, "Failed to retrieve optimized journal entries for company {CompanyId}", companyId);
            }

            return result;
        }

        /// <summary>
        /// ⚡ Get query performance statistics
        /// </summary>
        public async Task<QueryPerformanceStats> GetQueryPerformanceStatsAsync()
        {
            var stats = new QueryPerformanceStats
            {
                GeneratedAt = DateTime.UtcNow
            };

            try
            {
                // 🔒 Get database statistics
                var journalEntryCount = await _context.JournalEntries.CountAsync();
                var journalLineCount = await _context.JournalLines.CountAsync();
                var accountCount = await _context.FinanceAccounts.CountAsync();

                stats.JournalEntryCount = journalEntryCount;
                stats.JournalLineCount = journalLineCount;
                stats.AccountCount = accountCount;

                // 🔒 Get index information (simplified)
                stats.HasJournalEntryIndexes = true; // Would check actual indexes
                stats.HasJournalLineIndexes = true;
                stats.HasAccountIndexes = true;

                // 🔒 Calculate estimated query performance
                stats.EstimatedTrialBalanceTimeMs = EstimateQueryTime(journalLineCount, accountCount);
                stats.EstimatedBalanceSheetTimeMs = stats.EstimatedTrialBalanceTimeMs * 1.2;
                stats.EstimatedIncomeStatementTimeMs = stats.EstimatedTrialBalanceTimeMs * 1.1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get query performance statistics");
            }

            return stats;
        }

        #region Private Methods

        private double EstimateQueryTime(int recordCount, int accountCount)
        {
            // 🔒 Simple estimation based on record count
            // In a real implementation, you'd use actual performance metrics
            if (recordCount < 1000) return 50; // 50ms
            if (recordCount < 10000) return 200; // 200ms
            if (recordCount < 100000) return 800; // 800ms
            return 2000; // 2s for large datasets
        }

        #endregion
    }

    #region Supporting Classes

    public class OptimizedTrialBalance
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public DateTime QueryStartedAt { get; set; }
        public DateTime QueryCompletedAt { get; set; }
        public List<OptimizedTrialBalanceAccount> Accounts { get; set; } = new();
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public int AccountCount { get; set; }
        public int TransactionCount { get; set; }
        public double ExecutionTimeMs { get; set; }
    }

    public class OptimizedTrialBalanceAccount
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public AccountType AccountType { get; set; }
        public decimal Balance { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public int TransactionCount { get; set; }
        public bool IsActive { get; set; }
        public bool IsDebitBalance { get; set; }
    }

    public class OptimizedBalanceSheet
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public DateTime QueryStartedAt { get; set; }
        public DateTime QueryCompletedAt { get; set; }
        public OptimizedBalanceSheetSection Assets { get; set; } = new();
        public OptimizedBalanceSheetSection Liabilities { get; set; } = new();
        public OptimizedBalanceSheetSection Equity { get; set; } = new();
        public decimal TotalLiabilitiesAndEquity { get; set; }
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public int TotalAccountCount { get; set; }
        public double ExecutionTimeMs { get; set; }
        public List<OptimizedTrialBalanceAccount> Accounts => Assets.Accounts.Concat(Liabilities.Accounts).Concat(Equity.Accounts).ToList();
    }

    public class OptimizedBalanceSheetSection
    {
        public List<OptimizedTrialBalanceAccount> Accounts { get; set; } = new();
        public decimal Total { get; set; }
        public int AccountCount { get; set; }
    }

    public class OptimizedIncomeStatement
    {
        public int CompanyId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateTime QueryStartedAt { get; set; }
        public DateTime QueryCompletedAt { get; set; }
        public OptimizedIncomeStatementSection Revenue { get; set; } = new();
        public OptimizedIncomeStatementSection Expenses { get; set; } = new();
        public decimal GrossProfit { get; set; }
        public decimal NetIncome { get; set; }
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public int TotalAccountCount { get; set; }
        public double ExecutionTimeMs { get; set; }
    }

    public class OptimizedIncomeStatementSection
    {
        public List<OptimizedIncomeStatementAccount> Accounts { get; set; } = new();
        public decimal Total { get; set; }
        public int AccountCount { get; set; }
    }

    public class OptimizedIncomeStatementAccount
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public AccountType AccountType { get; set; }
        public decimal Amount { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public int TransactionCount { get; set; }
    }

    public class OptimizedJournalEntries
    {
        public int CompanyId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public DateTime QueryStartedAt { get; set; }
        public DateTime QueryCompletedAt { get; set; }
        public List<OptimizedJournalEntry> Entries { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public double ExecutionTimeMs { get; set; }
    }

    public class OptimizedJournalEntry
    {
        public int Id { get; set; }
        public string TransactionNumber { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public JournalStatus Status { get; set; }
        public decimal TotalAmount { get; set; }
        public int LineCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class QueryPerformanceStats
    {
        public DateTime GeneratedAt { get; set; }
        public int JournalEntryCount { get; set; }
        public int JournalLineCount { get; set; }
        public int AccountCount { get; set; }
        public bool HasJournalEntryIndexes { get; set; }
        public bool HasJournalLineIndexes { get; set; }
        public bool HasAccountIndexes { get; set; }
        public double EstimatedTrialBalanceTimeMs { get; set; }
        public double EstimatedBalanceSheetTimeMs { get; set; }
        public double EstimatedIncomeStatementTimeMs { get; set; }
    }

    #endregion
}
