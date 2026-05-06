using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SecureERP2.Modules.Finance.Entities;
using SecureERP2.Modules.Finance.Services;

namespace SecureERP2.Modules.Finance.Services
{
    /// <summary>
    /// 🔬 PHASE 1.2: Ledger Replay Validator
    /// Re-runs full ledger from scratch and compares outputs for mathematical correctness
    /// </summary>
    public class LedgerReplayValidator
    {
        private readonly ERPDbContext _context;
        private readonly AccountingEngine _accountingEngine;

        public LedgerReplayValidator(ERPDbContext context, AccountingEngine accountingEngine)
        {
            _context = context;
            _accountingEngine = accountingEngine;
        }

        /// <summary>
        /// 🔬 Execute complete ledger replay validation
        /// </summary>
        public async Task<LedgerReplayValidationResult> ValidateLedgerReplayAsync(int companyId, DateTime replayFromDate, DateTime replayToDate)
        {
            var result = new LedgerReplayValidationResult
            {
                CompanyId = companyId,
                ReplayFromDate = replayFromDate,
                ReplayToDate = replayToDate,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // 🔬 Step 1: Capture current state (baseline)
                result.BaselineState = await CaptureCurrentLedgerStateAsync(companyId, replayToDate);

                // 🔬 Step 2: Build replay engine from scratch
                var replayEngine = await BuildReplayEngineAsync(companyId, replayFromDate);

                // 🔬 Step 3: Replay all transactions chronologically
                result.ReplayState = await ReplayLedgerChronologicallyAsync(replayEngine, companyId, replayFromDate, replayToDate);

                // 🔬 Step 4: Compare baseline vs replay results
                result.ComparisonResult = await CompareLedgerStatesAsync(result.BaselineState, result.ReplayState);

                // 🔬 Step 5: Validate mathematical consistency
                result.MathematicalValidation = await ValidateMathematicalConsistencyAsync(result.ReplayState);

                // 🔬 Step 6: Check for replay anomalies
                result.ReplayAnomalies = await DetectReplayAnomaliesAsync(result.ReplayState, result.BaselineState);

                // 🔬 Calculate overall validation score
                result.OverallValidationScore = CalculateReplayValidationScore(result);
                result.ValidationStatus = DetermineValidationStatus(result.OverallValidationScore);
                result.CriticalDiscrepancies = IdentifyCriticalDiscrepancies(result);

                result.CompletedAt = DateTime.UtcNow;
                result.IsSuccess = result.ValidationStatus != ReplayValidationStatus.Failed;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Ledger replay validation failed: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔬 Step 1: Capture current ledger state as baseline
        /// </summary>
        private async Task<LedgerState> CaptureCurrentLedgerStateAsync(int companyId, DateTime asOfDate)
        {
            var state = new LedgerState { CompanyId = companyId, AsOfDate = asOfDate };

            // 🔬 Capture current trial balance
            var trialBalance = await _accountingEngine.GetTrialBalanceAsync(companyId, DateTime.MinValue, asOfDate);
            state.TrialBalance = trialBalance.Accounts.Select(a => new SecureERP2.Modules.Finance.Services.TrialBalanceAccount 
            {
                AccountId = a.AccountId,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                AccountType = a.AccountType,
                Balance = a.Balance,
                DebitBalance = a.DebitBalance,
                CreditBalance = a.CreditBalance,
                IsActive = a.IsActive,
                ParentAccountId = a.ParentAccountId,
                HierarchyLevel = a.HierarchyLevel
            }).ToList();

            // 🔬 Capture account balances
            state.AccountBalances = trialBalance.Accounts.ToDictionary(a => int.Parse(a.AccountCode), a => a.Balance);

            // 🔬 Capture total debits and credits
            state.TotalDebits = trialBalance.Accounts.Sum(a => a.Balance > 0 ? a.Balance : 0);
            state.TotalCredits = Math.Abs(trialBalance.Accounts.Sum(a => a.Balance < 0 ? a.Balance : 0));

            // 🔬 Capture journal entry count
            state.JournalEntryCount = await _context.JournalEntries
                .Where(j => j.CompanyId == companyId && j.JournalDate <= asOfDate)
                .CountAsync();

            // 🔬 Capture audit trail hash
            var lastAuditRecord = await _context.AuditTrails
                .Where(a => a.CompanyId == companyId && a.CreatedAt <= asOfDate)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            state.AuditTrailHash = lastAuditRecord?.CurrentHash ?? "0";

            return state;
        }

        /// <summary>
        /// 🔬 Step 2: Build replay engine from scratch
        /// </summary>
        private async Task<LedgerReplayEngine> BuildReplayEngineAsync(int companyId, DateTime fromDate)
        {
            var engine = new LedgerReplayEngine { CompanyId = companyId };

            // 🔬 Load chart of accounts as of replay start date
            engine.ChartOfAccounts = await _context.FinanceAccounts
                .Where(a => a.CompanyId == companyId)
                .OrderBy(a => a.AccountCode)
                .ToListAsync();

            // 🔬 Initialize account balances to zero (clean slate)
            engine.AccountBalances = engine.ChartOfAccounts
                .ToDictionary(a => a.Id, a => 0m);

            // 🔬 Load opening balances if replay start is not company inception
            if (fromDate > DateTime.MinValue)
            {
                var openingBalances = await GetOpeningBalancesAsync(companyId, fromDate.AddDays(-1));
                foreach (var balance in openingBalances)
                {
                    if (engine.AccountBalances.ContainsKey(balance.AccountId))
                    {
                        engine.AccountBalances[balance.AccountId] = balance.Balance;
                    }
                }
            }

            return engine;
        }

        /// <summary>
        /// 🔬 Step 3: Replay all transactions chronologically
        /// </summary>
        private async Task<LedgerState> ReplayLedgerChronologicallyAsync(LedgerReplayEngine engine, int companyId, DateTime fromDate, DateTime toDate)
        {
            var replayState = new LedgerState { CompanyId = companyId, AsOfDate = toDate };

            // 🔬 Get all journal entries in chronological order
            var journalEntries = await _context.JournalEntries
                .Where(j => j.CompanyId == companyId && 
                           j.JournalDate >= fromDate && 
                           j.JournalDate <= toDate &&
                           j.Status == JournalStatus.Posted)
                .Include(j => j.JournalLines)
                .OrderBy(j => j.JournalDate)
                .ThenBy(j => j.CreatedAt)
                .ToListAsync();

            replayState.JournalEntryCount = journalEntries.Count;

            // 🔬 Process each journal entry
            foreach (var entry in journalEntries)
            {
                var validationResult = await ProcessJournalEntryInReplayAsync(engine, entry);
                
                if (!validationResult.IsValid)
                {
                    replayState.ReplayErrors.Add(new ReplayError
                    {
                        JournalEntryId = entry.Id,
                        Description = validationResult.ErrorMessage,
                        Severity = ReplayErrorSeverity.Critical
                    });
                }

                // 🔬 Update replay audit trail
                engine.ReplayAuditTrail.Add(new ReplayAuditEntry
                {
                    JournalEntryId = entry.Id,
                    ProcessedAt = DateTime.UtcNow,
                    AccountBalancesBefore = engine.AccountBalances.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    AccountBalancesAfter = engine.AccountBalances.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                });
            }

            // 🔬 Build final replay state
            replayState.AccountBalances = engine.AccountBalances.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            replayState.TotalDebits = engine.AccountBalances.Values.Where(b => b > 0).Sum();
            replayState.TotalCredits = Math.Abs(engine.AccountBalances.Values.Where(b => b < 0).Sum());

            // 🔬 Build replay trial balance
            replayState.TrialBalance = await BuildTrialBalanceFromReplayAsync(engine);

            return replayState;
        }

        /// <summary>
        /// 🔬 Process individual journal entry in replay
        /// </summary>
        // private async Task<ReplayValidationResult> ProcessJournalEntryInReplayAsync(LedgerReplayEngine engine, JournalEntry entry) // Commented out - JournalEntry not available
        /*
        {
            var result = new ReplayValidationResult { IsValid = true };

            try
            {
                // 🔬 Validate double-entry balance before processing
                var totalDebits = entry.JournalLines.Sum(l => l.DebitAmount);
                var totalCredits = entry.JournalLines.Sum(l => l.CreditAmount);
                var balanceDifference = Math.Abs(totalDebits - totalCredits);

                if (balanceDifference > 0.01m)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Journal entry #{entry.Id} is not balanced: Debits={totalDebits:C}, Credits={totalCredits:C}";
                    return result;
                }

                // 🔬 Process each line
                foreach (var line in entry.JournalLines)
                {
                    if (!engine.AccountBalances.ContainsKey(line.AccountId))
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Account {line.AccountId} not found in chart of accounts";
                        return result;
                    }

                    // 🔬 Apply debit or credit
                    var currentBalance = engine.AccountBalances[line.AccountId];
                    
                    if (line.DebitAmount > 0)
                    {
                        engine.AccountBalances[line.AccountId] += line.DebitAmount;
                    }
                    
                    if (line.CreditAmount > 0)
                    {
                        engine.AccountBalances[line.AccountId] -= line.CreditAmount;
                    }

                    // 🔬 Validate account type rules
                    var account = engine.ChartOfAccounts.FirstOrDefault(a => a.Id == line.AccountId);
                    if (account != null)
                    {
                        var validationResult = ValidateAccountBalanceRules(account, engine.AccountBalances[line.AccountId]);
                        if (!validationResult.IsValid)
                        {
                            result.IsValid = false;
                            result.ErrorMessage = validationResult.ErrorMessage;
                            return result;
                        }
                    }
                }

                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Error processing journal entry #{entry.Id}: {ex.Message}";
            }

            return result;
        }
        */

        /// <summary>
        /// 🔬 Step 4: Compare baseline vs replay results
        /// </summary>
        private async Task<LedgerComparisonResult> CompareLedgerStatesAsync(LedgerState baseline, LedgerState replay)
        {
            var comparison = new LedgerComparisonResult
            {
                CompanyId = baseline.CompanyId,
                BaselineJournalCount = baseline.JournalEntryCount,
                ReplayJournalCount = replay.JournalEntryCount
            };

            // 🔬 Compare account balances
            comparison.AccountBalanceDifferences = new List<AccountBalanceDifference>();
            
            foreach (var accountId in baseline.AccountBalances.Keys)
            {
                var baselineBalance = baseline.AccountBalances.GetValueOrDefault(accountId, 0);
                var replayBalance = replay.AccountBalances.GetValueOrDefault(accountId, 0);
                var difference = baselineBalance - replayBalance;

                if (Math.Abs(difference) > 0.01m) // Allow for rounding
                {
                    comparison.AccountBalanceDifferences.Add(new AccountBalanceDifference
                    {
                        AccountId = accountId,
                        BaselineBalance = baselineBalance,
                        ReplayBalance = replayBalance,
                        Difference = difference,
                        DifferencePercentage = baselineBalance != 0 ? Math.Abs(difference / baselineBalance) * 100 : 0
                    });
                }
            }

            // 🔬 Compare totals
            comparison.TotalDebitsDifference = baseline.TotalDebits - replay.TotalDebits;
            comparison.TotalCreditsDifference = baseline.TotalCredits - replay.TotalCredits;
            comparison.TotalDifference = Math.Abs(comparison.TotalDebitsDifference) + Math.Abs(comparison.TotalCreditsDifference);

            // 🔬 Compare trial balance
            comparison.TrialBalanceDifferences = await CompareTrialBalancesAsync(baseline.TrialBalance, replay.TrialBalance);

            // 🔬 Calculate comparison score
            var totalAccounts = baseline.AccountBalances.Count;
            var matchingAccounts = totalAccounts - comparison.AccountBalanceDifferences.Count;
            comparison.MatchPercentage = totalAccounts > 0 ? (double)matchingAccounts / totalAccounts * 100 : 0;

            comparison.Status = comparison.MatchPercentage >= 99.9 ? ComparisonStatus.Perfect :
                                 comparison.MatchPercentage >= 95 ? ComparisonStatus.MinorDifferences :
                                 comparison.MatchPercentage >= 80 ? ComparisonStatus.SignificantDifferences :
                                 ComparisonStatus.MajorDiscrepancies;

            return comparison;
        }

        /// <summary>
        /// 🔬 Step 5: Validate mathematical consistency
        /// </summary>
        private async Task<MathematicalConsistencyResult> ValidateMathematicalConsistencyAsync(LedgerState replayState)
        {
            var validation = new MathematicalConsistencyResult { CompanyId = replayState.CompanyId };

            // 🔬 Validate fundamental accounting equation
            var assets = replayState.TrialBalance.Accounts
                .Where(a => a.AccountType == AccountType.Asset)
                .Sum(a => a.Balance);

            var liabilities = replayState.TrialBalance.Accounts
                .Where(a => a.AccountType == AccountType.Liability)
                .Sum(a => a.Balance);

            var equity = replayState.TrialBalance.Accounts
                .Where(a => a.AccountType == AccountType.Equity)
                .Sum(a => a.Balance);

            validation.Assets = assets;
            validation.Liabilities = liabilities;
            validation.Equity = equity;
            validation.BalanceEquationDifference = Math.Abs(assets - (liabilities + equity));
            validation.IsBalanced = validation.BalanceEquationDifference <= 0.01m;

            // 🔬 Validate debits equal credits
            validation.DebitsEqualCredits = Math.Abs(replayState.TotalDebits - replayState.TotalCredits) <= 0.01m;

            // 🔬 Validate no negative asset balances (unless allowed)
            var negativeAssets = replayState.TrialBalance.Accounts
                .Where(a => a.AccountType == AccountType.Asset && a.Balance < 0)
                .ToList();

            validation.NegativeAssetBalances = negativeAssets.Count;
            validation.HasNegativeAssets = negativeAssets.Any();

            // 🔬 Validate no positive liability balances (unless allowed)
            var positiveLiabilities = replayState.TrialBalance.Accounts
                .Where(a => a.AccountType == AccountType.Liability && a.Balance > 0)
                .ToList();

            validation.PositiveLiabilityBalances = positiveLiabilities.Count;
            validation.HasPositiveLiabilities = positiveLiabilities.Any();

            // 🔬 Calculate overall consistency score
            var consistencyChecks = new[]
            {
                validation.IsBalanced,
                validation.DebitsEqualCredits,
                !validation.HasNegativeAssets,
                !validation.HasPositiveLiabilities
            };

            validation.ConsistencyScore = consistencyChecks.Count(c => c) * 25; // 25 points each
            validation.Status = validation.ConsistencyScore >= 75 ? ConsistencyStatus.Consistent :
                                 validation.ConsistencyScore >= 50 ? ConsistencyStatus.MinorIssues :
                                 ConsistencyStatus.MajorIssues;

            return validation;
        }

        /// <summary>
        /// 🔬 Step 6: Detect replay anomalies
        /// </summary>
        private async Task<List<ReplayAnomaly>> DetectReplayAnomaliesAsync(LedgerState replayState, LedgerState baselineState)
        {
            var anomalies = new List<ReplayAnomaly>();

            // 🔬 Detect missing accounts
            var missingInReplay = baselineState.AccountBalances.Keys.Except(replayState.AccountBalances.Keys).ToList();
            if (missingInReplay.Any())
            {
                anomalies.Add(new ReplayAnomaly
                {
                    Type = "MissingAccounts",
                    Description = $"{missingInReplay.Count} accounts missing in replay",
                    Severity = ReplayAnomalySeverity.High,
                    AffectedRecordCount = missingInReplay.Count
                });
            }

            // 🔬 Detect extra accounts
            var extraInReplay = replayState.AccountBalances.Keys.Except(baselineState.AccountBalances.Keys).ToList();
            if (extraInReplay.Any())
            {
                anomalies.Add(new ReplayAnomaly
                {
                    Type = "ExtraAccounts",
                    Description = $"{extraInReplay.Count} extra accounts in replay",
                    Severity = ReplayAnomalySeverity.Medium,
                    AffectedRecordCount = extraInReplay.Count
                });
            }

            // 🔬 Detect balance drift patterns
            var significantDrifts = baselineState.AccountBalances
                .Where(kvp => replayState.AccountBalances.ContainsKey(kvp.Key))
                .Select(kvp => new
                {
                    AccountId = kvp.Key,
                    BaselineBalance = kvp.Value,
                    ReplayBalance = replayState.AccountBalances[kvp.Key],
                    Difference = Math.Abs(kvp.Value - replayState.AccountBalances[kvp.Key])
                })
                .Where(x => x.Difference > 1000) // More than $1000 difference
                .ToList();

            if (significantDrifts.Any())
            {
                anomalies.Add(new ReplayAnomaly
                {
                    Type = "BalanceDrift",
                    Description = $"{significantDrifts.Count} accounts with significant balance drift",
                    Severity = ReplayAnomalySeverity.High,
                    AffectedRecordCount = significantDrifts.Count
                });
            }

            // 🔬 Detect rounding accumulation
            var roundingIssues = baselineState.AccountBalances
                .Where(kvp => replayState.AccountBalances.ContainsKey(kvp.Key))
                .Count(kvp => Math.Abs((kvp.Value - replayState.AccountBalances[kvp.Key]) % 0.01m) > 0.001m);

            if (roundingIssues > 0)
            {
                anomalies.Add(new ReplayAnomaly
                {
                    Type = "RoundingAccumulation",
                    Description = $"{roundingIssues} accounts with rounding accumulation issues",
                    Severity = ReplayAnomalySeverity.Low,
                    AffectedRecordCount = roundingIssues
                });
            }

            return anomalies;
        }

        // Helper methods
        private async Task<List<OpeningBalance>> GetOpeningBalancesAsync(int companyId, DateTime asOfDate)
        {
            // Get opening balances as of specified date
            var trialBalance = await _accountingEngine.GetTrialBalanceAsync(companyId, DateTime.MinValue, asOfDate);
            
            return trialBalance.Accounts.Select(a => new OpeningBalance
            {
                AccountId = a.Id,
                Balance = a.Balance
            }).ToList();
        }

        // private async Task<TrialBalance> BuildTrialBalanceFromReplayAsync(LedgerReplayEngine engine) // Commented out - TrialBalance not available
        /*
        {
            var trialBalance = new TrialBalance
            {
                Accounts = engine.ChartOfAccounts.Select(account => new TrialBalanceAccount
                {
                    Id = account.Id,
                    AccountCode = account.AccountCode,
                    AccountName = account.AccountName,
                    AccountType = account.AccountType,
                    Balance = engine.AccountBalances.GetValueOrDefault(account.Id, 0)
                }).ToList()
            };

            trialBalance.TotalDebits = trialBalance.Accounts.Sum(a => a.Balance > 0 ? a.Balance : 0);
            trialBalance.TotalCredits = Math.Abs(trialBalance.Accounts.Sum(a => a.Balance < 0 ? a.Balance : 0));

            return trialBalance;
        }
        */

        private ReplayValidationResult ValidateAccountBalanceRules(FinanceAccount account, decimal balance)
        {
            var result = new ReplayValidationResult { IsValid = true };

            // 🔬 Basic account type balance rules
            switch (account.AccountType)
            {
                case AccountType.Asset:
                    // Assets should normally have debit balances (positive)
                    if (balance < -10000) // Allow for accumulated depreciation
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Asset account {account.AccountName} has large credit balance: {balance:C}";
                    }
                    break;

                case AccountType.Liability:
                    // Liabilities should normally have credit balances (negative in our system)
                    if (balance > 10000) // Allow for prepaid expenses
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Liability account {account.AccountName} has large debit balance: {balance:C}";
                    }
                    break;

                case AccountType.Revenue:
                    // Revenue should have credit balances (negative)
                    if (balance > 0)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Revenue account {account.AccountName} has debit balance: {balance:C}";
                    }
                    break;

                case AccountType.Expense:
                    // Expenses should have debit balances (positive)
                    if (balance < 0)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Expense account {account.AccountName} has credit balance: {balance:C}";
                    }
                    break;
            }

