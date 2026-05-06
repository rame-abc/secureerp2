using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using SecureERP2.Modules.Finance.Entities;
using SecureERP2.Modules.Finance.Services;

namespace SecureERP2.Modules.Finance.Services.Hardening
{
    /// <summary>
    /// 🔬 STEP 2: Observability System
    /// Metrics (lag, failures, retries), Distributed tracing
    /// </summary>
    public class ObservabilityService
    {
        private readonly ILogger<ObservabilityService> _logger;
        // private readonly IConnectionMultiplexer _redis; // Commented out due to missing assembly reference
        // private readonly EventBusService _eventBus; // Commented out - EventBusService not available
        private readonly LedgerEngineService _ledgerEngine;
        
        // Metrics tracking
        private readonly ConcurrentDictionary<string, MetricCounter> _metrics;
        private readonly ConcurrentDictionary<string, List<TraceRecord>> _traces;
        private readonly ConcurrentDictionary<string, LagMetric> _lagMetrics;
        
        // Performance counters (commented out due to missing assembly reference)
        // private readonly PerformanceCounter _cpuCounter;
        // private readonly PerformanceCounter _memoryCounter;
        // private readonly PerformanceCounter _diskCounter;
        
        // Redis keys
        private const string MetricsKeyPrefix = "metrics:";
        private const string TracesKeyPrefix = "traces:";
        private const string LagKeyPrefix = "lag:";
        private const string AlertsKeyPrefix = "alerts:";
        
        // Configuration
        private const int MaxTraceRecords = 10000;
        private const int MetricsRetentionHours = 24;
        private const int AlertThresholdMinutes = 5;
        
        public ObservabilityService(
            ILogger<ObservabilityService> logger,
            // EventBusService eventBus, // Commented out - EventBusService not available
            LedgerEngineService ledgerEngine)
        {
            _logger = logger;
            // _redis = redis; // Commented out due to missing assembly reference
            // _eventBus = eventBus; // Commented out - EventBusService not available
            _ledgerEngine = ledgerEngine;
            
            _metrics = new ConcurrentDictionary<string, MetricCounter>();
            _traces = new ConcurrentDictionary<string, List<TraceRecord>>();
            _lagMetrics = new ConcurrentDictionary<string, LagMetric>();
            
            // 🔥 Initialize performance counters (commented out due to missing assembly reference)
            // try
            // {
            //     _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            //     _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            //     _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
            // }
            // catch (Exception ex)
            // {
            //     _logger.LogWarning(ex, "Failed to initialize performance counters");
            // }
            
            // 🔥 Start background monitoring
            _ = Task.Run(MonitorSystemMetricsAsync);
            _ = Task.Run(MonitorEventLagAsync);
            _ = Task.Run(CheckAlertsAsync);
        }
        
        /// <summary>
        /// Start distributed trace
        /// </summary>
        public DistributedTrace StartTrace(string operationType, int companyId, object context = null)
        {
            var traceId = Guid.NewGuid().ToString();
            var spanId = Guid.NewGuid().ToString();
            
            var trace = new DistributedTrace
            {
                TraceId = traceId,
                SpanId = spanId,
                ParentSpanId = null,
                OperationType = operationType,
                CompanyId = companyId,
                StartedAt = DateTime.UtcNow,
                Context = context != null ? JsonSerializer.Serialize(context) : string.Empty,
                Status = TraceStatus.Running
            };
            
            // 🔥 Store trace
            StoreTrace(trace);
            
            _logger.LogDebug("Started trace {TraceId} for {OperationType} company {CompanyId}", 
                traceId, operationType, companyId);
            
            return trace;
        }
        
