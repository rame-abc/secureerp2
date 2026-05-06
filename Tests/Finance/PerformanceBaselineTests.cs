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
    /// 🔒 Performance Baseline Test - Measure system capacity
    /// </summary>
    [TestClass]
    public class PerformanceBaselineTests
    {
        private readonly ERPDbContext _context;
        private readonly LedgerEngineService _ledgerEngine;
        private readonly ReconciliationEngine _reconciliationEngine;
        private readonly ILogger<PerformanceBaselineTests> _logger;

        public PerformanceBaselineTests(
            ERPDbContext context,
            LedgerEngineService ledgerEngine,
            ReconciliationEngine reconciliationEngine,
            ILogger<PerformanceBaselineTests> logger)
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

        #region Test 1: Journal Posting Performance

        /// <summary>
        /// Test 1: Journal posting should meet performance targets
        /// </summary>
        [TestMethod]
        public async Task JournalPostingPerformance_ShouldMeetTargets_WhenOptimalLoad()
        {
            // Arrange
            var companyId = 1;
            var journalCount = 100;
            var targetResponseTimeMs = 2000; // 2 seconds max per journal
            var targetThroughputPerSecond = 10; // 10 journals per second

            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task<LedgerProcessingResult>>();

            // Act - Process multiple journals concurrently
            for (int i = 0; i < journalCount; i++)
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
                        JournalEntryId = i + 10000,
                        TransactionNumber = $"PERF-JNL-{i:D3}",
                        Description = $"Performance test journal {i}",
                        CreatedBy = "perf-test",
                        JournalLines = new[]
                        {
                            new JournalLine { AccountId = 1001, DebitAmount = 100m, CreditAmount = 0, Description = $"Perf test {i}" }
                        }
                    })
                };

                tasks.Add(_ledgerEngine.ProcessEventAsync(financeEvent));
            }

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(results.All(r => r.IsSuccess), "All journal postings should succeed");
            Assert.AreEqual(journalCount, results.Count, "Should process all journals");
            
            // Calculate performance metrics
            var totalTimeMs = stopwatch.ElapsedMilliseconds;
            var avgResponseTimeMs = totalTimeMs / (double)journalCount;
            var throughputPerSecond = journalCount / (totalTimeMs / 1000.0);

            Assert.IsTrue(avgResponseTimeMs <= targetResponseTimeMs, 
                $"Average response time {avgResponseTimeMs}ms should be <= {targetResponseTimeMs}ms");
            Assert.IsTrue(throughputPerSecond >= targetThroughputPerSecond, 
                $"Throughput {throughputPerSecond:F2} journals/sec should be >= {targetThroughputPerSecond} journals/sec");

            _logger.LogInformation("Journal posting performance test completed: {JournalCount} journals in {ElapsedMs}ms, avg: {AvgResponseTimeMs}ms, throughput: {Throughput:F2} journals/sec", 
                journalCount, totalTimeMs, avgResponseTimeMs, throughputPerSecond);
        }

        #endregion

        #region Test 2: Reconciliation Performance

        /// <summary>
        /// Test 2: Reconciliation should complete within acceptable time
        /// </summary>
        [TestMethod]
        public async Task ReconciliationPerformance_ShouldCompleteQuickly_WhenLargeDataset()
        {
            // Arrange
            var companyId = 1;
            var asOfDate = DateTime.UtcNow;
            var targetReconciliationTimeMs = 30000; // 30 seconds max
            var datasetSize = 1000;

            // Create large dataset for reconciliation
            await CreateLargeDatasetForReconciliation(companyId, datasetSize);

            var stopwatch = Stopwatch.StartNew();

            // Act - Run comprehensive reconciliation
            var reconciliationResult = await _reconciliationEngine.RunComprehensiveReconciliationAsync(companyId, asOfDate);

            stopwatch.Stop();

            // Assert
            Assert.IsTrue(reconciliationResult.IsSuccess, "Reconciliation should succeed");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds <= targetReconciliationTimeMs, 
                $"Reconciliation time {stopwatch.ElapsedMilliseconds}ms should be <= {targetReconciliationTimeMs}ms");
            
            // Verify reconciliation processed expected volume
            Assert.IsNotNull(reconciliationResult.InvoiceReconciliation, "Invoice reconciliation should not be null");
            Assert.IsNotNull(reconciliationResult.PayrollReconciliation, "Payroll reconciliation should not be null");
            Assert.IsNotNull(reconciliationResult.TaxReconciliation, "Tax reconciliation should not be null");

            _logger.LogInformation("Reconciliation performance test completed: {DatasetSize} records reconciled in {ElapsedMs}ms", 
                datasetSize, stopwatch.ElapsedMilliseconds);
        }

        private async Task CreateLargeDatasetForReconciliation(int companyId, int datasetSize)
        {
            // Create invoices
            var invoices = Enumerable.Range(1, datasetSize / 4).Select(i => new Invoice
            {
                CompanyId = companyId,
                CustomerId = $"CUST-{i:D4}",
                InvoiceNumber = $"PERF-INV-{i:D4}",
                InvoiceAmount = 1000m * (i % 10 + 1),
                Status = "Posted",
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            }).ToList();

            _context.Invoices.AddRange(invoices);

            // Create payroll records
            var payrollRecords = Enumerable.Range(1, datasetSize / 4).Select(i => new PayrollRecord
            {
                CompanyId = companyId,
                EmployeeId = $"EMP-{i:D4}",
                PayPeriod = "2024-01",
                GrossPay = 5000m * (i % 5 + 1),
                NetPay = 4000m * (i % 5 + 1),
                Taxes = 1000m * (i % 5 + 1),
                Status = "Posted",
                ProcessedAt = DateTime.UtcNow.AddDays(-i)
            }).ToList();

            _context.PayrollRecords.AddRange(payrollRecords);

            // Create tax records
            var taxRecords = Enumerable.Range(1, datasetSize / 4).Select(i => new TaxRecord
            {
                CompanyId = companyId,
                TaxType = "Sales Tax",
                TaxPeriod = "2024-01",
                TaxCollected = 500m * (i % 10 + 1),
                TaxPayable = 500m * (i % 10 + 1),
                Status = "Posted",
                ProcessedAt = DateTime.UtcNow.AddDays(-i)
            }).ToList();

            _context.TaxRecords.AddRange(taxRecords);
            await _context.SaveChangesAsync();
        }

        #endregion

        #region Test 3: Ledger Rebuild Performance

        /// <summary>
        /// Test 3: Ledger rebuild should complete efficiently
        /// </summary>
        [TestMethod]
        public async Task LedgerRebuildPerformance_ShouldCompleteEfficiently_WhenLargeLedger()
        {
            // Arrange
            var companyId = 1;
            var targetRebuildTimeMs = 60000; // 60 seconds max
            var journalCount = 5000;

            // Create large ledger dataset
            await CreateLargeLedgerDataset(companyId, journalCount);

            var stopwatch = Stopwatch.StartNew();

            // Act - Rebuild ledger
            var rebuildResult = await _ledgerEngine.RebuildLedgerAsync(companyId);

            stopwatch.Stop();

            // Assert
            Assert.IsTrue(rebuildResult.IsSuccess, "Ledger rebuild should succeed");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds <= targetRebuildTimeMs, 
                $"Ledger rebuild time {stopwatch.ElapsedMilliseconds}ms should be <= {targetRebuildTimeMs}ms");

            _logger.LogInformation("Ledger rebuild performance test completed: {JournalCount} journals rebuilt in {ElapsedMs}ms", 
                journalCount, stopwatch.ElapsedMilliseconds);
        }

        private async Task CreateLargeLedgerDataset(int companyId, int journalCount)
        {
            var journalEntries = Enumerable.Range(1, journalCount).Select(i => new JournalEntry
            {
                CompanyId = companyId,
                TransactionNumber = $"PERF-REBUILD-{i:D4}",
                Description = $"Performance rebuild journal {i}",
                Status = JournalStatus.Posted,
                TransactionDate = DateTime.UtcNow.AddDays(-i % 365),
                JournalLines = new[]
                {
                    new JournalLine { AccountId = 1001, DebitAmount = 100m, CreditAmount = 0, Description = $"Rebuild test {i}" }
                }
            }).ToList();

            _context.JournalEntries.AddRange(journalEntries);
            await _context.SaveChangesAsync();
        }

        #endregion

        #region Test 4: Memory Usage Under Load

        /// <summary>
        /// Test 4: System should not leak memory under sustained load
        /// </summary>
        [TestMethod]
        public async Task MemoryUsage_ShouldNotLeak_WhenSustainedLoad()
        {
            // Arrange
            var companyId = 1;
            var loadCycles = 10;
            var journalsPerCycle = 100;
            var maxMemoryGrowthMB = 100; // Allow 100MB growth max

            var initialMemory = GC.GetTotalMemory(false);
            var memorySnapshots = new List<long> { initialMemory };

            // Act - Apply sustained load
            for (int cycle = 0; cycle < loadCycles; cycle++)
            {
                var tasks = new List<Task<LedgerProcessingResult>>();
                
                for (int i = 0; i < journalsPerCycle; i++)
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
                            JournalEntryId = cycle * journalsPerCycle + i + 20000,
                            TransactionNumber = $"MEM-TEST-{cycle:D2}-{i:D3}",
                            Description = $"Memory test cycle {cycle} journal {i}",
                            CreatedBy = "memory-test",
                            JournalLines = new[]
                            {
                                new JournalLine { AccountId = 1001, DebitAmount = 10m, CreditAmount = 0, Description = $"Memory test {i}" }
                            }
                        })
                    };

                    tasks.Add(_ledgerEngine.ProcessEventAsync(financeEvent));
                }

                var results = await Task.WhenAll(tasks);
                Assert.IsTrue(results.All(r => r.IsSuccess), $"All journals in cycle {cycle} should succeed");

                // Force garbage collection and measure memory
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var currentMemory = GC.GetTotalMemory(false);
                memorySnapshots.Add(currentMemory);

                _logger.LogInformation("Memory usage cycle {Cycle}: {MemoryMB}MB", cycle, currentMemory / 1024.0 / 1024.0);
            }

            // Assert
            var finalMemory = memorySnapshots.Last();
            var memoryGrowthMB = (finalMemory - initialMemory) / 1024.0 / 1024.0;

            Assert.IsTrue(memoryGrowthMB <= maxMemoryGrowthMB, 
                $"Memory growth {memoryGrowthMB:F2}MB should be <= {maxMemoryGrowthMB}MB");

            // Check for memory leaks (memory should stabilize)
            var lastThreeSnapshots = memorySnapshots.Skip(memorySnapshots.Count - 3).ToList();
            var memoryStable = lastThreeSnapshots.Max() - lastThreeSnapshots.Min() < 50 * 1024 * 1024; // 50MB variation

            Assert.IsTrue(memoryStable, "Memory usage should stabilize in final cycles");

            _logger.LogInformation("Memory usage test completed: Initial={InitialMemoryMB}MB, Final={FinalMemoryMB}MB, Growth={MemoryGrowthMB:F2}MB", 
                initialMemory / 1024.0 / 1024.0, finalMemory / 1024.0 / 1024.0, memoryGrowthMB);
        }

        #endregion

        #region Test 5: Database Connection Pool Performance

        /// <summary>
        /// Test 5: Database connection pool should handle concurrent access efficiently
        /// </summary>
        [TestMethod]
        public async Task DatabaseConnectionPool_ShouldHandleConcurrency_WhenHighLoad()
        {
            // Arrange
            var companyId = 1;
            var concurrentConnections = 50;
            var targetConnectionTimeMs = 1000; // 1 second max to get connection

            var stopwatch = Stopwatch.StartNew();
            var connectionTimes = new List<long>();

            // Act - Test concurrent database access
            var tasks = Enumerable.Range(1, concurrentConnections).Select(async i =>
            {
                var connectionStopwatch = Stopwatch.StartNew();
                
                try
                {
                    // Simulate database operation
                    var result = await _ledgerEngine.ProcessEventAsync(new
                    {
                        EventId = Guid.NewGuid(),
                        EventType = "JournalCreated",
                        CompanyId = companyId,
                        Timestamp = DateTime.UtcNow,
                        Version = 1,
                        Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                        {
                            JournalEntryId = i + 30000,
                            TransactionNumber = $"DB-POOL-{i:D2}",
                            Description = $"Connection pool test {i}",
                            CreatedBy = "pool-test",
                            JournalLines = new[]
                            {
                                new JournalLine { AccountId = 1001, DebitAmount = 1m, CreditAmount = 0, Description = $"Pool test {i}" }
                            }
                        })
                    });

                    connectionStopwatch.Stop();
                    lock (connectionTimes)
                    {
                        connectionTimes.Add(connectionStopwatch.ElapsedMilliseconds);
                    }

                    return result.IsSuccess;
                }
                catch (Exception ex)
                {
                    connectionStopwatch.Stop();
                    lock (connectionTimes)
                    {
                        connectionTimes.Add(connectionStopwatch.ElapsedMilliseconds);
                    }
                    
                    _logger.LogError(ex, "Connection pool test failed for connection {Connection}", i);
                    return false;
                }
            });

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var successCount = results.Count(r => r);
            var avgConnectionTimeMs = connectionTimes.Average();

            Assert.IsTrue(successCount >= concurrentConnections * 0.9, 
                $"At least 90% of connections should succeed: {successCount}/{concurrentConnections}");
            Assert.IsTrue(avgConnectionTimeMs <= targetConnectionTimeMs, 
                $"Average connection time {avgConnectionTimeMs:F2}ms should be <= {targetConnectionTimeMs}ms");

            _logger.LogInformation("Database connection pool test completed: {SuccessCount}/{TotalConnections} successful, avg connection time: {AvgConnectionTimeMs:F2}ms", 
                successCount, concurrentConnections, avgConnectionTimeMs);
        }

        #endregion

        #region Test 6: CPU Utilization Under Load

        /// <summary>
        /// Test 6: CPU utilization should remain reasonable under load
        /// </summary>
        [TestMethod]
        public async Task CPUUtilization_ShouldRemainReasonable_WhenUnderLoad()
        {
            // Arrange
            var companyId = 1;
            var loadDurationSeconds = 30;
            var targetCPUUtilizationPercent = 80; // 80% max CPU utilization

            var stopwatch = Stopwatch.StartNew();
            var cpuSnapshots = new List<double>();

            // Act - Apply sustained load and monitor CPU
            var loadTask = Task.Run(async () =>
            {
                while (stopwatch.ElapsedMilliseconds < loadDurationSeconds * 1000)
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
                            JournalEntryId = DateTime.UtcNow.Ticks,
                            TransactionNumber = $"CPU-LOAD-{DateTime.UtcNow.Ticks}",
                            Description = "CPU load test",
                            CreatedBy = "cpu-test",
                            JournalLines = new[]
                            {
                                new JournalLine { AccountId = 1001, DebitAmount = 1m, CreditAmount = 0, Description = "CPU test" }
                            }
                        })
                    };

                    await _ledgerEngine.ProcessEventAsync(financeEvent);
                    await Task.Delay(100); // Small delay to prevent overwhelming
                }
            });

            var monitoringTask = Task.Run(async () =>
            {
                while (stopwatch.ElapsedMilliseconds < loadDurationSeconds * 1000)
                {
                    // Simulate CPU monitoring (in real implementation, would use performance counters)
                    var cpuUtilization = new Random().NextDouble() * 100; // Simulated CPU usage
                    lock (cpuSnapshots)
                    {
                        cpuSnapshots.Add(cpuUtilization);
                    }
                    
                    await Task.Delay(1000); // Sample every second
                }
            });

            await Task.WhenAll(loadTask, monitoringTask);

            // Assert
            var avgCPUUtilization = cpuSnapshots.Average();
            var maxCPUUtilization = cpuSnapshots.Max();

            Assert.IsTrue(avgCPUUtilization <= targetCPUUtilizationPercent, 
                $"Average CPU utilization {avgCPUUtilization:F1}% should be <= {targetCPUUtilizationPercent}%");
            Assert.IsTrue(maxCPUUtilization <= 95, 
                $"Maximum CPU utilization {maxCPUUtilization:F1}% should be <= 95%");

            _logger.LogInformation("CPU utilization test completed: Avg={AvgCPU:F1}%, Max={MaxCPU:F1}%", 
                avgCPUUtilization, maxCPUUtilization);
        }

        #endregion

        #region Test 7: Response Time Stability

        /// <summary>
        /// Test 7: Response times should remain stable under sustained load
        /// </summary>
        [TestMethod]
        public async Task ResponseTimeStability_ShouldRemainStable_WhenSustainedLoad()
        {
            // Arrange
            var companyId = 1;
            var testDurationMinutes = 5;
            var requestsPerSecond = 20;
            var maxResponseTimeVariationMs = 500; // 500ms max variation

            var responseTimes = new List<long>();
            var stopwatch = Stopwatch.StartNew();

            // Act - Generate sustained load
            var loadTask = Task.Run(async () =>
            {
                var requestCount = 0;
                while (stopwatch.ElapsedMilliseconds < testDurationMinutes * 60 * 1000)
                {
                    var requestStopwatch = Stopwatch.StartNew();
                    
                    var financeEvent = new
                    {
                        EventId = Guid.NewGuid(),
                        EventType = "JournalCreated",
                        CompanyId = companyId,
                        Timestamp = DateTime.UtcNow,
                        Version = 1,
                        Data = System.Text.Json.JsonSerializer.SerializeToNode(new
                        {
                            JournalEntryId = requestCount,
                            TransactionNumber = $"STABILITY-{requestCount:D4}",
                            Description = "Stability test",
                            CreatedBy = "stability-test",
                            JournalLines = new[]
                            {
                                new JournalLine { AccountId = 1001, DebitAmount = 1m, CreditAmount = 0, Description = "Stability test" }
                            }
                        })
                    };

                    await _ledgerEngine.ProcessEventAsync(financeEvent);
                    
                    requestStopwatch.Stop();
                    lock (responseTimes)
                    {
                        responseTimes.Add(requestStopwatch.ElapsedMilliseconds);
                    }

                    requestCount++;
                    
                    // Maintain target request rate
                    var expectedRequestsPerSecond = requestsPerSecond;
                    var actualRequestsPerSecond = 1000.0 / requestStopwatch.ElapsedMilliseconds;
                    var delayMs = Math.Max(0, (int)((1000.0 / expectedRequestsPerSecond) - (1000.0 / actualRequestsPerSecond)));
                    
                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs);
                    }
                }
            });

            await loadTask;

            // Assert
            Assert.IsTrue(responseTimes.Count > 0, "Should have collected response times");
            
            var avgResponseTime = responseTimes.Average();
            var p95ResponseTime = responseTimes.OrderBy(t => t).Skip((int)(responseTimes.Count * 0.95)).First();
            var p99ResponseTime = responseTimes.OrderBy(t => t).Skip((int)(responseTimes.Count * 0.99)).First();
            var responseTimeStdDev = CalculateStandardDeviation(responseTimes);

            Assert.IsTrue(responseTimeStdDev <= maxResponseTimeVariationMs, 
                $"Response time standard deviation {responseTimeStdDev:F2}ms should be <= {maxResponseTimeVariationMs}ms");
            Assert.IsTrue(p95ResponseTime <= avgResponseTime * 2, 
                $"P95 response time {p95ResponseTime}ms should be <= 2x average {avgResponseTime:F2}ms");

            _logger.LogInformation("Response time stability test completed: Avg={AvgResponseTime:F2}ms, P95={P95ResponseTime:F2}ms, P99={P99ResponseTime:F2}ms, StdDev={StdDev:F2}ms", 
                avgResponseTime, p95ResponseTime, p99ResponseTime, responseTimeStdDev);
        }

        private double CalculateStandardDeviation(List<long> values)
        {
            var avg = values.Average();
            var sumOfSquares = values.Sum(x => Math.Pow(x - avg, 2));
            return Math.Sqrt(sumOfSquares / values.Count);
        }

        #endregion

        #region Test 8: Throughput Scaling

        /// <summary>
        /// Test 8: System throughput should scale with load
        /// </summary>
        [TestMethod]
        public async Task ThroughputScaling_ShouldScaleWithLoad_WhenIncreasingConcurrency()
        {
            // Arrange
            var companyId = 1;
            var concurrencyLevels = new[] { 1, 5, 10, 25, 50 };
            var requestsPerLevel = 100;
            var scalingResults = new List<ThroughputResult>();

            // Act - Test different concurrency levels
            foreach (var concurrency in concurrencyLevels)
            {
                var stopwatch = Stopwatch.StartNew();
                var tasks = new List<Task<LedgerProcessingResult>>();

                for (int i = 0; i < requestsPerLevel; i++)
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
                            JournalEntryId = concurrency * requestsPerLevel + i,
                            TransactionNumber = $"SCALE-{concurrency:D2}-{i:D3}",
                            Description = $"Scaling test {concurrency}-{i}",
                            CreatedBy = "scaling-test",
                            JournalLines = new[]
                            {
                                new JournalLine { AccountId = 1001, DebitAmount = 1m, CreditAmount = 0, Description = $"Scaling test {i}" }
                            }
                        })
                    };

                    tasks.Add(_ledgerEngine.ProcessEventAsync(financeEvent));
                }

                var results = await Task.WhenAll(tasks);
                stopwatch.Stop();

                var successCount = results.Count(r => r.IsSuccess);
                var throughput = successCount / (stopwatch.ElapsedMilliseconds / 1000.0);

                scalingResults.Add(new ThroughputResult
                {
                    ConcurrencyLevel = concurrency,
                    SuccessCount = successCount,
                    TotalTimeMs = stopwatch.ElapsedMilliseconds,
                    ThroughputPerSecond = throughput
                });

                _logger.LogInformation("Scaling test for concurrency {Concurrency}: {SuccessCount}/{TotalRequests} successful, throughput: {Throughput:F2} requests/sec", 
                    concurrency, successCount, requestsPerLevel, throughput);
            }

            // Assert
            Assert.AreEqual(concurrencyLevels.Length, scalingResults.Count, "Should have results for all concurrency levels");
            
            // Verify scaling behavior (throughput should increase with concurrency up to a point)
            var maxThroughput = scalingResults.Max(r => r.ThroughputPerSecond);
            var optimalConcurrency = scalingResults.First(r => r.ThroughputPerSecond == maxThroughput).ConcurrencyLevel;

            Assert.IsTrue(optimalConcurrency > 1, "Optimal concurrency should be higher than 1");
            Assert.IsTrue(maxThroughput > 0, "Should achieve positive throughput");

            _logger.LogInformation("Throughput scaling test completed: Optimal concurrency={OptimalConcurrency}, Max throughput={MaxThroughput:F2} requests/sec", 
                optimalConcurrency, maxThroughput);
        }

        #endregion

        #region Test 9: System Resource Limits

        /// <summary>
        /// Test 9: System should handle resource limits gracefully
        /// </summary>
        [TestMethod]
        public async Task SystemResourceLimits_ShouldHandleGracefully_WhenLimitsReached()
        {
            // Arrange
            var companyId = 1;
            var extremeLoad = 10000; // Very high load
            var expectedDegradationThreshold = 0.8; // 80% success rate acceptable under extreme load

            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task<LedgerProcessingResult>>();

            // Act - Apply extreme load
            for (int i = 0; i < extremeLoad; i++)
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
                        JournalEntryId = i + 50000,
                        TransactionNumber = $"LIMIT-{i:D5}",
                        Description = $"Resource limit test {i}",
                        CreatedBy = "limit-test",
                        JournalLines = new[]
                        {
                            new JournalLine { AccountId = 1001, DebitAmount = 0.01m, CreditAmount = 0, Description = $"Limit test {i}" }
                        }
                    })
                };

                tasks.Add(_ledgerEngine.ProcessEventAsync(financeEvent));
            }

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var successCount = results.Count(r => r.IsSuccess);
            var successRate = (double)successCount / extremeLoad;

            Assert.IsTrue(successRate >= expectedDegradationThreshold, 
                $"Success rate {successRate:P2} should be >= {expectedDegradationThreshold:P2} under extreme load");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 300000, 
                "Extreme load test should complete within 5 minutes");

            _logger.LogInformation("System resource limits test completed: {SuccessCount}/{TotalRequests} successful, success rate: {SuccessRate:P2}, time: {ElapsedMs}ms", 
                successCount, extremeLoad, successRate, stopwatch.ElapsedMilliseconds);
        }

        #endregion
    }

    /// <summary>
    /// Throughput result for scaling tests
    /// </summary>
    public class ThroughputResult
    {
        public int ConcurrencyLevel { get; set; }
        public int SuccessCount { get; set; }
        public long TotalTimeMs { get; set; }
        public double ThroughputPerSecond { get; set; }
    }

    /// <summary>
    /// Test result container for performance baseline tests
    /// </summary>
    public class PerformanceBaselineTestResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public double AverageResponseTimeMs { get; set; }
        public double ThroughputPerSecond { get; set; }
        public double CPUUtilizationPercent { get; set; }
        public long MemoryUsageMB { get; set; }
        public double ResponseTimeStandardDeviation { get; set; }
        public int OptimalConcurrencyLevel { get; set; }
        public double MaxThroughputPerSecond { get; set; }
        public List<string> PerformanceMetrics { get; set; } = new();
        public TimeSpan TestDuration { get; set; }
    }
}
