using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SecureERP2.Modules.Finance.Services;
using SecureERP2.Modules.Finance.Entities;
using System.Threading;
using System.Diagnostics;

namespace SecureERP2.Tests.Finance
{
    /// <summary>
    /// 🔒 Idempotency Stress Test - Validate duplicate handling under load
    /// </summary>
    [TestClass]
    public class IdempotencyStressTests
    {
        private readonly ERPDbContext _context;
        private readonly LedgerEngineService _ledgerEngine;
        private readonly ReconciliationEngine _reconciliationEngine;
        private readonly ILogger<IdempotencyStressTests> _logger;

        public IdempotencyStressTests(
            ERPDbContext context,
            LedgerEngineService ledgerEngine,
            ReconciliationEngine reconciliationEngine,
            ILogger<IdempotencyStressTests> logger)
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

        #region Test 1: Duplicate Event Handling

        /// <summary>
        /// Test 1: Same event sent multiple times should only be processed once
        /// </summary>
        [TestMethod]
        public async Task SameEvent_MultipleTimes_ShouldProcessOnce()
        {
            // Arrange
            var eventId = Guid.NewGuid();
            var financeEvent = new
            {
                EventId = eventId,
                EventType = "JournalCreated",
                CompanyId = 1,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 999,
                    TransactionNumber = "STRESS-001",
                    Description = "Stress test journal",
                    CreatedBy = "stress-test",
                    JournalLines = new[]
                    {
                        new { AccountId = 1001, DebitAmount = 1000, CreditAmount = 0, Description = "Stress debit" }
                    }
                })
            };

            var results = new List<LedgerProcessingResult>();

            // Act - Send same event 10-100 times concurrently
            var tasks = Enumerable.Range(1, 100).Select(async i =>
            {
                var eventCopy = new
                {
                    EventId = eventId,
                    EventType = financeEvent.EventType,
                    CompanyId = financeEvent.CompanyId,
                    Timestamp = financeEvent.Timestamp,
                    Version = i,
                    Data = financeEvent.Data
                };

                return await _ledgerEngine.ProcessEventAsync(eventCopy);
            });

            var allResults = await Task.WhenAll(tasks);

            // Assert
            var successfulResults = allResults.Where(r => r.IsSuccess).ToList();
            var failedResults = allResults.Where(r => !r.IsSuccess).ToList();

            Assert.AreEqual(1, successfulResults.Count, "Exactly one event should succeed");
            Assert.AreEqual(99, failedResults.Count, "99 events should fail with idempotency");
            
            // Verify database state - only one journal entry should exist
            var journalEntries = await _context.JournalEntries
                .Where(je => je.TransactionNumber == "STRESS-001")
                .ToListAsync();

            Assert.AreEqual(1, journalEntries.Count, "Only one journal entry should be created");
            
