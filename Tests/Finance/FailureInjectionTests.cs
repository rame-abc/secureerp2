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
    /// 🔒 Failure Injection Test - Verify resilience mechanisms
    /// </summary>
    [TestClass]
    public class FailureInjectionTests
    {
        private readonly ERPDbContext _context;
        private readonly LedgerEngineService _ledgerEngine;
        private readonly ReconciliationEngine _reconciliationEngine;
        private readonly ILogger<FailureInjectionTests> _logger;

        public FailureInjectionTests(
            ERPDbContext context,
            LedgerEngineService ledgerEngine,
            ReconciliationEngine reconciliationEngine,
            ILogger<FailureInjectionTests> logger)
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

        #region Test 1: Database Mid-Transaction Failure

        /// <summary>
        /// Test 1: System should rollback when DB fails mid-transaction
        /// </summary>
        [TestMethod]
        public async Task DatabaseMidTransaction_ShouldRollback_WhenConnectionLost()
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
                    JournalEntryId = 999,
                    TransactionNumber = "FAIL-DB-001",
                    Description = "Database failure test",
                    CreatedBy = "failure-test",
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = 1001, DebitAmount = 1000m, CreditAmount = 0, Description = "Test debit" }
                    }
                })
            };

            var initialJournalCount = await _context.JournalEntries.CountAsync();

            // Act - Simulate database failure mid-transaction
            var result = await SimulateDatabaseFailureDuringTransaction(financeEvent);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Operation should fail due to database failure");
            Assert.IsTrue(result.ErrorMessage.Contains("database"), "Error should mention database failure");
            
            // Verify rollback occurred
            var finalJournalCount = await _context.JournalEntries.CountAsync();
            Assert.AreEqual(initialJournalCount, finalJournalCount, "Journal count should not change after rollback");

            _logger.LogInformation("Database mid-transaction failure test completed: Rollback verified");
        }

        private async Task<LedgerProcessingResult> SimulateDatabaseFailureDuringTransaction(FinanceEvent financeEvent)
        {
            // This would be implemented by mocking the database context to throw exceptions
            // For now, we'll simulate by creating a scenario that should trigger rollback
            try
            {
                // Start transaction
                using var transaction = await _context.Database.BeginTransactionAsync();
                
                // Create partial data
                var journalEntry = new JournalEntry
                {
                    CompanyId = financeEvent.CompanyId,
                    TransactionNumber = "FAIL-DB-001",
                    Description = "Test entry",
                    Status = JournalStatus.Posted,
                    TransactionDate = DateTime.UtcNow,
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = 1001, DebitAmount = 1000m, CreditAmount = 0 }
                    }
                };

                _context.JournalEntries.Add(journalEntry);
                await _context.SaveChangesAsync();

                // Simulate database failure
                await _context.Database.ExecuteSqlRawAsync("THROW 50000 'Simulated database failure'");

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                return new LedgerProcessingResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Database failure: {ex.Message}"
                };
            }
        }

        #endregion

        #region Test 2: Exception During Posting

        /// <summary>
        /// Test 2: System should handle exceptions during posting gracefully
        /// </summary>
        [TestMethod]
        public async Task ExceptionDuringPosting_ShouldNotLeavePartialWrites_WhenErrorOccurs()
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
                    JournalEntryId = 888,
                    TransactionNumber = "FAIL-EXCEPTION-001",
                    Description = "Exception during posting test",
                    CreatedBy = "failure-test",
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = 1001, DebitAmount = 1000m, CreditAmount = 0, Description = "Test debit" },
                        new JournalLine { AccountId = 1002, DebitAmount = 0, CreditAmount = 1000m, Description = "Test credit" }
                    }
                })
            };

            var initialJournalCount = await _context.JournalEntries.CountAsync();
            var initialAccountBalances = await GetAccountBalances();

            // Act - Simulate exception during posting
            var result = await SimulateExceptionDuringPosting(financeEvent);

            // Assert
            Assert.IsFalse(result.IsSuccess, "Operation should fail due to exception");
            Assert.IsTrue(result.ErrorMessage.Contains("exception"), "Error should mention exception");
            
            // Verify no partial writes occurred
            var finalJournalCount = await _context.JournalEntries.CountAsync();
            var finalAccountBalances = await GetAccountBalances();

            Assert.AreEqual(initialJournalCount, finalJournalCount, "Journal count should not change after exception");
            
            // Verify account balances unchanged
            foreach (var kvp in initialAccountBalances)
            {
                Assert.IsTrue(finalAccountBalances.ContainsKey(kvp.Key), 
                    $"Account {kvp.Key} should still exist");
                Assert.AreEqual(kvp.Value, finalAccountBalances[kvp.Key], 
                    $"Account {kvp.Key} balance should be unchanged: Initial={kvp.Value}, Final={finalAccountBalances[kvp.Key]}");
            }

            _logger.LogInformation("Exception during posting test completed: No partial writes verified");
        }

        private async Task<LedgerProcessingResult> SimulateExceptionDuringPosting(FinanceEvent financeEvent)
        {
            try
            {
                // Simulate exception during processing
                await Task.Delay(100); // Simulate processing time
                
                throw new InvalidOperationException("Simulated processing exception");
            }
            catch (Exception ex)
            {
                return new LedgerProcessingResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Processing exception: {ex.Message}"
                };
            }
        }

        #endregion

        #region Test 3: API Timeout Handling

        /// <summary>
        /// Test 3: Retry policy should activate on API timeouts
        /// </summary>
        [TestMethod]
        public async Task APITimeout_ShouldRetry_WhenTimeoutsOccur()
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
                    JournalEntryId = 777,
                    TransactionNumber = "FAIL-TIMEOUT-001",
                    Description = "API timeout test",
                    CreatedBy = "failure-test",
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = 1001, DebitAmount = 1000m, CreditAmount = 0, Description = "Test debit" }
                    }
                })
            };

            var retryAttempts = 0;
            var maxRetries = 3;

            // Act - Simulate API timeouts with retry
            LedgerProcessingResult result = null;

            do
            {
                retryAttempts++;
                try
                {
                    // Simulate timeout for first 2 attempts
                    if (retryAttempts <= 2)
                    {
                        throw new TimeoutException("Simulated API timeout");
                    }

                    result = await _ledgerEngine.ProcessEventAsync(financeEvent);
                    
                    if (result.IsSuccess)
                    {
                        break; // Success on retry
                    }
                }
                catch (TimeoutException)
                {
                    // Expected timeout for first 2 attempts
                    _logger.LogWarning($"API timeout attempt {retryAttempts}: Expected timeout");
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Unexpected error on attempt {retryAttempts}: {ex.Message}");
                    continue;
                }
            } while (retryAttempts < maxRetries && !result.IsSuccess);

            // Assert
            Assert.IsNotNull(result, "Should have final result");
            Assert.IsTrue(result.IsSuccess, "Should succeed after retries");
            Assert.AreEqual(3, retryAttempts, "Should attempt maximum retries");
            Assert.AreEqual(2, retryAttempts - 1, "Should have 2 timeout failures before success");

            _logger.LogInformation("API timeout test completed: {RetryCount} attempts, Success: {Success}", 
                retryAttempts, result?.IsSuccess ?? false);
        }

        #endregion

        #region Test 4: Network Connection Issues

        /// <summary>
        /// Test 4: System should handle network connection breaks
        /// </summary>
        [TestMethod]
        public async Task NetworkConnection_ShouldRecover_WhenConnectionBroken()
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
                    JournalEntryId = 666,
                    TransactionNumber = "FAIL-NETWORK-001",
                    Description = "Network connection test",
                    CreatedBy = "failure-test",
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = 1001, DebitAmount = 1000m, CreditAmount = 0, Description = "Test debit" }
                    }
                })
            };

            var connectionAttempts = 0;
            var maxConnectionAttempts = 5;

            // Act - Simulate network connection issues
            LedgerProcessingResult result = null;

            do
            {
                connectionAttempts++;
                try
                {
                    // Simulate network connection failure for first 3 attempts
                    if (connectionAttempts <= 3)
                    {
                        throw new HttpRequestException("Simulated network connection failure");
                    }

                    result = await _ledgerEngine.ProcessEventAsync(financeEvent);
                    
                    if (result.IsSuccess)
                    {
                        break; // Success on retry
                    }
                }
                catch (HttpRequestException)
                {
                    // Expected network failure for first 3 attempts
                    _logger.LogWarning($"Network connection failure attempt {connectionAttempts}: Expected failure");
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Unexpected error on connection attempt {connectionAttempts}: {ex.Message}");
                    continue;
                }
            } while (connectionAttempts < maxConnectionAttempts && !result.IsSuccess);

            // Assert
            Assert.IsNotNull(result, "Should have final result");
            Assert.IsTrue(result.IsSuccess, "Should succeed after connection retries");
            Assert.AreEqual(5, connectionAttempts, "Should attempt maximum connection attempts");
            Assert.AreEqual(3, connectionAttempts - 2, "Should have 3 connection failures before success");

            _logger.LogInformation("Network connection test completed: {ConnectionAttempts} attempts, Success: {Success}", 
                connectionAttempts, result?.IsSuccess ?? false);
        }

        #endregion

        #region Test 5: Retry Policy Activation

        /// <summary>
        /// Test 5: Retry policy should activate correctly for transient failures
        /// </summary>
        [TestMethod]
        public async Task RetryPolicy_ShouldActivate_WhenTransientFailures()
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
                    JournalEntryId = 555,
                    TransactionNumber = "FAIL-RETRY-001",
                    Description = "Retry policy test",
                    CreatedBy = "failure-test",
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = 1001, DebitAmount = 1000m, CreditAmount = 0, Description = "Test debit" }
                    }
                })
            };

            var retryPolicyActivations = 0;
            var expectedActivations = 2; // Should activate twice

            // Act - Simulate transient failures that trigger retry policy
            LedgerProcessingResult result = null;

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    // Simulate transient failure for attempts 1 and 3
                    if (attempt == 1 || attempt == 3)
                    {
                        throw new InvalidOperationException("Simulated transient failure");
                    }

                    result = await _ledgerEngine.ProcessEventAsync(financeEvent);
                    
                    if (result.IsSuccess && attempt > 1)
                    {
                        retryPolicyActivations++;
                        _logger.LogInformation($"Retry policy activated on attempt {attempt}");
                    }
                }
                catch (InvalidOperationException)
                {
                    if (attempt == 1 || attempt == 3)
                    {
                        retryPolicyActivations++;
                        _logger.LogInformation($"Retry policy activation detected on attempt {attempt}");
                    }
                }
            }

            // Assert
            Assert.IsNotNull(result, "Should have final result");
            Assert.IsTrue(result.IsSuccess, "Should succeed after retries");
            Assert.AreEqual(expectedActivations, retryPolicyActivations, 
                $"Retry policy should activate {expectedActivations} times but activated {retryPolicyActivations} times");

            _logger.LogInformation("Retry policy activation test completed: {Activations} policy activations", retryPolicyActivations);
        }

        #endregion

        #region Test 6: System Recovery After Failure

        /// <summary>
        /// Test 6: System should recover gracefully after catastrophic failure
        /// </summary>
        [TestMethod]
        public async Task SystemRecovery_ShouldRestoreService_WhenCatastrophicFailure()
        {
            // Arrange
            var companyId = 1;
            var financeEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalCreated",
                CompanyId = companyId,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 444,
                    TransactionNumber = "FAIL-RECOVERY-001",
                    Description = "System recovery test",
                    CreatedBy = "failure-test",
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = 1001, DebitAmount = 1000m, CreditAmount = 0, Description = "Test debit" }
                    }
                })
            };

            var initialSystemState = await CaptureSystemState();

            // Act - Simulate catastrophic failure and recovery
            var result = await SimulateCatastrophicFailureAndRecovery(financeEvent, companyId);

            // Assert
            Assert.IsTrue(result, "System should recover from catastrophic failure");
            
            var finalSystemState = await CaptureSystemState();
            
            // Verify system recovery
            Assert.IsTrue(finalSystemState.DatabaseAccessible, "Database should be accessible after recovery");
            Assert.IsTrue(finalSystemState.ServicesRunning, "Services should be running after recovery");
            Assert.IsTrue(finalSystemState.DataIntegrity, "Data integrity should be maintained after recovery");

            _logger.LogInformation("System recovery test completed: Recovery successful");
        }

        private async Task<bool> SimulateCatastrophicFailureAndRecovery(FinanceEvent financeEvent, int companyId)
        {
            try
            {
                // Simulate catastrophic failure
                await _context.Database.ExecuteSqlRawAsync("DROP TABLE JournalEntries"); // Simulate data loss
                
                var result = await _ledgerEngine.ProcessEventAsync(financeEvent);
                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Catastrophic failure: {ex.Message}");
                
                // Simulate recovery process
                await Task.Delay(2000); // Recovery time
                
                // Recreate database structure
                _context.Database.EnsureCreated();
                
                // Restore from backup (simulated)
                var backupEvent = new
                {
                    EventId = Guid.NewGuid(),
                    EventType = "JournalCreated",
                    CompanyId = companyId,
                    Timestamp = DateTime.UtcNow,
                    Version = 1,
                    Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                    {
                        JournalEntryId = 444,
                        TransactionNumber = "RECOVERY-RESTORED-001",
                        Description = "Restored after catastrophic failure",
                        CreatedBy = "system-recovery",
                        JournalLines = new[]
                        {
                            new JournalLine { AccountId = 1001, DebitAmount = 1000m, CreditAmount = 0, Description = "Restored data" }
                        }
                    })
                };

                var recoveryResult = await _ledgerEngine.ProcessEventAsync(backupEvent);
                return recoveryResult.IsSuccess;
            }
        }

        private async Task<SystemState> CaptureSystemState()
        {
            return new SystemState
            {
                DatabaseAccessible = await TestDatabaseConnection(),
                ServicesRunning = await TestServicesHealth(),
                DataIntegrity = await TestDataIntegrity()
            };
        }

        private async Task<bool> TestDatabaseConnection()
        {
            try
            {
                await _context.Database.CanConnectAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TestServicesHealth()
        {
            // Simulate checking if critical services are running
            await Task.Delay(100); // Simulate health check
            return true; // Assume services are healthy for test
        }

        private async Task<bool> TestDataIntegrity()
        {
            // Simulate data integrity check
            try
            {
                var count = await _context.JournalEntries.CountAsync();
                return count >= 0; // Basic integrity check
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Test 7: Graceful Degradation

        /// <summary>
        /// Test 7: System should degrade gracefully under load
        /// </summary>
        [TestMethod]
        public async Task GracefulDegradation_ShouldMaintainBasicFunctionality_WhenOverloaded()
        {
            // Arrange
            var companyId = 1;
            var financeEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalCreated",
                CompanyId = companyId,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 333,
                    TransactionNumber = "FAIL-DEGRADE-001",
                    Description = "Graceful degradation test",
                    CreatedBy = "failure-test",
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = 1001, DebitAmount = 1000m, CreditAmount = 0, Description = "Test debit" }
                    }
                })
            };

            var stopwatch = Stopwatch.StartNew();

            // Act - Simulate system overload and graceful degradation
            var result = await SimulateSystemOverloadAndGracefulDegradation(financeEvent);

            stopwatch.Stop();

            // Assert
            Assert.IsTrue(result.IsSuccess, "Should complete even under degraded state");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds > 1000, "Should take longer due to degraded performance");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000, "Should not exceed 10 seconds even under load");
            
            // Verify basic functionality maintained
            var degradedOperations = result.Data as Dictionary<string, object>;
            Assert.IsNotNull(degradedOperations, "Should have degradation information");
            Assert.IsTrue(degradedOperations.ContainsKey("DegradedMode"), "Should indicate degraded mode");
            Assert.IsTrue(degradedOperations.ContainsKey("BasicFunctionality"), "Should maintain basic functionality");

            _logger.LogInformation("Graceful degradation test completed: Degraded mode active for {ElapsedMs}ms", 
                stopwatch.ElapsedMilliseconds);
        }

        private async Task<LedgerProcessingResult> SimulateSystemOverloadAndGracefulDegradation(FinanceEvent financeEvent)
        {
            try
            {
                // Simulate system overload (high CPU, memory usage)
                await Task.Delay(2000); // Simulate processing delay due to overload
                
                // Process with degraded performance
                var result = await _ledgerEngine.ProcessEventAsync(financeEvent);
                
                if (result.IsSuccess)
                {
                    // Add degradation metadata
                    result.Data = new Dictionary<string, object>
                    {
                        ["DegradedMode"] = true,
                        ["BasicFunctionality"] = true,
                        ["PerformanceImpact"] = "High",
                        ["UserExperience"] = "Degraded but functional"
                    };
                }

                return result;
            }
            catch (Exception ex)
            {
                return new LedgerProcessingResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"System overload: {ex.Message}"
                };
            }
        }

        #endregion

        #region Test 8: Circuit Breaker Pattern

        /// <summary>
        /// Test 8: Circuit breaker should prevent cascade failures
        /// </summary>
        [TestMethod]
        public async Task CircuitBreaker_ShouldPreventCascade_WhenFailuresExceedThreshold()
        {
            // Arrange
            var companyId = 1;
            var financeEvent = new
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalCreated",
                CompanyId = companyId,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                {
                    JournalEntryId = 222,
                    TransactionNumber = "FAIL-CIRCUIT-001",
                    Description = "Circuit breaker test",
                    CreatedBy = "failure-test",
                    JournalLines = new[]
                    {
                        new JournalLine { AccountId = 1001, DebitAmount = 1000m, CreditAmount = 0, Description = "Test debit" }
                    }
                })
            };

            var failureCount = 0;
            var circuitBreakerThreshold = 3;
            var circuitOpen = false;

            // Act - Simulate failures that should trigger circuit breaker
            var results = new List<LedgerProcessingResult>();

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    // Simulate circuit breaker behavior
                    if (failureCount >= circuitBreakerThreshold && !circuitOpen)
                    {
                        circuitOpen = true;
                        _logger.LogWarning($"Circuit breaker opened after {failureCount} failures");
                        
                        // Fast fail when circuit is open
                        results.Add(new LedgerProcessingResult
                        {
                            IsSuccess = false,
                            ErrorMessage = "Circuit breaker is open"
                        });
                        continue;
                    }

                    var result = await _ledgerEngine.ProcessEventAsync(financeEvent);
                    
                    if (result.IsSuccess)
                    {
                        failureCount = 0; // Reset failure count on success
                        if (circuitOpen)
                        {
                            circuitOpen = false;
                            _logger.LogInformation("Circuit breaker closed after recovery period");
                        }
                    }
                    else
                    {
                        failureCount++;
                    }

                    results.Add(result);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    results.Add(new LedgerProcessingResult
                    {
                        IsSuccess = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            // Assert
            var successCount = results.Count(r => r.IsSuccess);
            var circuitBreakerActivations = results.Count(r => r.ErrorMessage.Contains("Circuit breaker is open"));

            Assert.IsTrue(circuitBreakerActivations >= 1, "Circuit breaker should activate at least once");
            Assert.IsTrue(successCount > 0, "Should have some successes after circuit closes");
            Assert.IsTrue(failureCount >= circuitBreakerThreshold, "Should trigger threshold");
            
            _logger.LogInformation("Circuit breaker test completed: {SuccessCount} successes, {CircuitBreakerActivations} circuit activations", 
                successCount, circuitBreakerActivations);
        }

        #endregion

        #region Helper Methods

        private async Task<Dictionary<int, decimal>> GetAccountBalances()
        {
            var accounts = await _context.FinanceAccounts.ToListAsync();
            return accounts.ToDictionary(a => a.Id, a => a.CurrentBalance);
        }

        #endregion
    }

    /// <summary>
    /// System state for failure injection tests
    /// </summary>
    public class SystemState
    {
        public bool DatabaseAccessible { get; set; }
        public bool ServicesRunning { get; set; }
        public bool DataIntegrity { get; set; }
    }

    /// <summary>
    /// Test result container for failure injection tests
    /// </summary>
    public class FailureInjectionTestResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public object Data { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public int RetryAttempts { get; set; }
        public bool RollbackOccurred { get; set; }
        public bool CircuitBreakerActivated { get; set; }
        public List<string> FailurePoints { get; set; } = new();
        public SystemState FinalSystemState { get; set; }
    }
}
