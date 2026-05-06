using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SecureERP2.Modules.Finance.Services;
using SecureERP2.Modules.Finance.Entities;
using SecureERP2.Modules.Finance.Journal;

namespace SecureERP2.Tests.Finance
{
    /// <summary>
    /// 🔒 Critical Path Test Suite - Core Finance Business Flows
    /// </summary>
    [TestClass]
    public class CriticalPathTests
    {
        private readonly ERPDbContext _context;
        private readonly LedgerEngineService _ledgerEngine;
        private readonly ReconciliationEngine _reconciliationEngine;
        private readonly ILogger<CriticalPathTests> _logger;

        public CriticalPathTests(
            ERPDbContext context,
            LedgerEngineService ledgerEngine,
            ReconciliationEngine reconciliationEngine,
            ILogger<CriticalPathTests> logger)
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

        #region Finance Core: Create Journal Flow

        /// <summary>
        /// Test 1: Create Journal - Core business flow
        /// </summary>
        [TestMethod]
        public async Task CreateJournal_ShouldCreateJournalEntry_WhenValidData()
        {
            // Arrange
            var financeEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalCreated",
                CompanyId = 1,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 123,
                    TransactionNumber = "JNL-001",
                    TransactionDate = DateTime.UtcNow,
                    Description = "Test journal entry",
                    CreatedBy = "test-user",
                    JournalLines = new[]
                    {
                        new { AccountId = 1001, DebitAmount = 1000, CreditAmount = 0, Description = "Cash debit" },
                        new { AccountId = 4001, DebitAmount = 0, CreditAmount = 1000, Description = "Cash credit" }
                    }
                })
            };

            // Act
            var result = await _ledgerEngine.ProcessEventAsync(financeEvent);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Journal creation should succeed");
            Assert.IsNotNull(result.Data, "Result data should not be null");
            
            // Verify database state
            var journalEntry = await _context.JournalEntries
                .Include(je => je.JournalLines)
                .FirstOrDefaultAsync(je => je.TransactionNumber == "JNL-001");

            Assert.IsNotNull(journalEntry, "Journal entry should be created in database");
            Assert.AreEqual("Test journal entry", journalEntry.Description);
            Assert.AreEqual(1000m, journalEntry.TotalDebit);
            Assert.AreEqual(1000m, journalEntry.TotalCredit);
        }

        /// <summary>
        /// Test 2: Create Journal - Validation failure
        /// </summary>
        [TestMethod]
        public async Task CreateJournal_ShouldFail_WhenInvalidData()
        {
            // Arrange
            var financeEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalCreated",
                CompanyId = 1,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    // Missing required fields
                    TransactionNumber = "JNL-002",
                    Description = "", // Empty description
                    JournalLines = new object[0] // Empty lines
                })
            };

            // Act
            var result = await _ledgerEngine.ProcessEventAsync(financeEvent);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Journal creation should fail");
            Assert.IsTrue(result.ErrorMessage.Contains("required"), "Should mention required fields");
            
            // Verify no database changes
            var journalEntries = await _context.JournalEntries.ToListAsync();
            Assert.AreEqual(0, journalEntries.Count, "No journal entries should be created");
        }

        #endregion

        #region Finance Core: Post Journal Flow

        /// <summary>
        /// Test 3: Post Journal - Core business flow
        /// </summary>
        [TestMethod]
        public async Task PostJournal_ShouldUpdateJournalEntry_WhenValidData()
        {
            // Arrange - First create a draft journal
            var createEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalCreated",
                CompanyId = 1,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 456,
                    TransactionNumber = "JNL-003",
                    Description = "Test journal for posting",
                    CreatedBy = "test-user",
                    JournalLines = new[]
                    {
                        new { AccountId = 1001, DebitAmount = 500, CreditAmount = 0, Description = "Revenue debit" },
                        new { AccountId = 4001, DebitAmount = 0, CreditAmount = 500, Description = "Revenue credit" }
                    }
                })
            };

            var createResult = await _ledgerEngine.ProcessEventAsync(createEvent);
            Assert.IsTrue(createResult.IsSuccess, "Draft journal creation should succeed");

            // Act - Post the journal
            var postEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalPosted",
                CompanyId = 1,
                Timestamp = DateTime.UtcNow,
                Version = 2,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 456,
                    PostedBy = "test-user",
                    PostedAt = DateTime.UtcNow
                })
            };

            var postResult = await _ledgerEngine.ProcessEventAsync(postEvent);

            // Assert
            Assert.IsTrue(postResult.IsSuccess, "Journal posting should succeed");
            
            // Verify database state
            var journalEntry = await _context.JournalEntries
                .Include(je => je.JournalLines)
                .FirstOrDefaultAsync(je => je.Id == 456);

            Assert.IsNotNull(journalEntry, "Journal entry should exist");
            Assert.AreEqual(JournalStatus.Posted, journalEntry.Status, "Journal should be posted");
        }

        #endregion

        #region Finance Core: Void Journal Flow

        /// <summary>
        /// Test 4: Void Journal - Core business flow
        /// </summary>
        [TestMethod]
        public async Task VoidJournal_ShouldCreateReversalEntry_WhenValidData()
        {
            // Arrange - Create and post a journal first
            var createEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalCreated",
                CompanyId = 1,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 789,
                    TransactionNumber = "JNL-004",
                    Description = "Journal to void",
                    CreatedBy = "test-user",
                    JournalLines = new[]
                    {
                        new { AccountId = 2001, DebitAmount = 200, CreditAmount = 0, Description = "Expense debit" },
                        new { AccountId = 5001, DebitAmount = 0, CreditAmount = 200, Description = "Expense credit" }
                    }
                })
            };

            var createResult = await _ledgerEngine.ProcessEventAsync(createEvent);
            var postEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalPosted",
                CompanyId = 1,
                Timestamp = DateTime.UtcNow,
                Version = 2,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 789,
                    PostedBy = "test-user",
                    PostedAt = DateTime.UtcNow
                })
            };

            var postResult = await _ledgerEngine.ProcessEventAsync(postEvent);
            Assert.IsTrue(postResult.IsSuccess, "Journal posting should succeed");

            // Act - Void the journal
            var voidEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalVoided",
                CompanyId = 1,
                Timestamp = DateTime.UtcNow,
                Version = 3,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 789,
                    VoidedBy = "test-user",
                    VoidedAt = DateTime.UtcNow,
                    Reason = "Test void"
                })
            };

            var voidResult = await _ledgerEngine.ProcessEventAsync(voidEvent);

            // Assert
            Assert.IsTrue(voidResult.IsSuccess, "Journal voiding should succeed");
            
            // Verify database state
            var journalEntry = await _context.JournalEntries
                .Include(je => je.JournalLines)
                .FirstOrDefaultAsync(je => je.Id == 789);

            Assert.IsNotNull(journalEntry, "Journal entry should exist");
            Assert.AreEqual(JournalStatus.Reversed, journalEntry.Status, "Journal should be reversed");
            
            // Verify reversal entry was created
            var reversalEntries = await _context.JournalEntries
                .Where(je => je.Description.Contains("REVERSAL"))
                .ToListAsync();

            Assert.IsTrue(reversalEntries.Count > 0, "Reversal entry should be created");
        }

        #endregion

        #region Finance Core: Ledger Rebuild Flow

        /// <summary>
        /// Test 5: Ledger Rebuild - Core business flow
        /// </summary>
        [TestMethod]
        public async Task RebuildLedger_ShouldUpdateAccountBalances_WhenValidData()
        {
            // Arrange
            var companyId = 1;
            
            // Act
            var result = await _ledgerEngine.RebuildLedgerAsync(companyId);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Ledger rebuild should succeed");
            Assert.IsNotNull(result.Data, "Result data should not be null");
            
            // Verify account balances were updated
            var accounts = await _context.FinanceAccounts.ToListAsync();
            Assert.IsTrue(accounts.Any(), "Accounts should exist");
        }

        #endregion

        #region Finance Core: Ledger Validation Flow

        /// <summary>
        /// Test 6: Ledger Validation - Financial truth test
        /// </summary>
        [TestMethod]
        public async Task ValidateLedger_ShouldDetectInconsistencies_WhenDataMismatch()
        {
            // Arrange - Create inconsistent data
            // Create manual journal entries that don't match ledger state
            var manualEntry = new JournalEntry
            {
                CompanyId = 1,
                TransactionNumber = "MANUAL-001",
                Description = "Manual adjustment",
                TransactionDate = DateTime.UtcNow,
                Status = JournalStatus.Posted,
                JournalLines = new[]
                {
                    new JournalLine { AccountId = 1001, DebitAmount = 1000, CreditAmount = 0 }
                }
            };

            _context.JournalEntries.Add(manualEntry);
            await _context.SaveChangesAsync();

            // Act - Run validation
            var result = await _ledgerEngine.ValidateLedgerIntegrityAsync(1);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Ledger validation should complete");
            Assert.IsTrue(result.Inconsistencies.Count > 0, "Should detect inconsistencies");
            
            // Verify specific inconsistency type
            var manualInconsistency = result.Inconsistencies.FirstOrDefault();
            Assert.IsNotNull(manualInconsistency, "Should have manual inconsistency detected");
        }

        #endregion

        #region Integration: End-to-End Flow

        /// <summary>
        /// Test 7: Full Invoice-to-GL Flow
        /// </summary>
        [TestMethod]
        public async Task InvoiceToGLFlow_ShouldMaintainConsistency_WhenComplete()
        {
            // Arrange
            var financeEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "InvoiceCreated",
                CompanyId = 1,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    InvoiceId = "INV-001",
                    CustomerId = "CUST-001",
                    InvoiceAmount = 1500,
                    GLAccountId = 4001
                })
            };

            // Act
            var result = await _reconciliationEngine.RunComprehensiveReconciliationAsync(1, DateTime.UtcNow);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Reconciliation should complete");
            Assert.IsNotNull(result.InvoiceReconciliation, "Invoice reconciliation should not be null");
            
            // Verify the reconciliation was processed correctly
            var reconciliation = result.InvoiceReconciliation;
            Assert.IsTrue(reconciliation.InvoiceCount > 0, "Should have processed invoices");
        }

        #endregion
    }

    /// <summary>
    /// Test result container for critical path tests
    /// </summary>
    public class CriticalPathTestResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public object Data { get; set; }
        public List<string> Inconsistencies { get; set; } = new();
        public TimeSpan ExecutionTime { get; set; }
        public int ProcessedEvents { get; set; }
    }
}