        /// <summary>
        /// Create child span
        /// </summary>
        public DistributedTrace CreateChildSpan(DistributedTrace parentTrace, string operationType, object context = null)
        {
            var spanId = Guid.NewGuid().ToString();
            
            var span = new DistributedTrace
            {
                TraceId = parentTrace.TraceId,
                SpanId = spanId,
                ParentSpanId = parentTrace.SpanId,
                OperationType = operationType,
                CompanyId = parentTrace.CompanyId,
                StartedAt = DateTime.UtcNow,
                Context = context != null ? JsonSerializer.Serialize(context) : string.Empty,
                Status = TraceStatus.Running
            };
            
            // 🔥 Store span
            StoreTrace(span);
            
            _logger.LogDebug("Created child span {SpanId} for {OperationType} in trace {TraceId}", 
                spanId, operationType, parentTrace.TraceId);
            
            return span;
        }
        
        /// <summary>
        /// Complete trace/span
        /// </summary>
        public void CompleteTrace(DistributedTrace trace, bool success = true, string error = null)
        {
            trace.CompletedAt = DateTime.UtcNow;
            // TODO: Fix TimeSpan? TotalMilliseconds - need to handle nullable TimeSpan
            // trace.DurationMs = (trace.CompletedAt - trace.StartedAt).TotalMilliseconds;
            trace.DurationMs = (trace.CompletedAt - trace.StartedAt)?.TotalMilliseconds ?? 0;
            trace.Status = success ? TraceStatus.Completed : TraceStatus.Failed;
            trace.ErrorMessage = error ?? string.Empty;
            
            // 🔥 Update trace
            UpdateTrace(trace);
            
            // 🔥 Record metrics
            RecordMetric($"{trace.OperationType}.duration", trace.DurationMs);
            RecordMetric($"{trace.OperationType}.success", success ? 1 : 0);
            RecordMetric($"{trace.OperationType}.error", success ? 0 : 1);
            
            _logger.LogDebug("Completed trace {TraceId} for {OperationType}: {Success} in {Duration}ms", 
                trace.TraceId, trace.OperationType, success, trace.DurationMs);
        }
        
        /// <summary>
        /// Record metric
        /// </summary>
        public void RecordMetric(string metricName, double value, Dictionary<string, string> tags = null)
        {
            var metric = new MetricRecord
            {
                Name = metricName,
                Value = value,
                Timestamp = DateTime.UtcNow,
                Tags = tags ?? new Dictionary<string, string>()
            };
            
            // 🔥 Update counter
            var counter = _metrics.GetOrAdd(metricName, _ => new MetricCounter
            {
                Name = metricName,
                Count = 0,
                Sum = 0,
                Min = double.MaxValue,
                Max = double.MinValue
            });
            
            counter.Count++;
            counter.Sum += value;
            counter.Min = Math.Min(counter.Min, value);
            counter.Max = Math.Max(counter.Max, value);
            counter.LastUpdated = DateTime.UtcNow;
            
            // 🔥 Store in Redis
            StoreMetricAsync(metric);
            
            _logger.LogDebug("Recorded metric {MetricName}: {Value}", metricName, value);
        }
        
        /// <summary>
        /// Record event lag
        /// </summary>
        public void RecordEventLag(string eventType, DateTime eventTimestamp, DateTime processedTimestamp)
        {
            var lagMs = (processedTimestamp - eventTimestamp).TotalMilliseconds;
            
            var lagMetric = new LagMetric
            {
                EventType = eventType,
                EventTimestamp = eventTimestamp,
                ProcessedTimestamp = processedTimestamp,
                LagMs = lagMs,
                RecordedAt = DateTime.UtcNow
            };
            
            // 🔥 Update lag tracking
            var key = $"lag:{eventType}";
            var existing = _lagMetrics.GetOrAdd(key, _ => new LagMetric
            {
                EventType = eventType,
                Count = 0,
                TotalLagMs = 0,
                MinLagMs = double.MaxValue,
                MaxLagMs = double.MinValue
            });
            
            existing.Count++;
            existing.TotalLagMs += lagMs;
            existing.MinLagMs = Math.Min(existing.MinLagMs, lagMs);
            existing.MaxLagMs = Math.Max(existing.MaxLagMs, lagMs);
            existing.AverageLagMs = existing.TotalLagMs / existing.Count;
            existing.LastUpdated = DateTime.UtcNow;
            
            // 🔥 Store in Redis
            StoreLagMetricAsync(lagMetric);
            
            // 🔥 Check for lag alerts
            if (lagMs > 5000) // 5 seconds
            {
                CreateAlert($"High lag detected for {eventType}", AlertSeverity.High, new Dictionary<string, object>
                {
                    ["eventType"] = eventType,
                    ["lagMs"] = lagMs,
                    ["threshold"] = 5000
                });
            }
            
            _logger.LogDebug("Recorded event lag for {EventType}: {Lag}ms", eventType, lagMs);
        }
        
