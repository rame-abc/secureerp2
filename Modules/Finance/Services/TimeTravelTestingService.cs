using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SecureERP2.Modules.Finance.Entities;

namespace SecureERP2.Modules.Finance.Services
{
    /// <summary>
    /// 🔒 REAL PRODUCTION HARDENING - Time Travel Testing
    /// MOST IMPORTANT FOR FINANCE SYSTEMS - Tests temporal data integrity
    /// </summary>
    public class TimeTravelTestingService
    {
        private readonly ERPDbContext _context;
        private readonly AccountingEngine _accountingEngine;

        public TimeTravelTestingService(ERPDbContext context, AccountingEngine accountingEngine)
        {
            _context = context;
            _accountingEngine = accountingEngine;
        }

        /// <summary>
        /// 🔒 Run comprehensive time travel tests
        /// </summary>
        public async Task<TimeTravelTestResult> RunTimeTravelTestsAsync(int companyId)
        {
            var result = new TimeTravelTestResult
            {
                CompanyId = companyId,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // 🔒 Test 1: Backdated invoices
                result.BackdatedInvoiceTest = await TestBackdatedInvoicesAsync(companyId);

                // 🔒 Test 2: Future-dated journal entries
                result.FutureDatedJournalTest = await TestFutureDatedJournalsAsync(companyId);

                // 🔒 Test 3: Period reopening attempts
                result.PeriodReopeningTest = await TestPeriodReopeningAsync(companyId);

                // 🔒 Test 4: Cross-period adjustments
                result.CrossPeriodAdjustmentTest = await TestCrossPeriodAdjustmentsAsync(companyId);

                // 🔒 Test 5: Temporal data consistency
                result.TemporalConsistencyTest = await TestTemporalConsistencyAsync(companyId);

                // 🔒 Test 6: Audit trail temporal integrity
                result.AuditTrailTemporalTest = await TestAuditTrailTemporalIntegrityAsync(companyId);

                // 🔒 Calculate overall test status
                result.OverallStatus = CalculateOverallTestStatus(result);
                result.TotalTestsRun = 6;
                result.TestsPassed = result.TestsPassed = CountPassedTests(result);
                result.CriticalIssues = IdentifyCriticalIssues(result);

                result.CompletedAt = DateTime.UtcNow;
                result.IsSuccess = result.OverallStatus == TimeTravelTestStatus.Passed;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Time travel testing failed: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔒 Test 1: Backdated invoices
        /// </summary>
        private async Task<BackdatedInvoiceTestResult> TestBackdatedInvoicesAsync(int companyId)
        {
            var result = new BackdatedInvoiceTestResult { CompanyId = companyId };

            try
            {
                var currentDate = DateTime.UtcNow;
                var thirtyDaysAgo = currentDate.AddDays(-30);

                // 🔒 Find backdated invoices
                var backdatedInvoices = await _context.Invoices
                    .Where(i => i.CompanyId == companyId && 
                               i.InvoiceDate < thirtyDaysAgo &&
                               i.CreatedAt > thirtyDaysAgo) // Created recently but dated in the past
                    .ToListAsync();

                result.BackdatedInvoiceCount = backdatedInvoices.Count;

                // 🔒 Check if backdated invoices caused period integrity issues
                var periodIssues = new List<TimeTravelPeriodIntegrityIssue>();
                foreach (var invoice in backdatedInvoices)
                {
                    // Check if invoice date falls in a closed period
                    var closedPeriod = await _context.PeriodClosings
                        .FirstOrDefaultAsync(p => p.CompanyId == invoice.CompanyId && 
                                              p.Status == PeriodStatus.Locked &&
                                              invoice.InvoiceDate < p.ClosingDate);

                    if (closedPeriod != null)
                    {
                        periodIssues.Add(new TimeTravelPeriodIntegrityIssue
                        {
                            InvoiceId = invoice.Id,
                            InvoiceDate = invoice.InvoiceDate,
                            ClosedPeriod = closedPeriod.ClosingDate,
                            IssueType = "BackdatedInvoiceInClosedPeriod",
                            Severity = TemporalIssueSeverity.Critical,
                            Description = $"Invoice {invoice.InvoiceNumber} dated {invoice.InvoiceDate:yyyy-MM-dd} posted in closed period ending {closedPeriod.ClosingDate:yyyy-MM-dd}"
                        });
                    }

                    // Check for revenue recognition timing issues
                    var revenueDifference = await CalculateRevenueDifferenceAsync(invoice);
                    if (Math.Abs(revenueDifference - invoice.TotalAmount) > 0.01m)
                    {
                        periodIssues.Add(new TimeTravelPeriodIntegrityIssue
                        {
                            InvoiceId = invoice.Id,
                            InvoiceDate = invoice.InvoiceDate,
                            ClosedPeriod = null,
                            IssueType = "RevenueRecognitionTimingMismatch",
                            Severity = TemporalIssueSeverity.Medium,
                            Description = $"Revenue recognition timing mismatch for invoice {invoice.InvoiceNumber}"
                        });
                    }
                }

                result.PeriodIntegrityIssues = periodIssues;
                result.Status = periodIssues.Any(issue => issue.Severity == TemporalIssueSeverity.Critical) ?
                    TimeTravelTestStatus.Failed : 
                    periodIssues.Any() ? TimeTravelTestStatus.Warning : TimeTravelTestStatus.Passed;

                result.Message = result.Status == TimeTravelTestStatus.Passed ?
                    "No critical backdated invoice issues found" :
                    $"Found {periodIssues.Count} backdated invoice issues";
            }
            catch (Exception ex)
            {
                result.Status = TimeTravelTestStatus.Error;
                result.ErrorMessage = $"Backdated invoice test error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔒 Test 2: Future-dated journal entries
        /// </summary>
        private async Task<FutureDatedJournalTestResult> TestFutureDatedJournalsAsync(int companyId)
        {
            var result = new FutureDatedJournalTestResult { CompanyId = companyId };

            try
            {
                var currentDate = DateTime.UtcNow;

                // 🔒 Find future-dated transactions
                var futureTransactions = await _context.Transactions
                    .Where(t => t.CompanyId == companyId && 
                               t.TransactionDate > currentDate)
                    .Include(t => t.LedgerEntries)
                    .ToListAsync();

                result.FutureDatedJournalCount = futureTransactions.Count;

                // 🔒 Analyze impact of future-dated entries
                var futureDateIssues = new List<FutureDateIssue>();
                foreach (var transaction in futureTransactions)
                {
                    // Check if future entry affects current period reporting
                    var currentPeriodEnd = new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(1).AddDays(-1);
                    
                    if (transaction.TransactionDate <= currentPeriodEnd.AddMonths(1))
                    {
                        futureDateIssues.Add(new FutureDateIssue
                        {
                            JournalId = transaction.Id,
                            JournalDate = transaction.TransactionDate,
                            IssueType = "FutureEntryInCurrentPeriod",
                            Severity = TemporalIssueSeverity.Medium,
                            Description = $"Transaction dated {transaction.TransactionDate:yyyy-MM-dd} affects current period reporting"
                        });
                    }

                    // Check if future entry creates period overlap
                    var trialBalanceAtFutureDate = await _accountingEngine.GetTrialBalanceAsync(companyId, DateTime.MinValue, transaction.TransactionDate);
                    var trialBalanceCurrent = await _accountingEngine.GetTrialBalanceAsync(companyId, DateTime.MinValue, currentDate);

                    var futureImpact = trialBalanceAtFutureDate.Accounts.Sum(a => Math.Abs(a.Balance)) - 
                                     trialBalanceCurrent.Accounts.Sum(a => Math.Abs(a.Balance));

                    if (Math.Abs(futureImpact) > 10000) // Large future impact
                    {
                        futureDateIssues.Add(new FutureDateIssue
                        {
                            JournalId = transaction.Id,
                            JournalDate = transaction.TransactionDate,
                            IssueType = "LargeFutureImpact",
                            Severity = TemporalIssueSeverity.High,
                            Description = $"Future entry has large impact: {futureImpact:C}"
                        });
                    }
                }

                result.FutureDateIssues = futureDateIssues;
                result.Status = futureDateIssues.Any(issue => issue.Severity == TemporalIssueSeverity.High) ?
                    TimeTravelTestStatus.Failed : 
                    futureDateIssues.Any() ? TimeTravelTestStatus.Warning : TimeTravelTestStatus.Passed;

                result.Message = result.Status == TimeTravelTestStatus.Passed ?
                    "No critical future-dated journal issues found" :
                    $"Found {futureDateIssues.Count} future-dated journal issues";
            }
            catch (Exception ex)
            {
                result.Status = TimeTravelTestStatus.Error;
                result.ErrorMessage = $"Future-dated journal test error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔒 Test 3: Period reopening attempts
        /// </summary>
        private async Task<PeriodReopeningTestResult> TestPeriodReopeningAsync(int companyId)
        {
            var result = new PeriodReopeningTestResult { CompanyId = companyId };

            try
            {
                // 🔒 Get locked periods
                var lockedPeriods = await _context.PeriodClosings
                    .Where(pc => pc.CompanyId == companyId && pc.IsLocked)
                    .OrderByDescending(pc => pc.ClosingDate)
                    .ToListAsync();

                result.LockedPeriodCount = lockedPeriods.Count;

                var reopeningAttempts = new List<ReopeningAttempt>();

                // 🔒 Test if locked periods can be modified
                foreach (var period in lockedPeriods)
                {
                    // Check for any transactions created after period lock but dated within the period
                    var suspiciousEntries = await _context.Transactions
                        .Where(t => t.CompanyId == companyId && 
                                   t.TransactionDate <= period.ClosingDate &&
                                   t.CreatedAt > period.ClosedAt)
                        .ToListAsync();

                    if (suspiciousEntries.Any())
                    {
                        reopeningAttempts.Add(new ReopeningAttempt
                        {
                            PeriodClosingId = period.Id,
                            ClosingDate = period.ClosingDate,
                            LockedAt = period.ClosedAt ?? DateTime.UtcNow,
                            AttemptType = "PostLockEntryInPeriod",
                            SuspiciousEntryCount = suspiciousEntries.Count,
                            Severity = TemporalIssueSeverity.Critical,
                            Description = $"{suspiciousEntries.Count} entries created after lock but dated within locked period"
                        });
                    }

                    // Check for modifications to locked period data
                    var modifiedEntries = await _context.Transactions
                        .Where(t => t.CompanyId == companyId && 
                                   t.TransactionDate <= period.ClosingDate &&
                                   t.UpdatedAt > period.ClosedAt)
                        .ToListAsync();

                    if (modifiedEntries.Any())
                    {
                        reopeningAttempts.Add(new ReopeningAttempt
                        {
                            PeriodClosingId = period.Id,
                            ClosingDate = period.ClosingDate,
                            LockedAt = period.ClosedAt ?? DateTime.UtcNow,
                            AttemptType = "ModifiedLockedPeriodEntry",
                            SuspiciousEntryCount = modifiedEntries.Count,
                            Severity = TemporalIssueSeverity.Critical,
                            Description = $"{modifiedEntries.Count} entries modified after period lock"
                        });
                    }
                }

                result.ReopeningAttempts = reopeningAttempts;
                result.Status = reopeningAttempts.Any(attempt => attempt.Severity == TemporalIssueSeverity.Critical) ?
                    TimeTravelTestStatus.Failed : 
                    reopeningAttempts.Any() ? TimeTravelTestStatus.Warning : TimeTravelTestStatus.Passed;

                result.Message = result.Status == TimeTravelTestStatus.Passed ?
                    "No period reopening attempts detected" :
                    $"Found {reopeningAttempts.Count} potential reopening attempts";
            }
            catch (Exception ex)
            {
                result.Status = TimeTravelTestStatus.Error;
                result.ErrorMessage = $"Period reopening test error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔒 Test 4: Cross-period adjustments
        /// </summary>
        private async Task<CrossPeriodAdjustmentTestResult> TestCrossPeriodAdjustmentsAsync(int companyId)
        {
            var result = new CrossPeriodAdjustmentTestResult { CompanyId = companyId };

            try
            {
                var currentDate = DateTime.UtcNow;
                var currentPeriod = new DateTime(currentDate.Year, currentDate.Month, 1);
                var previousPeriod = currentPeriod.AddMonths(-1);

                // 🔒 Find cross-period adjustments
                var crossPeriodAdjustments = await _context.Transactions
                    .Where(t => t.CompanyId == companyId && 
                               t.Description.Contains("Adjustment") &&
                               (t.TransactionDate < previousPeriod || t.TransactionDate > currentPeriod))
                    .Include(t => t.LedgerEntries)
                    .ToListAsync();

                result.CrossPeriodAdjustmentCount = crossPeriodAdjustments.Count;

                var adjustmentIssues = new List<CrossPeriodIssue>();

                foreach (var adjustment in crossPeriodAdjustments)
                {
                    // Check if adjustment spans multiple periods
                    var adjustmentPeriod = new DateTime(adjustment.TransactionDate.Year, adjustment.TransactionDate.Month, 1);
                    
                    if (adjustmentPeriod < previousPeriod || adjustmentPeriod > currentPeriod)
                    {
                        // Check if this creates period balance issues
                        var adjustmentPeriodEnd = adjustmentPeriod.AddMonths(1).AddDays(-1);
                        var trialBalanceAdjustment = await _accountingEngine.GetTrialBalanceAsync(companyId, DateTime.MinValue, adjustmentPeriodEnd);
                        
                        // Check if trial balance is balanced after adjustment
                        if (Math.Abs(trialBalanceAdjustment.DebitTotal - trialBalanceAdjustment.CreditTotal) > 0.01m)
                        {
                            adjustmentIssues.Add(new CrossPeriodIssue
                            {
                                JournalId = adjustment.Id,
                                AdjustmentDate = adjustment.TransactionDate,
                                IssueType = "UnbalancedCrossPeriodAdjustment",
                                Severity = TemporalIssueSeverity.Critical,
                                Description = $"Cross-period adjustment creates unbalanced trial balance"
                            });
                        }

                        // Check if adjustment affects closed periods
                        var affectedClosedPeriod = await _context.PeriodClosings
                            .FirstOrDefaultAsync(pc => pc.CompanyId == companyId && 
                                                    pc.IsLocked &&
                                                    pc.ClosingDate >= adjustmentPeriodEnd);

                        if (affectedClosedPeriod != null)
                        {
                            adjustmentIssues.Add(new CrossPeriodIssue
                            {
                                JournalId = adjustment.Id,
                                AdjustmentDate = adjustment.TransactionDate,
                                IssueType = "CrossPeriodAdjustmentInClosedPeriod",
                                Severity = TemporalIssueSeverity.Critical,
                                Description = $"Cross-period adjustment affects closed period ending {affectedClosedPeriod.ClosingDate:yyyy-MM-dd}"
                            });
                        }
                    }
                }

                result.AdjustmentIssues = adjustmentIssues;
                result.Status = adjustmentIssues.Any(issue => issue.Severity == TemporalIssueSeverity.Critical) ?
                    TimeTravelTestStatus.Failed : 
                    adjustmentIssues.Any() ? TimeTravelTestStatus.Warning : TimeTravelTestStatus.Passed;

                result.Message = result.Status == TimeTravelTestStatus.Passed ?
                    "No critical cross-period adjustment issues found" :
                    $"Found {adjustmentIssues.Count} cross-period adjustment issues";
            }
            catch (Exception ex)
            {
                result.Status = TimeTravelTestStatus.Error;
                result.ErrorMessage = $"Cross-period adjustment test error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔒 Test 5: Temporal data consistency
        /// </summary>
        private async Task<TemporalConsistencyTestResult> TestTemporalConsistencyAsync(int companyId)
        {
            var result = new TemporalConsistencyTestResult { CompanyId = companyId };

            try
            {
                var consistencyIssues = new List<TemporalConsistencyIssue>();

                // 🔒 Check for logical temporal inconsistencies
                var allTransactions = await _context.Transactions
                    .Where(t => t.CompanyId == companyId)
                    .OrderBy(t => t.CreatedAt)
                    .ToListAsync();

                // Check for transactions created before their date
                var createdBeforeDate = allTransactions
                    .Where(t => t.CreatedAt.Date > t.TransactionDate.Date)
                    .ToList();

                if (createdBeforeDate.Any())
                {
                    consistencyIssues.Add(new TemporalConsistencyIssue
                    {
                        IssueType = "TransactionCreatedAfterDate",
                        AffectedRecordCount = createdBeforeDate.Count,
                        Severity = TemporalIssueSeverity.Medium,
                        Description = $"{createdBeforeDate.Count} transactions created after their posting date"
                    });
                }

                // 🔒 Check for audit trail temporal gaps
                var auditRecords = await _context.AuditTrails
                    .Where(a => a.CompanyId == companyId)
                    .OrderBy(a => a.CreatedAt)
                    .ToListAsync();

                if (auditRecords.Any())
                {
                    var timeGaps = new List<TimeGap>();
                    for (int i = 1; i < auditRecords.Count; i++)
                    {
                        var gap = auditRecords[i].CreatedAt - auditRecords[i - 1].CreatedAt;
                        if (gap.TotalHours > 24) // Gap of more than 24 hours
                        {
                            timeGaps.Add(new TimeGap
                            {
                                Start = auditRecords[i - 1].CreatedAt,
                                End = auditRecords[i].CreatedAt,
                                Duration = gap,
                                Description = $"Audit trail gap of {gap.TotalHours:F1} hours"
                            });
                        }
                    }

                    if (timeGaps.Any())
                    {
                        consistencyIssues.Add(new TemporalConsistencyIssue
                        {
                            IssueType = "AuditTrailTimeGaps",
                            AffectedRecordCount = timeGaps.Count,
                            Severity = TemporalIssueSeverity.Low,
                            Description = $"Found {timeGaps.Count} audit trail time gaps"
                        });
                    }
                }

                // 🔒 Check for entity timestamp consistency
                var entitiesWithTimestamps = new List<object>
                {
                    await _context.Invoices.Where(i => i.CompanyId == companyId).ToListAsync(),
                    await _context.Transactions.Where(t => t.CompanyId == companyId).ToListAsync(),
                    await _context.PayrollRuns.Where(pr => pr.CompanyId == companyId).ToListAsync()
                };

                var timestampInconsistencies = 0;
                foreach (var entityList in entitiesWithTimestamps)
                {
                    if (entityList is IEnumerable<BaseEntity> baseEntities)
                    {
                        timestampInconsistencies += baseEntities.Count(e => e.UpdatedAt.HasValue && e.UpdatedAt < e.CreatedAt);
                    }
                }

                if (timestampInconsistencies > 0)
                {
                    consistencyIssues.Add(new TemporalConsistencyIssue
                    {
                        IssueType = "TimestampInconsistency",
                        AffectedRecordCount = timestampInconsistencies,
                        Severity = TemporalIssueSeverity.Medium,
                        Description = $"{timestampInconsistencies} records have updated timestamps before creation"
                    });
                }

                result.ConsistencyIssues = consistencyIssues;
                result.Status = consistencyIssues.Any(issue => issue.Severity == TemporalIssueSeverity.Critical) ?
                    TimeTravelTestStatus.Failed : 
                    consistencyIssues.Any() ? TimeTravelTestStatus.Warning : TimeTravelTestStatus.Passed;

                result.Message = result.Status == TimeTravelTestStatus.Passed ?
                    "Temporal data consistency verified" :
                    $"Found {consistencyIssues.Count} temporal consistency issues";
            }
            catch (Exception ex)
            {
                result.Status = TimeTravelTestStatus.Error;
                result.ErrorMessage = $"Temporal consistency test error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 🔒 Test 6: Audit trail temporal integrity
        /// </summary>
        private async Task<AuditTrailTemporalTestResult> TestAuditTrailTemporalIntegrityAsync(int companyId)
        {
            var result = new AuditTrailTemporalTestResult { CompanyId = companyId };

            try
            {
                // 🔒 Get audit records in chronological order
                var auditRecords = await _context.AuditTrails
                    .Where(a => a.CompanyId == companyId)
                    .OrderBy(a => a.CreatedAt)
                    .ToListAsync();

                result.TotalAuditRecords = auditRecords.Count;

                var integrityIssues = new List<AuditIntegrityIssue>();

                if (auditRecords.Any())
                {
                    // 🔒 Check for missing audit records
                    var financialTransactions = await _context.Transactions
                        .Where(t => t.CompanyId == companyId)
                        .ToListAsync();

                    var missingAudits = financialTransactions
                        .Where(t => !auditRecords.Any(ar => ar.EntityId == t.Id && ar.EntityType == "Transaction"))
                        .ToList();

                    if (missingAudits.Any())
                    {
                        integrityIssues.Add(new AuditIntegrityIssue
                        {
                            IssueType = "MissingAuditRecords",
                            AffectedRecordCount = missingAudits.Count,
                            Severity = TemporalIssueSeverity.High,
                            Description = $"{missingAudits.Count} financial transactions missing audit records"
                        });
                    }

                    // 🔒 Check for out-of-sequence timestamps
                    var outOfSequence = 0;
                    for (int i = 1; i < auditRecords.Count; i++)
                    {
                        if (auditRecords[i].CreatedAt < auditRecords[i - 1].CreatedAt)
                        {
                            outOfSequence++;
                        }
                    }

                    if (outOfSequence > 0)
                    {
                        integrityIssues.Add(new AuditIntegrityIssue
                        {
                            IssueType = "OutOfSequenceTimestamps",
                            AffectedRecordCount = outOfSequence,
                            Severity = TemporalIssueSeverity.Medium,
                            Description = $"{outOfSequence} audit records have out-of-sequence timestamps"
                        });
                    }

                    // 🔒 Check for hash chain integrity over time
                    var hashChainBreaks = 0;
                    for (int i = 1; i < auditRecords.Count; i++)
                    {
                        if (auditRecords[i].PreviousHash != auditRecords[i - 1].CurrentHash)
                        {
                            hashChainBreaks++;
                        }
                    }

                    if (hashChainBreaks > 0)
                    {
                        integrityIssues.Add(new AuditIntegrityIssue
                        {
                            IssueType = "HashChainBreaks",
                            AffectedRecordCount = hashChainBreaks,
                            Severity = TemporalIssueSeverity.Critical,
                            Description = $"{hashChainBreaks} audit trail hash chain breaks detected"
                        });
                    }
                }

                result.IntegrityIssues = integrityIssues;
                result.Status = integrityIssues.Any(issue => issue.Severity == TemporalIssueSeverity.Critical) ?
                    TimeTravelTestStatus.Failed : 
                    integrityIssues.Any() ? TimeTravelTestStatus.Warning : TimeTravelTestStatus.Passed;

                result.Message = result.Status == TimeTravelTestStatus.Passed ?
                    "Audit trail temporal integrity verified" :
                    $"Found {integrityIssues.Count} audit integrity issues";
            }
            catch (Exception ex)
            {
                result.Status = TimeTravelTestStatus.Error;
                result.ErrorMessage = $"Audit trail temporal test error: {ex.Message}";
            }

            return result;
        }

        // Helper methods
        private TimeTravelTestStatus CalculateOverallTestStatus(TimeTravelTestResult result)
        {
            var allStatuses = new[]
            {
                result.BackdatedInvoiceTest.Status,
                result.FutureDatedJournalTest.Status,
                result.PeriodReopeningTest.Status,
                result.CrossPeriodAdjustmentTest.Status,
                result.TemporalConsistencyTest.Status,
                result.AuditTrailTemporalTest.Status
            };

            if (allStatuses.Any(s => s == TimeTravelTestStatus.Error)) return TimeTravelTestStatus.Error;
            if (allStatuses.Any(s => s == TimeTravelTestStatus.Failed)) return TimeTravelTestStatus.Failed;
            if (allStatuses.Any(s => s == TimeTravelTestStatus.Warning)) return TimeTravelTestStatus.Warning;
            return TimeTravelTestStatus.Passed;
        }

        private int CountPassedTests(TimeTravelTestResult result)
        {
            return new[]
            {
                result.BackdatedInvoiceTest.Status,
                result.FutureDatedJournalTest.Status,
                result.PeriodReopeningTest.Status,
                result.CrossPeriodAdjustmentTest.Status,
                result.TemporalConsistencyTest.Status,
                result.AuditTrailTemporalTest.Status
            }.Count(s => s == TimeTravelTestStatus.Passed);
        }

        private List<CriticalTemporalIssue> IdentifyCriticalIssues(TimeTravelTestResult result)
        {
            var criticalIssues = new List<CriticalTemporalIssue>();

            // Collect all critical issues
            criticalIssues.AddRange(result.BackdatedInvoiceTest.PeriodIntegrityIssues
                .Where(i => i.Severity == TemporalIssueSeverity.Critical)
                .Select(i => new CriticalTemporalIssue
                {
                    TestType = "BackdatedInvoice",
                    IssueType = i.IssueType,
                    Description = i.Description,
                    EntityId = i.InvoiceId
                }));

            criticalIssues.AddRange(result.FutureDatedJournalTest.FutureDateIssues
                .Where(i => i.Severity == TemporalIssueSeverity.High)
                .Select(i => new CriticalTemporalIssue
                {
                    TestType = "FutureDatedJournal",
                    IssueType = i.IssueType,
                    Description = i.Description,
                    EntityId = i.JournalId
                }));

            criticalIssues.AddRange(result.PeriodReopeningTest.ReopeningAttempts
                .Where(i => i.Severity == TemporalIssueSeverity.Critical)
                .Select(i => new CriticalTemporalIssue
                {
                    TestType = "PeriodReopening",
                    IssueType = i.AttemptType,
                    Description = i.Description,
                    EntityId = i.PeriodClosingId
                }));

            return criticalIssues;
        }

        private async Task<decimal> CalculateRevenueDifferenceAsync(SecureERP2.Modules.Invoice.Entities.Invoice invoice)
        {
            // Calculate total revenue from ledger entries for this invoice
            var revenueEntries = await _context.LedgerEntries
                .Join(_context.FinanceAccounts, le => le.AccountId, fa => fa.Id, (le, fa) => new { le, fa })
                .Where(x => x.le.Description.Contains($"Invoice {invoice.InvoiceNumber}") &&
                           x.fa.AccountType == AccountType.Revenue &&
                           x.le.CompanyId == invoice.CompanyId)
                .ToListAsync();

            return revenueEntries.Sum(x => x.le.CreditAmount - x.le.DebitAmount);
        }
    }

    // Supporting classes
    public class TimeTravelTestResult
    {
        public int CompanyId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        
        public BackdatedInvoiceTestResult BackdatedInvoiceTest { get; set; } = new();
        public FutureDatedJournalTestResult FutureDatedJournalTest { get; set; } = new();
        public PeriodReopeningTestResult PeriodReopeningTest { get; set; } = new();
        public CrossPeriodAdjustmentTestResult CrossPeriodAdjustmentTest { get; set; } = new();
        public TemporalConsistencyTestResult TemporalConsistencyTest { get; set; } = new();
        public AuditTrailTemporalTestResult AuditTrailTemporalTest { get; set; } = new();
        
        public TimeTravelTestStatus OverallStatus { get; set; }
        public int TotalTestsRun { get; set; }
        public int TestsPassed { get; set; }
        public List<CriticalTemporalIssue> CriticalIssues { get; set; } = new();
    }

    public class BackdatedInvoiceTestResult
    {
        public int CompanyId { get; set; }
        public TimeTravelTestStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public string Message { get; set; } = string.Empty;
        public int BackdatedInvoiceCount { get; set; }
        public List<TimeTravelPeriodIntegrityIssue> PeriodIntegrityIssues { get; set; } = new();
    }

    public class FutureDatedJournalTestResult
    {
        public int CompanyId { get; set; }
        public TimeTravelTestStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public string Message { get; set; } = string.Empty;
        public int FutureDatedJournalCount { get; set; }
        public List<FutureDateIssue> FutureDateIssues { get; set; } = new();
    }

    public class PeriodReopeningTestResult
    {
        public int CompanyId { get; set; }
        public TimeTravelTestStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public string Message { get; set; } = string.Empty;
        public int LockedPeriodCount { get; set; }
        public List<ReopeningAttempt> ReopeningAttempts { get; set; } = new();
    }

    public class CrossPeriodAdjustmentTestResult
    {
        public int CompanyId { get; set; }
        public TimeTravelTestStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public string Message { get; set; } = string.Empty;
        public int CrossPeriodAdjustmentCount { get; set; }
        public List<CrossPeriodIssue> AdjustmentIssues { get; set; } = new();
    }

    public class TemporalConsistencyTestResult
    {
        public int CompanyId { get; set; }
        public TimeTravelTestStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<TemporalConsistencyIssue> ConsistencyIssues { get; set; } = new();
    }

    public class AuditTrailTemporalTestResult
    {
        public int CompanyId { get; set; }
        public TimeTravelTestStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalAuditRecords { get; set; }
        public List<AuditIntegrityIssue> IntegrityIssues { get; set; } = new();
    }

    // Issue classes
    public class TimeTravelPeriodIntegrityIssue
    {
        public int InvoiceId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime? ClosedPeriod { get; set; }
        public string IssueType { get; set; } = string.Empty;
        public TemporalIssueSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class FutureDateIssue
    {
        public int JournalId { get; set; }
        public DateTime JournalDate { get; set; }
        public string IssueType { get; set; } = string.Empty;
        public TemporalIssueSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class ReopeningAttempt
    {
        public int PeriodClosingId { get; set; }
        public DateTime ClosingDate { get; set; }
        public DateTime LockedAt { get; set; }
        public string AttemptType { get; set; } = string.Empty;
        public int SuspiciousEntryCount { get; set; }
        public TemporalIssueSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class CrossPeriodIssue
    {
        public int JournalId { get; set; }
        public DateTime AdjustmentDate { get; set; }
        public string IssueType { get; set; } = string.Empty;
        public TemporalIssueSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class TemporalConsistencyIssue
    {
        public string IssueType { get; set; } = string.Empty;
        public int AffectedRecordCount { get; set; }
        public TemporalIssueSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class AuditIntegrityIssue
    {
        public string IssueType { get; set; } = string.Empty;
        public int AffectedRecordCount { get; set; }
        public TemporalIssueSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class CriticalTemporalIssue
    {
        public string TestType { get; set; } = string.Empty;
        public string IssueType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int EntityId { get; set; }
    }

    public class TimeGap
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public TimeSpan Duration { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    // Enums
    public enum TimeTravelTestStatus
    {
        Passed,
        Warning,
        Failed,
        Error
    }

    public enum TemporalIssueSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    }
