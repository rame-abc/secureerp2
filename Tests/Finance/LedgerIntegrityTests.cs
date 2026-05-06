using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using SecureERP2.Modules.Finance.Services;
using SecureERP2.Modules.Finance.Entities;

namespace SecureERP2.Tests.Finance
{
    /// <summary>
    /// 🔒 Ledger Integrity Test - Financial Truth Verification
    /// </summary>
    [TestClass]
    public class LedgerIntegrityTests
    {
        private readonly ERPDbContext _context;
        private readonly LedgerEngineService _ledgerEngine;
        private readonly ReconciliationEngine _reconciliationEngine;
        private readonly ILogger<LedgerIntegrityTests> _logger;

        public LedgerIntegrityTests(
            ERPDbContext context,
            LedgerEngineService ledgerEngine,
            ReconciliationEngine reconciliationEngine,
            ILogger<LedgerIntegrityTests> logger)
        {
            _context = context;
            _ledgerEngine = ledgerEngine;
            _reconciliationEngine = reconciliationEngine;
            _logger = logger;
        }

        [TestInitialize]
        public void Setup()
        {
            // Clean database for each test
            _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up after each test
            _context.Database.EnsureDeleted();
        }

        #region Test 1: Database vs Ledger State Consistency

        /// <summary>
        /// Test 1: Database state should match ledger state exactly
        /// </summary>
        [TestMethod]
        public async Task DatabaseVsLedgerState_ShouldMatchExactly()
        {
            // Arrange - Create controlled data
            var companyId = 1;
            
            // Create test accounts
            var accounts = new List<FinanceAccount>
            {
                new FinanceAccount { Id = 1001, CurrentBalance = 5000m, AccountNumber = "1001", AccountName = "Cash" },
                new FinanceAccount { Id = 1002, CurrentBalance = 10000m, AccountNumber = "1002", AccountName = "Bank" },
                new FinanceAccount { Id = 1003, CurrentBalance = -2000m, AccountNumber = "1003", AccountName = "Accounts Receivable" }
            };

            _context.FinanceAccounts.AddRange(accounts);
            await _context.SaveChangesAsync();

            // Act - Rebuild ledger from database state
            var ledgerRebuildResult = await _ledgerEngine.RebuildLedgerAsync(companyId);

            // Assert - Compare ledger state with database
            Assert.IsTrue(ledgerRebuildResult.IsSuccess, "Ledger rebuild should succeed");
            
            var ledgerAccounts = ledgerRebuildResult.Data?.AccountBalances ?? new Dictionary<int, decimal>();
            
            foreach (var account in accounts)
            {
                Assert.IsTrue(ledgerAccounts.ContainsKey(account.Id), 
                    $"Account {account.AccountNumber} should exist in ledger");
                
                if (ledgerAccounts.TryGetValue(account.Id, out var ledgerBalance))
                {
                    Assert.AreEqual(account.CurrentBalance, ledgerBalance, 
                        $"Account {account.AccountNumber} balance mismatch: DB={account.CurrentBalance}, Ledger={ledgerBalance}");
                }
                else
                {
                    Assert.Fail($"Account {account.AccountNumber} missing from ledger rebuild result");
                }
            }

            _logger.LogInformation("Database vs Ledger State consistency test completed successfully");
        }

        #endregion

        #region Test 2: Financial Truth Verification

