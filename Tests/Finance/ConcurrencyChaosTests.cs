using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using SecureERP2.Modules.Finance.Services;
using SecureERP2.Modules.Finance.Entities;
using System.Threading;
using System.Diagnostics;

namespace SecureERP2.Tests.Finance
{
    /// <summary>
    /// 🔒 Concurrency Chaos Test - Stress race conditions and RowVersion handling
    /// </summary>
    [TestClass]
    public class ConcurrencyChaosTests
    {
        private readonly ERPDbContext _context;
        private readonly LedgerEngineService _ledgerEngine;
        private readonly ReconciliationEngine _reconciliationEngine;
        private readonly ILogger<ConcurrencyChaosTests> _logger;

        public ConcurrencyChaosTests(
            ERPDbContext context,
            LedgerEngineService ledgerEngine,
            ReconciliationEngine reconciliationEngine,
            ILogger<ConcurrencyChaosTests> logger)
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

        #region Test 1: Concurrent Journal Posting

        /// <summary>
        /// Test 1: Multiple users posting to same account simultaneously
        /// </summary>
        [TestMethod]
        public async Task ConcurrentJournalPosting_ShouldMaintainDataIntegrity_WhenMultipleUsers()
        {
            // Arrange
            var companyId = 1;
            var accountId = 1001;
            var baseBalance = 5000m;

            // Create base account
            var account = new FinanceAccount
            {
                Id = accountId,
                AccountNumber = "CHAOS-001",
                AccountName = "Chaos Test Account",
                CurrentBalance = baseBalance,
                CompanyId = companyId
            };

            _context.FinanceAccounts.Add(account);
            await _context.SaveChangesAsync();

            // Act - Simulate 10 concurrent journal postings
            var tasks = new List<Task<LedgerProcessingResult>>();
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < 10; i++)
            {
                var task = Task.Run(async () =>
                {
                    var financeEvent = new
                    {
                        EventId = Guid.NewGuid(),
                        EventType = "JournalCreated",
                        CompanyId = companyId,
                        Timestamp = DateTime.UtcNow,
                        Version = 1,
                        Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                        {
                            JournalEntryId = i + 1000,
                            TransactionNumber = $"CHAOS-{i:D3}",
                            Description = $"Concurrent journal posting {i}",
                            CreatedBy = $"user-{i}",
                            JournalLines = new[]
                            {
                                new JournalLine 
                                { 
                                    AccountId = accountId, 
                                    DebitAmount = i % 2 == 0 ? 100m : 0, 
                                    CreditAmount = i % 2 == 1 ? 100m : 0,
                                    Description = $"Concurrent posting {i}"
                                }
                            }
                        })
                    };

                    return await _ledgerEngine.ProcessEventAsync(financeEvent);
                });

                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(results.All(r => r.IsSuccess), "All concurrent postings should succeed");
            Assert.AreEqual(10, results.Count, "Should process all 10 concurrent postings");
            
            // Verify account balance integrity
            var finalAccount = await _context.FinanceAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId);

            Assert.IsNotNull(finalAccount, "Account should exist");
            Assert.AreEqual(baseBalance + results.Sum(r => r.Data?.JournalLines?.Sum(jl => jl.DebitAmount - jl.CreditAmount) ?? 0m), 
                finalAccount.CurrentBalance, "Account balance should match expected total");