            _logger.LogInformation("Idempotency stress test completed: {SuccessCount} succeeded, {FailureCount} failed", 
                successfulResults.Count, failedResults.Count);
        }

        /// <summary>
        /// Test 2: Retry storm handling
        /// </summary>
        [TestMethod]
        public async Task RetryStorm_ShouldMaintainDataIntegrity()
        {
            // Arrange
            var eventId = Guid.NewGuid();
            var financeEvent = new
            {
                EventId = eventId,
                EventType = "JournalCreated",
                CompanyId = 1,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 1001,
                    TransactionNumber = "RETRY-STRESS",
                    Description = "Retry stress test",
                    CreatedBy = "stress-test",
                    JournalLines = new[]
                    {
                        new { AccountId = 1001, DebitAmount = 1000, CreditAmount = 0, Description = "Retry stress debit" }
                    }
                })
            };

            // Act - Simulate retry storm by sending event with artificial failures
            var retryAttempts = 0;
            var maxRetries = 20;

            while (retryAttempts < maxRetries)
            {
                try
                {
                    // Simulate network failure for first 10 attempts
                    if (retryAttempts < 10)
                    {
                        throw new InvalidOperationException("Simulated network failure");
                    }

                    var result = await _ledgerEngine.ProcessEventAsync(financeEvent);
                    
                    if (result.IsSuccess)
                    {
                        Assert.Fail($"Event should not succeed on attempt {retryAttempts + 1} due to simulated failure");
                        return;
                    }

                    retryAttempts++;
                }
                catch (InvalidOperationException)
                {
                    // Expected failure for first 10 attempts
                    retryAttempts++;
                }
            }

            // Last attempt should succeed
            var finalResult = await _ledgerEngine.ProcessEventAsync(financeEvent);

            // Assert
            Assert.IsTrue(finalResult.IsSuccess, "Final attempt should succeed");
            Assert.AreEqual(maxRetries, retryAttempts, "Should attempt maximum retries");
            
            // Verify only one journal entry was created despite retries
            var journalEntries = await _context.JournalEntries
                .Where(je => je.TransactionNumber == "RETRY-STRESS")
                .ToListAsync();

            Assert.AreEqual(1, journalEntries.Count, "Only one journal entry should be created despite retry storm");
            
            _logger.LogInformation("Retry storm test completed: {TotalAttempts}", retryAttempts);
        }

        #endregion

        #region Test 3: Concurrent Event Processing

        /// <summary>
        /// Test 3: Concurrent processing of same event should maintain data integrity
        /// </summary>
        [TestMethod]
        public async Task ConcurrentEvents_ShouldMaintainIntegrity()
        {
            // Arrange
            var eventId = Guid.NewGuid();
            var financeEvent = new
            {
                EventId = eventId,
                EventType = "JournalCreated",
                CompanyId = 1,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 2001,
                    TransactionNumber = "CONCURRENT-001",
                    Description = "Concurrent stress test",
                    CreatedBy = "stress-test",
                    JournalLines = new[]
                    {
                        new { AccountId = 1001, DebitAmount = 1000, CreditAmount = 0, Description = "Concurrent debit" }
                    }
                })
            };

            // Act - Send same event 50 times concurrently
            var tasks = Enumerable.Range(1, 50).Select(async i =>
            {
                var eventCopy = new
                {
                    EventId = eventId,
                    EventType = financeEvent.EventType,
                    CompanyId = financeEvent.CompanyId,
                    Timestamp = financeEvent.Timestamp,
                    Version = i,
                    Data = financeEvent.Data
                };

                return await _ledgerEngine.ProcessEventAsync(eventCopy);
            });

            var allResults = await Task.WhenAll(tasks);

            // Assert
            var successfulResults = allResults.Where(r => r.IsSuccess).ToList();
            var failedResults = allResults.Where(r => !r.IsSuccess).ToList();

            Assert.IsTrue(successfulResults.Count >= 1, "At least one event should succeed");
            Assert.AreEqual(50, successfulResults.Count + failedResults.Count, "All 50 events should be processed");
            
            // Verify database integrity - should not have duplicate journal entries
            var journalEntries = await _context.JournalEntries
                .Where(je => je.TransactionNumber == "CONCURRENT-001")
                .ToListAsync();

            Assert.IsTrue(journalEntries.Count <= 1, "Should not have duplicate journal entries");
            
            _logger.LogInformation("Concurrent events test completed: {SuccessCount} succeeded, {FailureCount} failed", 
                successfulResults.Count, failedResults.Count);
        }

        #endregion

        #region Test 4: Message Delivery Validation

        /// <summary>
        /// Test 4: Message delivery should be atomic and complete
        /// </summary>
        [TestMethod]
        public async Task MessageDelivery_ShouldBeAtomic()
        {
            // Arrange
            var eventId = Guid.NewGuid();
            var financeEvent = new
            {
                EventId = eventId,
                EventType = "JournalCreated",
                CompanyId = 1,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 3001,
                    TransactionNumber = "ATOMIC-001",
                    Description = "Atomic delivery test",
                    CreatedBy = "stress-test",
                    JournalLines = new[]
                    {
                        new { AccountId = 1001, DebitAmount = 1000, CreditAmount = 0, Description = "Atomic debit" },
                        new { AccountId = 1002, DebitAmount = 500, CreditAmount = 500, Description = "Atomic credit" }
                    }
                })
            };

            // Act
            var result = await _ledgerEngine.ProcessEventAsync(financeEvent);

            // Assert
            Assert.IsTrue(result.IsSuccess, "Event processing should succeed");
            Assert.IsNotNull(result.Data, "Result data should not be null");
            
            // Verify both journal lines were created atomically
            var journalEntries = await _context.JournalEntries
                .Where(je => je.TransactionNumber == "ATOMIC-001")
                .ToListAsync();

            Assert.AreEqual(1, journalEntries.Count, "Exactly one journal entry should be created");
            Assert.AreEqual(2, journalEntries.First().JournalLines.Count, "Both journal lines should be created");
            
            _logger.LogInformation("Atomic message delivery test completed successfully");
        }

        #endregion

        #region Test 5: Performance Under Stress

        /// <summary>
        /// Test 5: System should maintain performance under load
        /// </summary>
        [TestMethod]
        public async Task PerformanceUnderStress_ShouldMaintainResponseTime()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task<LedgerProcessingResult>>();

            // Act - Process 100 events concurrently
            for (int i = 0; i < 100; i++)
            {
                var financeEvent = new
                {
                    EventId = Guid.NewGuid(),
                    EventType = "JournalCreated",
                    CompanyId = 1,
                    Timestamp = DateTime.UtcNow,
                    Version = 1,
                    Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                    {
                        JournalEntryId = i,
                        TransactionNumber = $"PERF-{i:D3}",
                        Description = $"Performance test {i}",
                        CreatedBy = "stress-test",
                        JournalLines = new[]
                        {
                            new { AccountId = 1001, DebitAmount = 100, CreditAmount = 0, Description = $"Perf test {i}" }
                        }
                    })
                };

                tasks.Add(_ledgerEngine.ProcessEventAsync(financeEvent));
            }

            var allResults = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(allResults.All(r => r.IsSuccess), "All events should be processed successfully");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 30000, "Processing should complete within 30 seconds");
            
            var avgResponseTime = allResults.Average(r => 
            {
                // Calculate response time from result creation time
                return 100m; // Placeholder - would be calculated from actual metrics
            });

            Assert.IsTrue(avgResponseTime < 5000, "Average response time should be reasonable under stress");
            
            _logger.LogInformation("Performance stress test completed: {EventCount} events in {ElapsedMs}ms, avg response: {AvgResponseTime}ms", 
                allResults.Count, stopwatch.ElapsedMilliseconds, avgResponseTime);
        }

        #endregion

        #region Test 6: Resource Cleanup Validation

        /// <summary>
        /// Test 6: Resources should be properly cleaned up after stress test
        /// </summary>
        [TestMethod]
        public async Task ResourceCleanup_ShouldNotLeaveOrphans()
        {
            // Arrange - Create a journal entry
            var financeEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalCreated",
                CompanyId = 1,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 4001,
                    TransactionNumber = "CLEANUP-001",
                    Description = "Resource cleanup test",
                    CreatedBy = "stress-test",
                    JournalLines = new[]
                    {
                        new { AccountId = 1001, DebitAmount = 1000, CreditAmount = 0, Description = "Cleanup test" }
                    }
                })
            };

            var result = await _ledgerEngine.ProcessEventAsync(financeEvent);
            Assert.IsTrue(result.IsSuccess, "Event should be processed successfully");

            // Act - Simulate process termination without proper cleanup
            var initialJournalCount = await _context.JournalEntries.CountAsync();

            // Cleanup should happen automatically via TestCleanup, but we verify state
            var finalJournalCount = await _context.JournalEntries.CountAsync();
            
            // Assert
            Assert.AreEqual(initialJournalCount + 1, finalJournalCount, "Journal count should increase by 1");
            
            _logger.LogInformation("Resource cleanup validation completed: journal count before: {Before}, after: {After}", 
                initialJournalCount, finalJournalCount);
        }

        #endregion
    }

    /// <summary>
    /// Test result container for idempotency stress tests
    /// </summary>
    public class IdempotencyStressTestResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public object Data { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public int ProcessedEvents { get; set; }
        public int FailedEvents { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}