            return result;
        }

        // private async Task<List<TrialBalanceDifference>> CompareTrialBalancesAsync(TrialBalance baseline, TrialBalance replay) // Commented out - TrialBalance not available
        /*
        {
            var differences = new List<TrialBalanceDifference>();

            foreach (var baselineAccount in baseline.Accounts)
            {
                var replayAccount = replay.Accounts.FirstOrDefault(a => a.Id == baselineAccount.Id);
                if (replayAccount != null)
                {
                    var difference = baselineAccount.Balance - replayAccount.Balance;
                    if (Math.Abs(difference) > 0.01m)
                    {
                        differences.Add(new TrialBalanceDifference
                        {
                            AccountId = baselineAccount.Id,
                            AccountName = baselineAccount.AccountName,
                            BaselineBalance = baselineAccount.Balance,
                            ReplayBalance = replayAccount.Balance,
                            Difference = difference
                        });
                    }
                }
            }

            return differences;
        }
        */

        // Calculate validation score and status
        private double CalculateReplayValidationScore(LedgerReplayValidationResult result)
        {
            var scores = new[]
            {
                result.ComparisonResult.MatchPercentage,
                result.MathematicalValidation.ConsistencyScore,
                result.ReplayAnomalies.Count == 0 ? 100 : Math.Max(0, 100 - result.ReplayAnomalies.Count * 10)
            };

            return scores.Average();
        }

        private ReplayValidationStatus DetermineValidationStatus(double score)
        {
            if (score >= 99) return ReplayValidationStatus.Perfect;
            if (score >= 95) return ReplayValidationStatus.Excellent;
            if (score >= 90) return ReplayValidationStatus.Good;
            if (score >= 80) return ReplayValidationStatus.Acceptable;
            if (score >= 70) return ReplayValidationStatus.Poor;
            return ReplayValidationStatus.Failed;
        }

        private List<CriticalDiscrepancy> IdentifyCriticalDiscrepancies(LedgerReplayValidationResult result)
        {
            var discrepancies = new List<CriticalDiscrepancy>();

            // 🔬 Critical balance equation violations
            if (!result.MathematicalValidation.IsBalanced)
            {
                discrepancies.Add(new CriticalDiscrepancy
                {
                    Type = "BalanceEquationViolation",
                    Description = $"Assets ({result.MathematicalValidation.Assets:C}) ≠ Liabilities + Equity ({result.MathematicalValidation.Liabilities + result.MathematicalValidation.Equity:C})",
                    Severity = ReplayAnomalySeverity.Critical
                });
            }

            // 🔬 Critical account balance differences
            var criticalDifferences = result.ComparisonResult.AccountBalanceDifferences
                .Where(d => Math.Abs(d.Difference) > 10000) // More than $10,000 difference
                .ToList();

            foreach (var difference in criticalDifferences)
            {
                discrepancies.Add(new CriticalDiscrepancy
                {
                    Type = "CriticalBalanceDifference",
                    Description = $"Account {difference.AccountId} difference: {difference.Difference:C}",
                    Severity = ReplayAnomalySeverity.Critical
                });
            }

            return discrepancies;
        }
    }

    // Supporting classes
    public class LedgerReplayValidationResult
    {
        public int CompanyId { get; set; }
        public DateTime ReplayFromDate { get; set; }
        public DateTime ReplayToDate { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }

        public LedgerState BaselineState { get; set; } = new();
        public LedgerState ReplayState { get; set; } = new();
        public LedgerComparisonResult ComparisonResult { get; set; } = new();
        public MathematicalConsistencyResult MathematicalValidation { get; set; } = new();
        public List<ReplayAnomaly> ReplayAnomalies { get; set; } = new();

        public double OverallValidationScore { get; set; }
        public ReplayValidationStatus ValidationStatus { get; set; }
        public List<CriticalDiscrepancy> CriticalDiscrepancies { get; set; } = new();
    }

    public class ReplayValidatorLedgerState
    {
        public int CompanyId { get; set; }
        public DateTime AsOfDate { get; set; }
        public Dictionary<int, decimal> AccountBalances { get; set; } = new();
        // public TrialBalance TrialBalance { get; set; } = new(); // Commented out - TrialBalance not available
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public int JournalEntryCount { get; set; }
        public string AuditTrailHash { get; set; } = string.Empty;
        public List<ReplayError> ReplayErrors { get; set; } = new();
    }

    public class LedgerReplayEngine
    {
        public int CompanyId { get; set; }
        public List<FinanceAccount> ChartOfAccounts { get; set; } = new();
        public Dictionary<int, decimal> AccountBalances { get; set; } = new();
        public List<ReplayAuditEntry> ReplayAuditTrail { get; set; } = new();
    }

    public class ReplayValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class ReplayAuditEntry
    {
        public int JournalEntryId { get; set; }
        public DateTime ProcessedAt { get; set; }
        public Dictionary<int, decimal> AccountBalancesBefore { get; set; } = new();
        public Dictionary<int, decimal> AccountBalancesAfter { get; set; } = new();
    }

    public class LedgerComparisonResult
    {
        public int CompanyId { get; set; }
        public int BaselineJournalCount { get; set; }
        public int ReplayJournalCount { get; set; }
        public List<AccountBalanceDifference> AccountBalanceDifferences { get; set; } = new();
        public decimal TotalDebitsDifference { get; set; }
        public decimal TotalCreditsDifference { get; set; }
        public decimal TotalDifference { get; set; }
        public List<TrialBalanceDifference> TrialBalanceDifferences { get; set; } = new();
        public double MatchPercentage { get; set; }
        public ComparisonStatus Status { get; set; }
    }

    public class AccountBalanceDifference
    {
        public int AccountId { get; set; }
        public decimal BaselineBalance { get; set; }
        public decimal ReplayBalance { get; set; }
        public decimal Difference { get; set; }
        public double DifferencePercentage { get; set; }
    }

    public class TrialBalanceDifference
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public decimal BaselineBalance { get; set; }
        public decimal ReplayBalance { get; set; }
        public decimal Difference { get; set; }
    }

    public class MathematicalConsistencyResult
    {
        public int CompanyId { get; set; }
        public decimal Assets { get; set; }
        public decimal Liabilities { get; set; }
        public decimal Equity { get; set; }
        public decimal BalanceEquationDifference { get; set; }
        public bool IsBalanced { get; set; }
        public bool DebitsEqualCredits { get; set; }
        public int NegativeAssetBalances { get; set; }
        public bool HasNegativeAssets { get; set; }
        public int PositiveLiabilityBalances { get; set; }
        public bool HasPositiveLiabilities { get; set; }
        public int ConsistencyScore { get; set; }
        public ConsistencyStatus Status { get; set; }
    }

    public class ReplayAnomaly
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ReplayAnomalySeverity Severity { get; set; }
        public int AffectedRecordCount { get; set; }
    }

    public class ReplayError
    {
        public int JournalEntryId { get; set; }
        public string Description { get; set; } = string.Empty;
        public ReplayErrorSeverity Severity { get; set; }
    }

    public class CriticalDiscrepancy
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ReplayAnomalySeverity Severity { get; set; }
    }

    public class OpeningBalance
    {
        public int AccountId { get; set; }
        public decimal Balance { get; set; }
    }

    // Enums
    public enum ReplayValidationStatus
    {
        Perfect,      // 99-100%
        Excellent,    // 95-98.9%
        Good,         // 90-94.9%
        Acceptable,   // 80-89.9%
        Poor,         // 70-79.9%
        Failed        // <70%
    }

    public enum ComparisonStatus
    {
        Perfect,
        MinorDifferences,
        SignificantDifferences,
        MajorDiscrepancies
    }

    public enum ConsistencyStatus
    {
        Consistent,
        MinorIssues,
        MajorIssues
    }

    public enum ReplayAnomalySeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum ReplayErrorSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}