        /// <summary>
        /// Test 2: Reconciliation should detect all inconsistencies
        /// </summary>
        [TestMethod]
        public async Task Reconciliation_ShouldDetectAllInconsistencies_WhenDataMismatch()
        {
            // Arrange - Create inconsistent data
            var companyId = 1;
            var asOfDate = DateTime.UtcNow;

            // Create manual journal entries that don't match reality
            var manualEntries = new List<JournalEntry>
            {
                new JournalEntry
                {
                    CompanyId = companyId,
                    TransactionDate = asOfDate.AddDays(-1),
                    Description = "Manual adjustment - cash",
                    Status = JournalStatus.Posted,
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = 1001, DebitAmount = 1000m, CreditAmount = 0 }
                    }
                },
                new JournalEntry
                {
                    CompanyId = companyId,
                    TransactionDate = asOfDate.AddDays(-1),
                    Description = "Manual adjustment - bank",
                    Status = JournalStatus.Posted,
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = 1002, DebitAmount = 5000m, CreditAmount = 0 }
                    }
                }
            };

            _context.JournalEntries.AddRange(manualEntries);
            await _context.SaveChangesAsync();

            // Act - Run reconciliation
            var reconciliationResult = await _reconciliationEngine.RunComprehensiveReconciliationAsync(companyId, asOfDate);

            // Assert - Should detect inconsistencies
            Assert.IsTrue(reconciliationResult.IsSuccess, "Reconciliation should complete");
            Assert.IsTrue(reconciliationResult.InvoiceReconciliation?.Differences?.Count > 0, 
                "Should detect invoice/GL differences");
            Assert.IsTrue(reconciliationResult.PayrollReconciliation?.Differences?.Count > 0, 
                "Should detect payroll/expense differences");
            
            // Verify specific inconsistencies were found
            var allDifferences = new List<string>();
            
            if (reconciliationResult.InvoiceReconciliation?.Differences != null)
            {
                allDifferences.AddRange(reconciliationResult.InvoiceReconciliation.Differences.Select(d => 
                    $"Invoice GL mismatch: {d.Description}"));
            }

            if (reconciliationResult.PayrollReconciliation?.Differences != null)
            {
                allDifferences.AddRange(reconciliationResult.PayrollReconciliation.Differences.Select(d => 
                    $"Payroll expense mismatch: {d.Description}"));
            }

            Assert.IsTrue(allDifferences.Count > 0, "Should have detected inconsistencies");
            
            _logger.LogInformation("Financial truth verification test completed: {InconsistencyCount} inconsistencies detected", 
                allDifferences.Count);
        }

        #endregion

        #region Test 3: No Drift Over Time

        /// <summary>
        /// Test 3: Ledger balances should remain stable over time
        /// </summary>
        [TestMethod]
        public async Task LedgerBalances_ShouldRemainStable_OverTime()
        {
            // Arrange - Create initial state
            var companyId = 1;
            var initialBalance = 10000m;
            
            var account = new FinanceAccount
            {
                Id = 1001,
                CurrentBalance = initialBalance,
                AccountNumber = "STABILITY-001",
                AccountName = "Stability Test Account"
            };

            _context.FinanceAccounts.Add(account);
            await _context.SaveChangesAsync();

            // Act - Process multiple transactions that should net to zero
            var transactions = Enumerable.Range(1, 10).Select(i => new JournalEntry
            {
                CompanyId = companyId,
                TransactionDate = DateTime.UtcNow.AddMinutes(-i),
                Description = $"Stability test transaction {i}",
                Status = JournalStatus.Posted,
                JournalLines = new[]
                {
                    new JournalLine { AccountId = 1001, DebitAmount = i % 2 == 0 ? 1000m : 0, CreditAmount = i % 2 == 1 ? 1000m : 0 }
                }
            });

            foreach (var transaction in transactions)
            {
                _context.JournalEntries.Add(transaction);
            }
            await _context.SaveChangesAsync();

            // Rebuild to verify stability
            var rebuildResult = await _ledgerEngine.RebuildLedgerAsync(companyId);

            // Assert - Balance should return to original
            Assert.IsTrue(rebuildResult.IsSuccess, "Ledger rebuild should succeed");
            
            var finalBalance = rebuildResult.Data?.AccountBalances?.GetValueOrDefault(1001, 0m);
            Assert.AreEqual(initialBalance, finalBalance, 
                $"Balance should be stable: Initial={initialBalance}, Final={finalBalance}");
            
            _logger.LogInformation("Ledger stability test completed: Initial={InitialBalance}, Final={FinalBalance}", 
                initialBalance, finalBalance);
        }

        #endregion

        #region Test 4: Rounding Accuracy

        /// <summary>
        /// Test 4: No rounding errors in financial calculations
        /// </summary>
        [TestMethod]
        public async Task RoundingAccuracy_ShouldBePrecise_WithFinancialCalculations()
        {
            // Arrange - Create transactions with precise amounts
            var companyId = 1;
            var preciseAmount = 1000.333333333m; // Creates repeating decimal
            
            var transactions = new List<JournalEntry>
            {
                new JournalEntry
                {
                    CompanyId = companyId,
                    TransactionDate = DateTime.UtcNow,
                    Description = "Precision test 1",
                    Status = JournalStatus.Posted,
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = 1001, DebitAmount = preciseAmount, CreditAmount = 0 }
                    }
                },
                new JournalEntry
                {
                    CompanyId = companyId,
                    TransactionDate = DateTime.UtcNow,
                    Description = "Precision test 2",
                    Status = JournalStatus.Posted,
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = 1001, DebitAmount = 0, CreditAmount = preciseAmount }
                    }
                }
            };

            _context.JournalEntries.AddRange(transactions);
            await _context.SaveChangesAsync();

            // Act - Run reconciliation
            var reconciliationResult = await _reconciliationEngine.RunComprehensiveReconciliationAsync(companyId, DateTime.UtcNow);

            // Assert - Should handle precise amounts without rounding errors
            Assert.IsTrue(reconciliationResult.IsSuccess, "Reconciliation should handle precise amounts");
            
            // Verify no rounding discrepancies
            if (reconciliationResult.InvoiceReconciliation?.Differences != null)
            {
                var roundingErrors = reconciliationResult.InvoiceReconciliation.Differences
                    .Where(d => d.Description.Contains("rounding") || d.TotalDifference > 0.01m)
                    .ToList();

                Assert.AreEqual(0, roundingErrors.Count, "Should have no rounding errors");
            }

            _logger.LogInformation("Rounding accuracy test completed successfully");
        }

        #endregion

        #region Test 5: Cross-Module Consistency

        /// <summary>
        /// Test 5: Data consistency across finance modules
        /// </summary>
        [TestMethod]
        public async Task CrossModuleConsistency_ShouldMaintainDataIntegrity()
        {
            // Arrange - Create related data across modules
            var companyId = 1;
            var customerId = "CUST-001";
            var invoiceId = "INV-001";
            var invoiceAmount = 5000m;

            // Create invoice
            var invoice = new Invoice
            {
                CompanyId = companyId,
                CustomerId = customerId,
                InvoiceNumber = invoiceId,
                InvoiceAmount = invoiceAmount,
                Status = "Posted",
                CreatedAt = DateTime.UtcNow
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            // Create corresponding journal entry
            var journalEntry = new JournalEntry
            {
                CompanyId = companyId,
                TransactionDate = DateTime.UtcNow,
                Description = $"Invoice {invoiceId}",
                Status = JournalStatus.Posted,
                JournalLines = new[]
                {
                    new JournalLine { AccountId = 4001, DebitAmount = 0, CreditAmount = invoiceAmount, Description = $"Invoice {invoiceId}" }
                }
            };

            _context.JournalEntries.Add(journalEntry);
            await _context.SaveChangesAsync();

            // Act - Run reconciliation
            var reconciliationResult = await _reconciliationEngine.RunComprehensiveReconciliationAsync(companyId, DateTime.UtcNow);

            // Assert - Should find matching records
            Assert.IsTrue(reconciliationResult.IsSuccess, "Cross-module reconciliation should succeed");
            Assert.IsNotNull(reconciliationResult.InvoiceReconciliation, "Invoice reconciliation should not be null");
            
            // Verify consistency
            var reconciledInvoice = reconciliationResult.InvoiceReconciliation?.InvoicesProcessed
                ?.FirstOrDefault(i => i.InvoiceId == invoiceId);

            Assert.IsNotNull(reconciledInvoice, "Should find reconciled invoice");
            Assert.AreEqual(invoiceAmount, reconciledInvoice?.InvoiceAmount, 
                "Invoice amounts should match");

            _logger.LogInformation("Cross-module consistency test completed successfully");
        }

        #endregion

        #region Test 6: Audit Trail Completeness

        /// <summary>
        /// Test 6: All critical operations should be auditable
        /// </summary>
        [TestMethod]
        public async Task AuditTrail_ShouldCaptureAllCriticalOperations()
        {
            // Arrange - Perform critical operations
            var companyId = 1;

            // Create and post journal
            var createEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalCreated",
                CompanyId = companyId,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 123,
                    Description = "Audit test journal",
                    CreatedBy = "audit-test"
                })
            };

            var createResult = await _ledgerEngine.ProcessEventAsync(createEvent);
            Assert.IsTrue(createResult.IsSuccess, "Journal creation should be auditable");

            // Post the journal
            var postEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalPosted",
                CompanyId = companyId,
                Timestamp = DateTime.UtcNow,
                Version = 2,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 123,
                    PostedBy = "audit-test",
                    PostedAt = DateTime.UtcNow
                })
            };

            var postResult = await _ledgerEngine.ProcessEventAsync(postEvent);
            Assert.IsTrue(postResult.IsSuccess, "Journal posting should be auditable");

            // Act - Verify audit trail
            var auditTrail = await _context.AuditTrails
                .Where(at => at.CompanyId == companyId)
                .OrderByDescending(at => at.Timestamp)
                .Take(10)
                .ToListAsync();

            // Assert - Should capture both operations
            Assert.AreEqual(2, auditTrail.Count, "Should capture both journal creation and posting");
            
            var createAudit = auditTrail.FirstOrDefault(at => at.Description.Contains("JournalCreated"));
            var postAudit = auditTrail.FirstOrDefault(at => at.Description.Contains("JournalPosted"));

            Assert.IsNotNull(createAudit, "Should audit journal creation");
            Assert.IsNotNull(postAudit, "Should audit journal posting");
            
            _logger.LogInformation("Audit trail completeness test completed: {AuditCount} operations captured", auditTrail.Count);
        }

        #endregion
    }

    /// <summary>
    /// Test result container for ledger integrity tests
    /// </summary>
    public class LedgerIntegrityTestResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public Dictionary<int, decimal> AccountBalances { get; set; } = new();
        public List<string> Inconsistencies { get; set; } = new();
        public TimeSpan ExecutionTime { get; set; }
    }
}
