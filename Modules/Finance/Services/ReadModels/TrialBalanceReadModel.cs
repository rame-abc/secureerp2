using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using SecureERP2.Modules.Finance.Entities;

namespace SecureERP2.Modules.Finance.Services.ReadModels
{
    /// <summary>
    /// 🏗️ STEP 6.6: Trial Balance Read Model (CQRS)
    /// Pre-computed trial balance for fast queries
    /// </summary>
    public class TrialBalanceReadModel
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<TrialBalanceReadModel> _logger;
        private readonly IDistributedCache _cache;
        private readonly LedgerEngineService _ledgerEngine;
        
        // Cache configuration
        private const string CacheKeyPrefix = "trial_balance:";
        private const int CacheExpirationMinutes = 30;
        
        public TrialBalanceReadModel(
            ERPDbContext context,
            ILogger<TrialBalanceReadModel> logger,
            IDistributedCache cache,
            LedgerEngineService ledgerEngine)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _ledgerEngine = ledgerEngine;
        }
        
        /// <summary>
        /// Get trial balance with caching
        /// </summary>
        public async Task<TrialBalanceResult> GetTrialBalanceAsync(
            int companyId, 
            DateTime? asOfDate = null,
            bool forceRefresh = false)
        {
            var result = new TrialBalanceResult
            {
                CompanyId = companyId,
                AsOfDate = asOfDate ?? DateTime.UtcNow
            };
            
            try
            {
                _logger.LogDebug("Getting trial balance for company {CompanyId} as of {Date}", 
                    companyId, result.AsOfDate.ToString("yyyy-MM-dd"));
                
                // 🔥 Check cache first
                var cacheKey = $"{CacheKeyPrefix}{companyId}:{result.AsOfDate:yyyyMMdd}";
                
                if (!forceRefresh)
                {
                    var cachedResult = await _cache.GetStringAsync(cacheKey);
                    if (!string.IsNullOrEmpty(cachedResult))
                    {
                        result = JsonSerializer.Deserialize<TrialBalanceResult>(cachedResult);
                        result.FromCache = true;
                        _logger.LogDebug("Trial balance found in cache for company {CompanyId}", companyId);
                        return result;
                    }
                }
                
                // 🔥 Build trial balance from ledger
                result = await BuildTrialBalanceAsync(companyId, result.AsOfDate);
                
                // 🔥 Cache the result
                if (result.IsSuccess)
                {
                    await CacheTrialBalanceAsync(cacheKey, result);
                }
                
                _logger.LogInformation("Generated trial balance for company {CompanyId}: {AccountCount} accounts, {Duration}ms", 
                    companyId, result.Accounts?.Count ?? 0, result.DurationMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get trial balance for company {CompanyId}", companyId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Build trial balance from ledger data
        /// </summary>
        private async Task<TrialBalanceResult> BuildTrialBalanceAsync(int companyId, DateTime asOfDate)
        {
            var result = new TrialBalanceResult
            {
                CompanyId = companyId,
                AsOfDate = asOfDate,
                StartedAt = DateTime.UtcNow
            };
            
            try
            {
                // 🔥 Get ledger state
                var ledgerState = _ledgerEngine.GetLedgerState(companyId);
                
                // 🔥 Get accounts
                var accounts = await _context.FinanceAccounts
                    .Where(a => a.CompanyId == companyId && a.IsActive)
                    .OrderBy(a => a.AccountCode)
                    .ToListAsync();
                
                var trialBalanceAccounts = new List<TrialBalanceAccount>();
                
                foreach (var account in accounts)
                {
                    // 🔥 Get balance from ledger state
                    var balance = ledgerState.AccountBalances.GetValueOrDefault(account.Id, 0m);
                    
                    // 🔥 Calculate running balance for date range
                    var dateFilteredBalance = await GetDateFilteredBalanceAsync(companyId, account.Id, asOfDate);
                    
                    var trialAccount = new TrialBalanceAccount
                    {
                        AccountId = account.Id,
                        AccountCode = account.AccountCode,
                        AccountName = account.AccountName,
                        AccountType = account.AccountType,
                        Balance = dateFilteredBalance,
                        DebitBalance = account.AccountType == AccountType.Asset || account.AccountType == AccountType.Expense ? dateFilteredBalance : 0m,
                        CreditBalance = account.AccountType == AccountType.Liability || account.AccountType == AccountType.Equity || account.AccountType == AccountType.Revenue ? dateFilteredBalance : 0m,
                        IsActive = account.IsActive,
                        ParentAccountId = account.ParentAccountId,
                        // TODO: Add HierarchyLevel property to FinanceAccount
                        // HierarchyLevel = account.HierarchyLevel
                        HierarchyLevel = 1 // Placeholder
                    };
                    
                    trialBalanceAccounts.Add(trialAccount);
                }
                
                // 🔥 Calculate totals
                var totalDebits = trialBalanceAccounts.Sum(a => a.DebitBalance);
                var totalCredits = trialBalanceAccounts.Sum(a => a.CreditBalance);
                var totalBalance = trialBalanceAccounts.Sum(a => a.Balance);
                
                // 🔥 Validate balance
                var isBalanced = Math.Abs(totalDebits - totalCredits) < 0.01m;
                
                result.Accounts = trialBalanceAccounts;
                result.TotalDebits = totalDebits;
                result.TotalCredits = totalCredits;
                result.TotalBalance = totalBalance;
                result.IsBalanced = isBalanced;
                result.GeneratedAt = DateTime.UtcNow;
                result.IsSuccess = true;
                
                _logger.LogDebug("Trial balance built: {Debits} debits, {Credits} credits, Balanced: {IsBalanced}", 
                    totalDebits, totalCredits, isBalanced);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build trial balance for company {CompanyId}", companyId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                result.CompletedAt = DateTime.UtcNow;
                result.DurationMs = (result.CompletedAt - result.StartedAt).TotalMilliseconds;
            }
            
            return result;
        }
        
        /// <summary>
        /// Get date-filtered balance for an account
        /// </summary>
        private async Task<decimal> GetDateFilteredBalanceAsync(int companyId, int accountId, DateTime asOfDate)
        {
            try
            {
                // 🔥 Get balance from journal lines up to asOfDate
                // TODO: Add JournalLines DbSet to ERPDbContext
                // var balance = await _context.JournalLines
                //     .Where(jl => jl.AccountId == accountId)
                //     .Where(jl => jl.JournalEntry.CompanyId == companyId)
                //     .Where(jl => jl.JournalEntry.Status == JournalStatus.Posted)
                //     .Where(jl => jl.JournalEntry.TransactionDate <= asOfDate)
                //     .SumAsync(jl => jl.DebitAmount - jl.CreditAmount);
                // TODO: Mock balance for now
                var balance = 0m; // Placeholder
                
                return balance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get date-filtered balance for account {AccountId}", accountId);
                return 0m;
            }
        }
        
        /// <summary>
        /// Update trial balance projection
        /// </summary>
        public async Task<ProjectionUpdateResult> UpdateProjectionAsync(int companyId, FinanceEvent financeEvent)
        {
            var result = new ProjectionUpdateResult
            {
                CompanyId = companyId,
                EventType = financeEvent.EventType,
                EventId = financeEvent.EventId
            };
            
            try
            {
                _logger.LogDebug("Updating trial balance projection for event {EventId}", financeEvent.EventId);
                
                // 🔥 Invalidate cache for this company
                await InvalidateCacheAsync(companyId);
                
                // 🔥 Update projection based on event type
                switch (financeEvent.EventType)
                {
                    case "JournalPosted":
                        result = await ProcessJournalPostedEventAsync(companyId, financeEvent);
                        break;
                    
                    case "JournalVoided":
                        result = await ProcessJournalVoidedEventAsync(companyId, financeEvent);
                        break;
                    
                    default:
                        // 🔥 Other events don't affect trial balance directly
                        result.IsSuccess = true;
                        result.Message = "Event does not affect trial balance";
                        break;
                }
                
                result.UpdatedAt = DateTime.UtcNow;
                
                _logger.LogDebug("Updated trial balance projection for event {EventId}: {Success}", 
                    financeEvent.EventId, result.IsSuccess);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update trial balance projection for event {EventId}", financeEvent.EventId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.UpdatedAt = DateTime.UtcNow;
            }
            
            return result;
        }
        
        /// <summary>
        /// Process JournalPosted event
        /// </summary>
        private async Task<ProjectionUpdateResult> ProcessJournalPostedEventAsync(int companyId, FinanceEvent financeEvent)
        {
            var result = new ProjectionUpdateResult
            {
                CompanyId = companyId,
                EventType = financeEvent.EventType,
                EventId = financeEvent.EventId
            };
            
            try
            {
                // 🔥 Get the original journal entry
                var journalEntryId = int.Parse(financeEvent.Data["JournalEntryId"].ToString());
                var journalEntry = await _context.JournalEntries
                    .Include(je => je.JournalLines)
                    .FirstOrDefaultAsync(je => je.Id == journalEntryId && je.CompanyId == companyId);
                
                if (journalEntry == null)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Journal entry not found";
                    return result;
                }
                
                // 🔥 Update projection for each account
                foreach (var line in journalEntry.JournalLines)
                {
                    await UpdateAccountProjectionAsync(companyId, line.AccountId);
                }
                
                result.IsSuccess = true;
                result.Message = $"Updated {journalEntry.JournalLines.Count} account projections";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process JournalPosted event {EventId}", financeEvent.EventId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Process JournalVoided event
        /// </summary>
        private async Task<ProjectionUpdateResult> ProcessJournalVoidedEventAsync(int companyId, FinanceEvent financeEvent)
        {
            var result = new ProjectionUpdateResult
            {
                CompanyId = companyId,
                EventType = financeEvent.EventType,
                EventId = financeEvent.EventId
            };
            
            try
            {
                // 🔥 Get the original journal entry
                var journalEntryId = int.Parse(financeEvent.Data["JournalEntryId"].ToString());
                var journalEntry = await _context.JournalEntries
                    .Include(je => je.JournalLines)
                    .FirstOrDefaultAsync(je => je.Id == journalEntryId && je.CompanyId == companyId);
                
                if (journalEntry == null)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Journal entry not found";
                    return result;
                }
                
                // 🔥 Update projection for each account (voiding reverses the effect)
                foreach (var line in journalEntry.JournalLines)
                {
                    await UpdateAccountProjectionAsync(companyId, line.AccountId);
                }
                
                result.IsSuccess = true;
                result.Message = $"Updated {journalEntry.JournalLines.Count} account projections for voided entry";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process JournalVoided event {EventId}", financeEvent.EventId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Update individual account projection
        /// </summary>
        private async Task UpdateAccountProjectionAsync(int companyId, int accountId)
        {
            try
            {
                // 🔥 Get current balance
                var balance = await _context.JournalLines
                    .Where(jl => jl.AccountId == accountId)
                    .Where(jl => jl.JournalEntry.CompanyId == companyId)
                    .Where(jl => jl.JournalEntry.Status.ToString() == "Posted")
                    .SumAsync(jl => jl.DebitAmount - jl.CreditAmount);
                
                // 🔥 Update projection
                var projection = await _context.EventProjections
                    .FirstOrDefaultAsync(ep => ep.CompanyId == companyId && 
                                              ep.ProjectionName == "TrialBalance" && 
                                              ep.AggregateId == accountId.ToString());
                
                if (projection == null)
                {
                    // 🔥 Create new projection
                    projection = new EventProjection
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = companyId,
                        ProjectionName = "TrialBalance",
                        AggregateId = accountId.ToString(),
                        Data = JsonSerializer.Serialize(new { Balance = balance }),
                        LastUpdated = DateTime.UtcNow,
                        LastEventVersion = 0,
                        IsActive = true
                    };
                    
                    await _context.EventProjections.AddAsync(projection);
                }
                else
                {
                    // 🔥 Update existing projection
                    projection.Data = JsonSerializer.Serialize(new { Balance = balance });
                    projection.LastUpdated = DateTime.UtcNow;
                    projection.LastEventVersion++;
                }
                
                await _context.SaveChangesAsync();
                
                _logger.LogDebug("Updated trial balance projection for account {AccountId}", accountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update account projection {AccountId}", accountId);
            }
        }
        
        /// <summary>
        /// Cache trial balance result
        /// </summary>
        private async Task CacheTrialBalanceAsync(string cacheKey, TrialBalanceResult result)
        {
            try
            {
                var serializedResult = JsonSerializer.Serialize(result);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes)
                };
                
                await _cache.SetStringAsync(cacheKey, serializedResult, options);
                
                _logger.LogDebug("Cached trial balance for key {CacheKey}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache trial balance for key {CacheKey}", cacheKey);
            }
        }
        
        /// <summary>
        /// Invalidate cache for company
        /// </summary>
        private async Task InvalidateCacheAsync(int companyId)
        {
            try
            {
                // 🔥 Remove all trial balance cache entries for this company
                var pattern = $"{CacheKeyPrefix}{companyId}:*";
                
                // This is a simplified approach - in production you'd use a more sophisticated cache invalidation
                // For now, we'll just remove a few common date patterns
                var today = DateTime.UtcNow;
                var dates = new[]
                {
                    today,
                    today.AddDays(-1),
                    today.AddDays(-7),
                    today.AddDays(-30),
                    new DateTime(today.Year, today.Month, 1) // First of month
                };
                
                foreach (var date in dates)
                {
                    var cacheKey = $"{CacheKeyPrefix}{companyId}:{date:yyyyMMdd}";
                    await _cache.RemoveAsync(cacheKey);
                }
                
                _logger.LogDebug("Invalidated trial balance cache for company {CompanyId}", companyId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate trial balance cache for company {CompanyId}", companyId);
            }
        }
        
        /// <summary>
        /// Get trial balance statistics
        /// </summary>
        public async Task<TrialBalanceStatistics> GetStatisticsAsync(int companyId)
        {
            var stats = new TrialBalanceStatistics
            {
                CompanyId = companyId,
                GeneratedAt = DateTime.UtcNow
            };
            
            try
            {
                // 🔥 Get account count
                var accountCount = await _context.FinanceAccounts
                    .CountAsync(a => a.CompanyId == companyId && a.IsActive);
                
                // 🔥 Get projection count
                var projectionCount = await _context.EventProjections
                    .CountAsync(ep => ep.CompanyId == companyId && ep.ProjectionName == "TrialBalance");
                
                // 🔥 Get last update time
                var lastUpdate = await _context.EventProjections
                    .Where(ep => ep.CompanyId == companyId && ep.ProjectionName == "TrialBalance")
                    .OrderByDescending(ep => ep.LastUpdated)
                    .Select(ep => ep.LastUpdated)
                    .FirstOrDefaultAsync();
                
                stats.ActiveAccountsCount = accountCount;
                stats.ProjectionCount = projectionCount;
                stats.LastProjectionUpdate = lastUpdate;
                stats.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get trial balance statistics for company {CompanyId}", companyId);
                stats.IsSuccess = false;
                stats.ErrorMessage = ex.Message;
            }
            
            return stats;
        }
    }
    
    #region Supporting Classes
    
    public class TrialBalance
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public List<TrialBalanceAccount> Accounts { get; set; } = new();
        
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal TotalEquity { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public double DurationMs { get; set; }
    }
    
    public class TrialBalanceResult
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public List<TrialBalanceAccount> Accounts { get; set; } = new();
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal TotalBalance { get; set; }
        public bool IsBalanced { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public double DurationMs { get; set; }
        public bool FromCache { get; set; }
    }
    
    public class TrialBalanceAccount
    {
        public int AccountId { get; set; }
        public int Id { get; set; } // Alias for AccountId
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public AccountType AccountType { get; set; }
        public decimal Balance { get; set; }
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
        public bool IsActive { get; set; }
        public List<TrialBalanceAccount> Accounts { get; set; } = new();
        public int? ParentAccountId { get; set; }
        public int HierarchyLevel { get; set; }
    }
    
    public class ProjectionUpdateResult
    {
        public int CompanyId { get; set; }
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
    
    public class TrialBalanceStatistics
    {
        public int CompanyId { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public int ActiveAccountsCount { get; set; }
        public int ProjectionCount { get; set; }
        public DateTime? LastProjectionUpdate { get; set; }
    }
    
    #endregion
}