        /// <summary>
        /// Monitor system metrics
        /// </summary>
        private async Task MonitorSystemMetricsAsync()
        {
            while (true)
            {
                try
                {
                    var timestamp = DateTime.UtcNow;
                    
                    // 🔥 CPU usage
                    // TODO: Add missing _cpuCounter field or use alternative approach
                    // if (_cpuCounter != null)
                    // {
                    //     var cpuUsage = _cpuCounter.NextValue();
                    //     RecordMetric("system.cpu.usage", cpuUsage, new Dictionary<string, string>
                    //     {
                    //         ["source"] = "performance_counter"
                    // TODO: Mock CPU usage for now
                    var cpuUsage = 25.0; // Placeholder
                    RecordMetric("system.cpu.usage", cpuUsage, new Dictionary<string, string>
                    {
                        ["source"] = "mock"
                    });
                    
                    // 🔥 Memory usage
                    // TODO: Add missing _memoryCounter field or use alternative approach
                    // if (_memoryCounter != null)
                    // {
                    //     var availableMemory = _memoryCounter.NextValue();
                    //     RecordMetric("system.memory.available_mb", availableMemory, new Dictionary<string, string>
                    //     {
                    //         ["source"] = "performance_counter"
                    // TODO: Mock memory usage for now
                    var availableMemory = 4096.0; // Placeholder
                    RecordMetric("system.memory.available_mb", availableMemory, new Dictionary<string, string>
                    {
                        ["source"] = "mock"
                    });
                    
                    // 🔥 Disk usage
                    // TODO: Add missing _diskCounter field or use alternative approach
                    // if (_diskCounter != null)
                    // {
                    //     var diskUsage = _diskCounter.NextValue();
                    //     RecordMetric("system.disk.usage", diskUsage, new Dictionary<string, string>
                    //     {
                    //         ["source"] = "performance_counter"
                    // TODO: Mock disk usage for now
                    var diskUsage = 75.0; // Placeholder
                    RecordMetric("system.disk.usage", diskUsage, new Dictionary<string, string>
                    {
                        ["source"] = "mock"
                    });
                    
                    // 🔥 Redis metrics
                    await RecordRedisMetricsAsync(timestamp);
                    
                    // 🔥 Event bus metrics
                    await RecordEventBusMetricsAsync(timestamp);
                    
                    // 🔥 Ledger engine metrics
                    await RecordLedgerMetricsAsync(timestamp);
                    
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring system metrics");
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            }
        }
        
        /// <summary>
        /// Record Redis metrics
        /// </summary>
        private async Task RecordRedisMetricsAsync(DateTime timestamp)
        {
            try
            {
                // TODO: Use IDistributedCache instead of Redis
                // var server = _redis.GetServer(_redis.GetEndPoints().First());
                // var info = server.Info();
                
                // 🔥 Memory usage
                // var memoryInfo = info.FirstOrDefault(i => i.Key == "memory");
                // if (memoryInfo != null)
                // {
                //     var usedMemory = memoryInfo.Entries.FirstOrDefault(e => e.Key == "used_memory").Value;
                // TODO: Mock Redis info for now
                var usedMemory = "0"; // Placeholder
                // TODO: Comment out remaining Redis code
                // if (double.TryParse(usedMemory, out var memoryBytes))
                // {
                //     RecordMetric("redis.memory.used_bytes", memoryBytes, new Dictionary<string, string>
                //     {
                //         ["source"] = "redis_info"
                //     });
                // }
                
                // 🔥 Connected clients
                // TODO: Comment out remaining Redis code
                // var clientsInfo = info.FirstOrDefault(i => i.Key == "clients");
                // if (clientsInfo != null)
                // {
                //     var connectedClients = clientsInfo.Entries.FirstOrDefault(e => e.Key == "connected_clients").Value;
                //     if (int.TryParse(connectedClients, out var clients))
                //     {
                //         RecordMetric("redis.clients.connected", clients, new Dictionary<string, string>
                //         {
                //             ["source"] = "redis_info"
                //         });
                //     }
                // }
                
                // 🔥 Operations per second
                // TODO: Comment out remaining Redis code
                // var statsInfo = info.FirstOrDefault(i => i.Key == "stats");
                // if (statsInfo != null)
                // {
                //     var opsPerSec = statsInfo.Entries.FirstOrDefault(e => e.Key == "instantaneous_ops_per_sec").Value;
                // TODO: Mock ops per second for now
                var opsPerSec = "0"; // Placeholder
                // TODO: Comment out remaining Redis code
                // if (double.TryParse(opsPerSec, out var ops))
                // {
                //     RecordMetric("redis.ops_per_sec", ops, new Dictionary<string, string>
                //     {
                //         ["source"] = "redis_info"
                //     });
                // }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording Redis metrics");
            }
        }
        
        /// <summary>
        /// Record event bus metrics
        /// </summary>
        private async Task RecordEventBusMetricsAsync(DateTime timestamp)
        {
            try
            {
                // TODO: Add _eventBus field to ObservabilityService
                // 🔥 Get event bus statistics
                // var stats = await _eventBus.GetStatisticsAsync();
                // TODO: Mock event bus statistics for now
                // TODO: Comment out remaining event bus code
                // var stats = new EventBusStatistics(); // Placeholder
                // 
                // if (stats.IsSuccess)
                // {
                //     RecordMetric("eventbus.total_events", stats.TotalEvents, new Dictionary<string, string>
                //     {
                //         ["source"] = "eventbus_stats"
                //     });
                //     
                //     RecordMetric("eventbus.failed_events", stats.FailedEvents, new Dictionary<string, string>
                //     {
                //         ["source"] = "eventbus_stats"
                //     });
                //     
                //     RecordMetric("eventbus.retry_events", stats.RetriedEvents, new Dictionary<string, string>
                //     {
                //         ["source"] = "eventbus_stats"
                //     });
                //     
                //     RecordMetric("eventbus.active_subscriptions", stats.ActiveSubscriptions, new Dictionary<string, string>
                //     {
                //         ["source"] = "eventbus_stats"
                //     });
                // }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording event bus metrics");
            }
        }
        
        /// <summary>
        /// Record ledger engine metrics
        /// </summary>
        private async Task RecordLedgerMetricsAsync(DateTime timestamp)
        {
            try
            {
                // TODO: Add GetStatisticsAsync method to LedgerEngineService
                // 🔥 Get ledger statistics
                // var stats = await _ledgerEngine.GetStatisticsAsync();
                // TODO: Mock ledger statistics for now
                // TODO: Comment out remaining ledger code
                // var stats = new LedgerStatistics(); // Placeholder
                // 
                // if (stats.IsSuccess)
                // {
                //     RecordMetric("ledger.total_events", stats.TotalEventsProcessed, new Dictionary<string, string>
                //     {
                //         ["source"] = "ledger_stats"
                //     });
                //     
                //     RecordMetric("ledger.active_companies", stats.ActiveCompanies, new Dictionary<string, string>
                //     {
                //         ["source"] = "ledger_stats"
                //     });
                //     
                //     RecordMetric("ledger.validation_errors", stats.ValidationErrors, new Dictionary<string, string>
                //     {
                //         ["source"] = "ledger_stats"
                //     });
                //     
                //     RecordMetric("ledger.reconciliation_errors", stats.ReconciliationErrors, new Dictionary<string, string>
                //     {
                //         ["source"] = "ledger_stats"
                //     });
                // }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording ledger metrics");
            }
        }
        
        /// <summary>
        /// Monitor event lag
        /// </summary>
        private async Task MonitorEventLagAsync()
        {
            while (true)
            {
                try
                {
                    // TODO: Use IDistributedCache instead of Redis
                    // // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                    // var pattern = $"{LagKeyPrefix}*";
                    // TODO: Mock event lag monitoring for now
                    // TODO: Comment out remaining Redis code
                    // var server = _redis.GetServer(_redis.GetEndPoints().First());
                    // var keys = server.Keys(database: db.Database, pattern: pattern).ToArray();
                    
                    // foreach (var key in keys)
                    // {
                    //     var lagJson = await db.StringGetAsync(key);
                    //     if (lagJson.HasValue)
                    //     {
                    //         var lagMetric = JsonSerializer.Deserialize<LagMetric>(lagJson);
                //         
                //         // 🔥 Check for stale lag metrics
                //         if (DateTime.UtcNow - lagMetric.RecordedAt > TimeSpan.FromMinutes(5))
                //         {
                //             await db.KeyDeleteAsync(key);
                //         }
                //     }
                // }
                    
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring event lag");
                    await Task.Delay(TimeSpan.FromMinutes(5));
                }
            }
        }
        
        /// <summary>
        /// Check alerts
        /// </summary>
        private async Task CheckAlertsAsync()
        {
            while (true)
            {
                try
                {
                    var alerts = new List<Alert>();
                    
                    // 🔥 Check CPU usage
                    var cpuMetric = _metrics.Values.FirstOrDefault(m => m.Name == "system.cpu.usage");
                    if (cpuMetric != null && cpuMetric.Max > 80)
                    {
                        alerts.Add(new Alert
                        {
                            Id = Guid.NewGuid(),
                            Type = "High CPU Usage",
                            Severity = AlertSeverity.Medium,
                            Message = $"CPU usage is {cpuMetric.Max:F1}%",
                            CreatedAt = DateTime.UtcNow,
                            Data = new Dictionary<string, object>
                            {
                                ["cpu_usage"] = cpuMetric.Max,
                                ["threshold"] = 80
                            }
                        });
                    }
                    
                    // 🔥 Check memory usage
                    var memoryMetric = _metrics.Values.FirstOrDefault(m => m.Name == "system.memory.available_mb");
                    if (memoryMetric != null && memoryMetric.Min < 100) // Less than 100MB available
                    {
                        alerts.Add(new Alert
                        {
                            Id = Guid.NewGuid(),
                            Type = "Low Memory",
                            Severity = AlertSeverity.High,
                            Message = $"Available memory is {memoryMetric.Min:F1}MB",
                            CreatedAt = DateTime.UtcNow,
                            Data = new Dictionary<string, object>
                            {
                                ["available_memory_mb"] = memoryMetric.Min,
                                ["threshold"] = 100
                            }
                        });
                    }
                    
                    // 🔥 Check error rates
                    var errorMetrics = _metrics.Where(m => m.Key.EndsWith(".error")).ToList();
                    foreach (var errorMetric in errorMetrics)
                    {
                        var successMetric = _metrics.Values.FirstOrDefault(m => m.Name == errorMetric.Key.Replace(".error", ".success"));
                        if (successMetric != null && successMetric.Count > 0)
                        {
                            var errorRate = (double)errorMetric.Value.Count / (successMetric.Count + errorMetric.Value.Count);
                            if (errorRate > 0.1) // 10% error rate
                            {
                                alerts.Add(new Alert
                                {
                                    Id = Guid.NewGuid(),
                                    Type = "High Error Rate",
                                    Severity = AlertSeverity.High,
                                    Message = $"Error rate for {errorMetric.Key} is {errorRate:P2}",
                                    CreatedAt = DateTime.UtcNow,
                                    Data = new Dictionary<string, object>
                                    {
                                        ["metric"] = errorMetric.Key,
                                        ["error_rate"] = errorRate,
                                        ["threshold"] = 0.1
                                    }
                                });
                            }
                        }
                    }
                    
                    // 🔥 Store alerts
                    foreach (var alert in alerts)
                    {
                        await StoreAlertAsync(alert);
                        _logger.LogWarning("ALERT: {Type} - {Message}", alert.Type, alert.Message);
                    }
                    
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking alerts");
                    await Task.Delay(TimeSpan.FromMinutes(5));
                }
            }
        }
        
        /// <summary>
        /// Create alert
        /// </summary>
        private void CreateAlert(string message, AlertSeverity severity, Dictionary<string, object> data)
        {
            var alert = new Alert
            {
                Id = Guid.NewGuid(),
                Type = "Performance",
                Severity = severity,
                Message = message,
                CreatedAt = DateTime.UtcNow,
                Data = data
            };
            
            _ = Task.Run(() => StoreAlertAsync(alert));
        }
        
        /// <summary>
        /// Store trace
        /// </summary>
        private void StoreTrace(DistributedTrace trace)
        {
            try
            {
                var key = $"{TracesKeyPrefix}{trace.TraceId}";
                
                if (!_traces.ContainsKey(key))
                {
                    _traces[key] = new List<TraceRecord>();
                }
                
                var record = new TraceRecord
                {
                    TraceId = trace.TraceId,
                    SpanId = trace.SpanId,
                    ParentSpanId = trace.ParentSpanId,
                    OperationType = trace.OperationType,
                    CompanyId = trace.CompanyId,
                    StartedAt = trace.StartedAt,
                    CompletedAt = trace.CompletedAt,
                    DurationMs = trace.DurationMs,
                    Status = trace.Status,
                    Context = trace.Context,
                    ErrorMessage = trace.ErrorMessage
                };
                
                _traces[key].Add(record);
                
                // 🔥 Keep only recent traces
                if (_traces[key].Count > MaxTraceRecords)
                {
                    _traces[key] = _traces[key].TakeLast(MaxTraceRecords).ToList();
                }
                
                // 🔥 Store in Redis
                _ = Task.Run(() => StoreTraceAsync(record));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing trace {TraceId}", trace.TraceId);
            }
        }
        
        /// <summary>
        /// Update trace
        /// </summary>
        private void UpdateTrace(DistributedTrace trace)
        {
            try
            {
                var key = $"{TracesKeyPrefix}{trace.TraceId}";
                
                if (_traces.ContainsKey(key))
                {
                    var record = _traces[key].FirstOrDefault(t => t.SpanId == trace.SpanId);
                    if (record != null)
                    {
                        record.CompletedAt = trace.CompletedAt;
                        record.DurationMs = trace.DurationMs;
                        record.Status = trace.Status;
                        record.ErrorMessage = trace.ErrorMessage;
                    }
                }
                
                // 🔥 Update in Redis
                _ = Task.Run(() => UpdateTraceAsync(trace));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating trace {TraceId}", trace.TraceId);
            }
        }
        
        /// <summary>
        /// Store metric asynchronously
        /// </summary>
        private async Task StoreMetricAsync(MetricRecord metric)
        {
            try
            {
                // TODO: Replace Redis metrics storage with IDistributedCache or mock
                // // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                // var key = $"{MetricsKeyPrefix}{metric.Name}";
                // 
                // var metricJson = JsonSerializer.Serialize(metric);
                // await db.ListLeftPushAsync(key, metricJson);
                // await db.ListTrimAsync(key, 0, 999); // Keep last 1000 values
                // await db.KeyExpireAsync(key, TimeSpan.FromHours(MetricsRetentionHours));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing metric {MetricName}", metric.Name);
            }
        }
        
        /// <summary>
        /// Store lag metric asynchronously
        /// </summary>
        private async Task StoreLagMetricAsync(LagMetric lagMetric)
        {
            try
            {
                // TODO: Replace Redis lag metrics storage with IDistributedCache or mock
                // // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                // var key = $"{LagKeyPrefix}{lagMetric.EventType}";
                // 
                // var lagJson = JsonSerializer.Serialize(lagMetric);
                // await db.StringSetAsync(key, lagJson, TimeSpan.FromMinutes(10));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing lag metric for {EventType}", lagMetric.EventType);
            }
        }
        
        /// <summary>
        /// Store trace asynchronously
        /// </summary>
        private async Task StoreTraceAsync(TraceRecord record)
        {
            try
            {
                // TODO: Replace Redis trace storage with IDistributedCache or mock
                // // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                // var key = $"{TracesKeyPrefix}{record.TraceId}";
                // 
                // var traceJson = JsonSerializer.Serialize(record);
                // await db.ListLeftPushAsync(key, traceJson);
                // await db.ListTrimAsync(key, 0, 99); // Keep last 100 spans
                // await db.KeyExpireAsync(key, TimeSpan.FromHours(1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing trace {TraceId}", record.TraceId);
            }
        }
        
        /// <summary>
        /// Update trace asynchronously
        /// </summary>
        private async Task UpdateTraceAsync(DistributedTrace trace)
        {
            try
            {
                // TODO: Replace Redis trace update with IDistributedCache or mock
                // // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                // var key = $"{TracesKeyPrefix}{trace.TraceId}";
                // 
                // var traceJson = JsonSerializer.Serialize(trace);
                // await db.StringSetAsync($"{key}:current", traceJson, TimeSpan.FromHours(1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating trace {TraceId}", trace.TraceId);
            }
        }
        
        /// <summary>
        /// Store alert asynchronously
        /// </summary>
        private async Task StoreAlertAsync(Alert alert)
        {
            try
            {
                // TODO: Replace Redis alert storage with IDistributedCache or mock
                // // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                // var alertJson = JsonSerializer.Serialize(alert);
                // 
                // await db.ListLeftPushAsync(AlertsKeyPrefix, alertJson);
                // await db.ListTrimAsync(AlertsKeyPrefix, 0, 999); // Keep last 1000 alerts
                // await db.KeyExpireAsync(AlertsKeyPrefix, TimeSpan.FromDays(7));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing alert {AlertId}", alert.Id);
            }
        }
        
        /// <summary>
        /// Get observability dashboard data
        /// </summary>
        public async Task<ObservabilityDashboard> GetDashboardAsync(int? companyId = null)
        {
            var dashboard = new ObservabilityDashboard
            {
                GeneratedAt = DateTime.UtcNow,
                CompanyId = companyId
            };
            
            try
            {
                // 🔥 System metrics
                dashboard.SystemMetrics = _metrics.Values.ToDictionary(m => m.Name, m => new MetricSummary
                {
                    Count = m.Count,
                    Sum = m.Sum,
                    Average = m.Count > 0 ? m.Sum / m.Count : 0,
                    Min = m.Min,
                    Max = m.Max,
                    LastUpdated = m.LastUpdated
                });
                
                // 🔥 Lag metrics
                dashboard.LagMetrics = _lagMetrics.Values.ToDictionary(l => l.EventType, l => new LagSummary
                {
                    Count = l.Count,
                    AverageLagMs = l.AverageLagMs,
                    MinLagMs = l.MinLagMs,
                    MaxLagMs = l.MaxLagMs,
                    LastUpdated = l.LastUpdated
                });
                
                // 🔥 Recent alerts
                dashboard.RecentAlerts = await GetRecentAlertsAsync(companyId);
                
                // 🔥 Trace summary
                dashboard.TraceSummary = GetTraceSummary();
                
                dashboard.IsSuccess = true;
                
                _logger.LogDebug("Generated observability dashboard for company {CompanyId}", companyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating observability dashboard");
                dashboard.IsSuccess = false;
                dashboard.ErrorMessage = ex.Message;
            }
            
            return dashboard;
        }
        
        /// <summary>
        /// Get recent alerts
        /// </summary>
        private async Task<List<Alert>> GetRecentAlertsAsync(int? companyId = null)
        {
            try
            {
                // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                // TODO: Redis operations commented out due to missing service reference
                // var alertsJson = await db.ListRangeAsync(AlertsKeyPrefix, 0, 49); // Last 50 alerts
                
                var alerts = new List<Alert>(); // Placeholder
                // var alerts = alertsJson
                //     .Select(a => JsonSerializer.Deserialize<Alert>(a))
                //     .OrderByDescending(a => a.CreatedAt)
                //     .ToList();
                
                return alerts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent alerts");
                return new List<Alert>();
            }
        }
        
        /// <summary>
        /// Get trace summary
        /// </summary>
        private TraceSummary GetTraceSummary()
        {
            var summary = new TraceSummary();
            
            try
            {
                var allTraces = _traces.Values.SelectMany(t => t).ToList();
                
                summary.TotalTraces = allTraces.Count;
                summary.RunningTraces = allTraces.Count(t => t.Status == TraceStatus.Running);
                summary.CompletedTraces = allTraces.Count(t => t.Status == TraceStatus.Completed);
                summary.FailedTraces = allTraces.Count(t => t.Status == TraceStatus.Failed);
                
                if (summary.CompletedTraces > 0)
                {
                    var completedTraces = allTraces.Where(t => t.Status == TraceStatus.Completed).ToList();
                    summary.AverageDurationMs = completedTraces.Average(t => t.DurationMs);
                    summary.MinDurationMs = completedTraces.Min(t => t.DurationMs);
                    summary.MaxDurationMs = completedTraces.Max(t => t.DurationMs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trace summary");
            }
            
            return summary;
        }
        
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            // TODO: Performance counters are commented out due to missing service references
            // _cpuCounter?.Dispose();
            // _memoryCounter?.Dispose();
            // _diskCounter?.Dispose();
        }
    }
    
    #region Supporting Classes
    
    public class DistributedTrace
    {
        public string TraceId { get; set; } = string.Empty;
        public string SpanId { get; set; } = string.Empty;
        public string ParentSpanId { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public double DurationMs { get; set; }
        public string Context { get; set; } = string.Empty;
        public TraceStatus Status { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
    
    public class TraceRecord
    {
        public string TraceId { get; set; } = string.Empty;
        public string SpanId { get; set; } = string.Empty;
        public string ParentSpanId { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public double DurationMs { get; set; }
        public TraceStatus Status { get; set; }
        public string Context { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
    
    public class MetricCounter
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Sum { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public DateTime LastUpdated { get; set; }
    }
    
    public class MetricRecord
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }
    
    public class LagMetric
    {
        public string EventType { get; set; } = string.Empty;
        public DateTime EventTimestamp { get; set; }
        public DateTime ProcessedTimestamp { get; set; }
        public double LagMs { get; set; }
        public DateTime RecordedAt { get; set; }
        public int Count { get; set; }
        public double TotalLagMs { get; set; }
        public double AverageLagMs { get; set; }
        public double MinLagMs { get; set; }
        public double MaxLagMs { get; set; }
        public DateTime LastUpdated { get; set; }
    }
    
    public class Alert
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }
    
    public class ObservabilityDashboard
    {
        public int? CompanyId { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public Dictionary<string, MetricSummary> SystemMetrics { get; set; } = new();
        public Dictionary<string, LagSummary> LagMetrics { get; set; } = new();
        public List<Alert> RecentAlerts { get; set; } = new();
        public TraceSummary TraceSummary { get; set; }
    }
    
    public class MetricSummary
    {
        public int Count { get; set; }
        public double Sum { get; set; }
        public double Average { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public DateTime LastUpdated { get; set; }
    }
    
    public class LagSummary
    {
        public int Count { get; set; }
        public double AverageLagMs { get; set; }
        public double MinLagMs { get; set; }
        public double MaxLagMs { get; set; }
        public DateTime LastUpdated { get; set; }
    }
    
    public class TraceSummary
    {
        public int TotalTraces { get; set; }
        public int RunningTraces { get; set; }
        public int CompletedTraces { get; set; }
        public int FailedTraces { get; set; }
        public double AverageDurationMs { get; set; }
        public double MinDurationMs { get; set; }
        public double MaxDurationMs { get; set; }
    }
    
    public enum TraceStatus
    {
        Running,
        Completed,
        Failed,
        Cancelled
    }
    
    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
    
    #endregion
}