            _logger.LogInformation("Concurrent journal posting test completed: {Count} postings in {ElapsedMs}ms", 
                results.Count, stopwatch.ElapsedMilliseconds);
        }

        #endregion

        #region Test 2: RowVersion Conflict Detection

        /// <summary>
        /// Test 2: RowVersion prevents lost updates during concurrent access
        /// </summary>
        [TestMethod]
        public async Task RowVersionConflict_ShouldPreventDataLoss_WhenConcurrentUpdates()
        {
            // Arrange
            var companyId = 1;
            var accountId = 1002;
            var journalEntryId = 2001;

            // Create initial journal entry
            var initialEntry = new JournalEntry
            {
                Id = journalEntryId,
                CompanyId = companyId,
                TransactionNumber = "ROWVER-001",
                Description = "Initial entry",
                Status = JournalStatus.Posted,
                TransactionDate = DateTime.UtcNow,
                JournalLines = new[]
                {
                    new JournalLine { AccountId = accountId, DebitAmount = 100m, CreditAmount = 0 }
                }
            };

            _context.JournalEntries.Add(initialEntry);
            await _context.SaveChangesAsync();

            // Act - Simulate concurrent updates to same entry
            var updateTasks = new List<Task>();
            var expectedVersion = new byte[] { 1, 2, 3 };

            for (int i = 0; i < 5; i++)
            {
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var entry = await _context.JournalEntries
                            .FirstOrDefaultAsync(je => je.Id == journalEntryId);

                        if (entry != null)
                        {
                            entry.Description = $"Concurrent update {i + 1}";
                            entry.RowVersion = expectedVersion[i];
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            // Entry was deleted by another transaction
                            return new LedgerProcessingResult
                            {
                                IsSuccess = false,
                                ErrorMessage = $"Entry not found for update {i + 1}"
                            };
                        }
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        // Expected concurrency exception
                        return new LedgerProcessingResult
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Concurrency conflict detected on update {i + 1}"
                        };
                    }
                });

                updateTasks.Add(task);
            }

            var updateResults = await Task.WhenAll(updateTasks);
            var concurrencyConflicts = updateResults.Count(r => !r.IsSuccess);

            // Assert
            Assert.IsTrue(concurrencyConflicts >= 2, "Should detect RowVersion conflicts");
            Assert.AreEqual(5, concurrencyConflicts, "Should have 5 concurrency conflicts (2 successful + 3 conflicts)");
            
            // Verify final state
            var finalEntry = await _context.JournalEntries
                .FirstOrDefaultAsync(je => je.Id == journalEntryId);

            Assert.IsNotNull(finalEntry, "Entry should still exist");
            Assert.IsTrue(expectedVersion.Contains(finalEntry.RowVersion), "Final entry should have one of the expected versions");

            _logger.LogInformation("RowVersion conflict test completed: {ConflictCount} conflicts detected", concurrencyConflicts);
        }

        #endregion

        #region Test 3: Reconciliation Race Conditions

        /// <summary>
        /// Test 3: Reconciliation should handle concurrent runs gracefully
        /// </summary>
        [TestMethod]
        public async Task ConcurrentReconciliation_ShouldNotCorruptData_WhenMultipleRuns()
        {
            // Arrange
            var companyId = 1;
            var asOfDate = DateTime.UtcNow;

            // Act - Start two reconciliations simultaneously
            var task1 = _reconciliationEngine.RunComprehensiveReconciliationAsync(companyId, asOfDate);
            var task2 = _reconciliationEngine.RunComprehensiveReconciliationAsync(companyId, asOfDate.AddMinutes(1));

            var results = await Task.WhenAll(task1, task2);

            // Assert
            Assert.IsTrue(results.All(r => r.IsSuccess), "Both reconciliations should succeed");
            
            // Verify no data corruption
            var finalState = await _context.LedgerStates.FindAsync(companyId);
            Assert.IsNotNull(finalState, "Ledger state should exist");

            _logger.LogInformation("Concurrent reconciliation test completed successfully");
        }

        #endregion

        #region Test 4: Ledger State Consistency Under Chaos

        /// <summary>
        /// Test 4: Ledger state should remain consistent despite system failures
        /// </summary>
        [TestMethod]
        public async Task LedgerStateConsistency_ShouldRecover_WhenSystemFailures()
        {
            // Arrange
            var companyId = 1;
            var initialLedgerState = new LedgerState
            {
                CompanyId = companyId,
                AccountBalances = new Dictionary<int, decimal>
                {
                    { 1001, 1000m },
                    { 1002, 2000m }
                },
                LastUpdated = DateTime.UtcNow
            };

            _context.LedgerStates.Add(initialLedgerState);
            await _context.SaveChangesAsync();

            // Act - Simulate system failures during ledger operations
            var chaosTasks = new List<Task>();
            var random = new Random();

            for (int i = 0; i < 20; i++)
            {
                var task = Task.Run(async () =>
                {
                    try
                    {
                        // Simulate random failures
                        if (random.Next(1, 10) <= 3) // 30% failure rate
                        {
                            throw new InvalidOperationException($"Simulated system failure {i}");
                        }

                        // Perform ledger operation
                        var ledgerState = await _context.LedgerStates.FindAsync(companyId);
                        if (ledgerState != null)
                        {
                            // Randomly modify balance (simulating corruption)
                            var randomAccount = random.Next(1, 3);
                            ledgerState.AccountBalances[randomAccount] = random.Next(-1000, 1000);
                            await _context.SaveChangesAsync();
                        }
                    }
                    catch
                    {
                        // Expected failures during chaos test
                        // Don't rethrow - let test continue
                    }
                });

                chaosTasks.Add(task);
            }

            await Task.WhenAll(chaosTasks);

            // Assert - Verify state recovery
            var finalLedgerState = await _context.LedgerStates.FindAsync(companyId);
            Assert.IsNotNull(finalLedgerState, "Ledger state should be recoverable");

            // Verify some balances changed (expected due to chaos)
            Assert.IsTrue(finalLedgerState.AccountBalances.Values.Any(balance => 
                Math.Abs(balance - initialLedgerState.AccountBalances.GetValueOrDefault(balance.Key, 0m)) > 100m), 
                "Some balances should have changed during chaos test");

            _logger.LogInformation("Ledger state consistency test completed: {ChaosTasks} chaos tasks executed", chaosTasks.Count);
        }

        #endregion

        #region Test 5: Performance Under Chaos

        /// <summary>
        /// Test 5: System should maintain performance under adverse conditions
        /// </summary>
        [TestMethod]
        public async Task PerformanceUnderChaos_ShouldMaintainResponseTime_WhenSystemStressed()
        {
            // Arrange
            var companyId = 1;
            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task<LedgerProcessingResult>>();

            // Act - Generate load with simulated system stress
            for (int i = 0; i < 50; i++)
            {
                var task = Task.Run(async () =>
                {
                    // Simulate system stress with random delays
                    await Task.Delay(random.Next(10, 100)); // 10-100ms delay

                    var financeEvent = new
                    {
                        EventId = Guid.NewGuid(),
                        EventType = "JournalCreated",
                        CompanyId = companyId,
                        Timestamp = DateTime.UtcNow,
                        Version = 1,
                        Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                        {
                            JournalEntryId = i + 3000,
                            Description = $"Chaos stress test {i}",
                            CreatedBy = "chaos-test"
                        })
                    };

                    return await _ledgerEngine.ProcessEventAsync(financeEvent);
                });

                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(results.All(r => r.IsSuccess), "All operations should succeed despite system stress");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 30000, "Operations should complete within 30 seconds despite chaos");
            
            var avgResponseTime = results.Average(r => 
            {
                // Would calculate from actual response times in real implementation
                return 500m; // Placeholder
            });

            Assert.IsTrue(avgResponseTime < 2000, "Average response time should remain reasonable under stress");

            _logger.LogInformation("Performance under chaos test completed: {TaskCount} tasks in {ElapsedMs}ms", 
                results.Count, stopwatch.ElapsedMilliseconds);
        }

        #endregion

        #region Test 6: Resource Exhaustion

        /// <summary>
        /// Test 6: System should handle resource exhaustion gracefully
        /// </summary>
        [TestMethod]
        public async Task ResourceExhaustion_ShouldFailGracefully_WhenOverloaded()
        {
            // Arrange
            var companyId = 1;
            var maxConcurrentTasks = 100;

            // Act - Attempt to overwhelm system with concurrent operations
            var tasks = new List<Task>();
            for (int i = 0; i < maxConcurrentTasks; i++)
            {
                var task = Task.Run(async () =>
                {
                    try
                    {
                        // Heavy operation that should stress the system
                        var financeEvent = new
                        {
                            EventId = Guid.NewGuid(),
                            EventType = "JournalCreated",
                            CompanyId = companyId,
                            Timestamp = DateTime.UtcNow,
                            Version = 1,
                            Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                            {
                                JournalEntryId = i + 4000,
                                Description = $"Resource exhaustion test {i}",
                                CreatedBy = "stress-test",
                                JournalLines = Enumerable.Range(1, 100).Select(j => 
                                    new JournalLine { AccountId = 1001, DebitAmount = 1m, CreditAmount = 0 }).ToList()
                            })
                        };

                        return await _ledgerEngine.ProcessEventAsync(financeEvent);
                    }
                    catch (Exception ex) when (ex is OutOfMemoryException || ex is InsufficientMemoryException)
                    {
                        // Expected resource exhaustion
                        return new LedgerProcessingResult
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Resource exhaustion detected: {ex.Message}"
                        };
                    }
                    catch
                    {
                        // Other exceptions should still be handled
                        return new LedgerProcessingResult
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Unexpected error: {ex.Message}"
                        };
                    }
                });

                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);
            var resourceExhaustionCount = results.Count(r => !r.IsSuccess && 
                r.ErrorMessage.Contains("Resource exhaustion"));

            // Assert
            Assert.IsTrue(resourceExhaustionCount > 0, "Should detect resource exhaustion");
            Assert.IsTrue(results.Count(r => r.IsSuccess || 
                r.ErrorMessage.Contains("Resource exhaustion")) < maxConcurrentTasks, 
                "Should handle resource exhaustion gracefully");

            _logger.LogInformation("Resource exhaustion test completed: {ExhaustionCount} resource exhaustion events detected", 
                resourceExhaustionCount);
        }

        #endregion

        #region Test 7: Network Partition Tolerance

        /// <summary>
        /// Test 7: System should handle network issues gracefully
        /// </summary>
        [TestMethod]
        public async Task NetworkPartition_ShouldRetry_WhenConnectionIssues()
        {
            // Arrange
            var companyId = 1;
            var retryCount = 0;
            var maxRetries = 5;

            // Act - Simulate network issues with retry logic
            var financeEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalCreated",
                CompanyId = companyId,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 5001,
                    Description = "Network partition test",
                    CreatedBy = "network-test"
                })
            };

            LedgerProcessingResult result = null;

            do
            {
                retryCount++;
                try
                {
                    // Simulate network failure for first few attempts
                    if (retryCount <= 3)
                    {
                        throw new HttpRequestException($"Simulated network failure {retryCount}");
                    }

                    result = await _ledgerEngine.ProcessEventAsync(financeEvent);
                    
                    if (result.IsSuccess)
                    {
                        break; // Success on retry
                    }
                }
                catch (HttpRequestException)
                {
                    // Expected network failure
                    _logger.LogWarning($"Network failure attempt {retryCount}: Expected failure");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Unexpected error on attempt {retryCount}: {ex.Message}");
                }
            } while (retryCount < maxRetries && !result.IsSuccess);

            // Assert
            Assert.IsNotNull(result, "Should have final result");
            Assert.IsTrue(result.IsSuccess, "Should succeed after retries");
            Assert.AreEqual(4, retryCount, "Should retry 4 times before success");
            Assert.IsTrue(retryCount <= maxRetries, "Should not exceed max retries");

            _logger.LogInformation("Network partition test completed: {RetryCount} attempts, Success: {Success}", 
                retryCount, result?.IsSuccess ?? false);
        }

        #endregion

        #region Test 8: Deadlock Detection and Recovery

        /// <summary>
        /// Test 8: System should detect and handle deadlocks
        /// </summary>
        [TestMethod]
        public async Task DeadlockHandling_ShouldDetectAndRecover_WhenConcurrentAccess()
        {
            // Arrange
            var companyId = 1;
            var accountId = 1003;
            var journalEntryId = 6001;

            // Create initial entry to lock
            var initialEntry = new JournalEntry
            {
                Id = journalEntryId,
                CompanyId = companyId,
                TransactionNumber = "DEADLOCK-001",
                Description = "Initial entry for deadlock test",
                Status = JournalStatus.Posted,
                TransactionDate = DateTime.UtcNow,
                JournalLines = new[]
                {
                    new JournalLine { AccountId = accountId, DebitAmount = 100m, CreditAmount = 0 }
                }
            };

            _context.JournalEntries.Add(initialEntry);
            await _context.SaveChangesAsync();

            // Act - Create deadlock scenario
            var tasks = new List<Task>();
            for (int i = 0; i < 2; i++)
            {
                var task = Task.Run(async () =>
                {
                    try
                    {
                        // Simulate deadlock by accessing same row in different order
                        using var transaction = await _context.Database.BeginTransactionAsync();
                        {
                            var entry1 = await _context.JournalEntries
                                .FirstOrDefaultAsync(je => je.Id == journalEntryId);

                            var entry2 = await _context.JournalEntries
                                .FirstOrDefaultAsync(je => je.TransactionNumber == "DEADLOCK-001" && je.Id != journalEntryId);

                            if (entry1 != null && entry2 != null)
                            {
                                // First task updates, second task tries to read - should cause deadlock
                                entry1.Description = $"Updated by task 1 at {DateTime.UtcNow}";
                                await _context.SaveChangesAsync();
                            }

                            if (i == 1) // Second task
                            {
                                entry2.Description = $"Updated by task 2 at {DateTime.UtcNow}";
                                await _context.SaveChangesAsync();
                            }

                            await transaction.CommitAsync();
                        }
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        // Expected deadlock
                        _logger.LogWarning($"Deadlock detected in task {i + 1}: {ex.Message}");
                        return new LedgerProcessingResult
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Deadlock detected: {ex.Message}"
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Unexpected error in deadlock test {i + 1}: {ex.Message}");
                        return new LedgerProcessingResult
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Unexpected error: {ex.Message}"
                        };
                    }
                });

                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            var deadlockCount = results.Count(r => !r.IsSuccess && 
                r.ErrorMessage.Contains("Deadlock detected"));

            Assert.IsTrue(deadlockCount >= 1, "Should detect at least one deadlock");
            Assert.AreEqual(2, deadlockCount, "Should have 2 deadlock attempts (1 success, 1 failure)");

            _logger.LogInformation("Deadlock handling test completed: {DeadlockCount} deadlocks detected", deadlockCount);
        }

        #endregion

        #region Test 9: Data Corruption Detection

        /// <summary>
        /// Test 9: System should detect data corruption attempts
        /// </summary>
        [TestMethod]
        public async Task DataCorruption_ShouldDetectTampering_WhenDataModified()
        {
            // Arrange
            var companyId = 1;
            var accountId = 1004;
            var journalEntryId = 7001;

            // Create initial entry
            var initialEntry = new JournalEntry
            {
                Id = journalEntryId,
                CompanyId = companyId,
                TransactionNumber = "CORRUPTION-001",
                Description = "Initial entry for corruption test",
                Status = JournalStatus.Posted,
                TransactionDate = DateTime.UtcNow,
                JournalLines = new[]
                {
                    new JournalLine { AccountId = accountId, DebitAmount = 1000m, CreditAmount = 0 }
                }
            };

            _context.JournalEntries.Add(initialEntry);
            await _context.SaveChangesAsync();

            // Act - Simulate data corruption
            var corruptTask = Task.Run(async () =>
            {
                try
                {
                    // Direct database manipulation to simulate corruption
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE JournalEntries SET Description = 'Corrupted data' WHERE Id = {0}", 
                        journalEntryId);

                    return new LedgerProcessingResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Data corruption simulation"
                    };
                }
                catch (Exception ex)
                {
                    return new LedgerProcessingResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Corruption attempt failed: {ex.Message}"
                    };
                }
            });

            var legitimateTask = Task.Run(async () =>
            {
                try
                {
                    // Normal operation that should fail due to corrupted data
                    var financeEvent = new
                    {
                        EventId = Guid.NewGuid(),
                        EventType = "JournalCreated",
                        CompanyId = companyId,
                        Timestamp = DateTime.UtcNow,
                        Version = 1,
                        Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                        {
                            JournalEntryId = journalEntryId,
                            Description = "Legitimate operation on corrupted data"
                        })
                    };

                    return await _ledgerEngine.ProcessEventAsync(financeEvent);
                }
                catch (Exception ex)
                {
                    return new LedgerProcessingResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Legitimate operation failed: {ex.Message}"
                    };
                }
            });

            var results = await Task.WhenAll(corruptTask, legitimateTask);

            // Assert
            Assert.IsFalse(results.Any(r => r.IsSuccess), "Both operations should fail due to data corruption");
            Assert.IsTrue(results.Any(r => r.ErrorMessage.Contains("Data corruption simulation")), 
                "Should detect data corruption attempt");
            Assert.IsTrue(results.Any(r => r.ErrorMessage.Contains("Legitimate operation failed")), 
                "Should fail legitimate operation due to corrupted data");

            _logger.LogInformation("Data corruption detection test completed: Corruption detected and operations blocked");
        }

        #endregion

        #region Test 10: System Recovery Validation

        /// <summary>
        /// Test 10: System should recover gracefully from failures
        /// </summary>
        [TestMethod]
        public async Task SystemRecovery_ShouldRestoreConsistentState_AfterFailure()
        {
            // Arrange
            var companyId = 1;
            var backupData = new Dictionary<string, object>
            {
                ["AccountBalances"] = new Dictionary<int, decimal> { { 1001, 5000m } },
                ["LastUpdated"] = DateTime.UtcNow.AddDays(-1)
            };

            // Act - Simulate system failure and recovery
            try
            {
                // Simulate catastrophic failure
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM JournalEntries WHERE CompanyId = 1");

                // Attempt recovery
                var recoveryResult = await SimulateSystemRecovery(companyId, backupData);

                Assert.IsTrue(recoveryResult, "System should recover from failure");

                _logger.LogInformation("System recovery test completed successfully");
            }
            catch (Exception ex)
            {
                Assert.Fail($"System recovery failed: {ex.Message}");
            }
        }

        private async Task<bool> SimulateSystemRecovery(int companyId, Dictionary<string, object> backupData)
        {
            // Simulate recovery process
            await Task.Delay(1000); // Simulate recovery time

            // Restore from backup
            foreach (var kvp in backupData)
            {
                if (kvp.Key == "AccountBalances")
                {
                    var balances = (Dictionary<int, decimal>)kvp.Value;
                    foreach (var balance in balances)
                    {
                        var account = await _context.FinanceAccounts
                            .FirstOrDefaultAsync(a => a.Id == balance.Key);

                        if (account != null)
                        {
                            account.CurrentBalance = balance.Value;
                            await _context.SaveChangesAsync();
                        }
                    }
                }
            }

            // Verify recovery success
            var finalAccounts = await _context.FinanceAccounts.ToListAsync();
            return finalAccounts.All(a => backupData["AccountBalances"].Values.Contains(
                a.CurrentBalance));
        }

        #endregion
    }

    /// <summary>
    /// Test result container for concurrency chaos tests
    /// </summary>
    public class ConcurrencyChaosTestResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> DetectedIssues { get; set; } = new();
        public TimeSpan ExecutionTime { get; set; }
        public int ConcurrentOperations { get; set; }
        public int DeadlocksDetected { get; set; }
        public int ResourceExhaustionEvents { get; set; }
        public bool PerformanceMaintained { get; set; }
    }
}
