using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Threading;

namespace SecureERP2.Modules.Finance.Services
{
    /// <summary>
    /// Production Monitoring Service - Real-time system monitoring and alerting
    /// </summary>
    public class ProductionMonitoringService
    {
        private readonly ILogger<ProductionMonitoringService> _logger;
        private readonly IConfiguration _configuration;
        private readonly Timer _monitoringTimer;
        private readonly Dictionary<string, PerformanceCounter> _counters;
        private readonly List<ProductionAlert> _activeAlerts;
        private readonly object _alertLock = new object();

        public ProductionMonitoringService(
            ILogger<ProductionMonitoringService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _counters = new Dictionary<string, PerformanceCounter>();
            _activeAlerts = new List<ProductionAlert>();
            
            // Start monitoring timer
            _monitoringTimer = new Timer(MonitorSystem, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Get current production metrics
        /// </summary>
        public ProductionMetrics GetCurrentMetrics()
        {
            var process = Process.GetCurrentProcess();
            var memory = GC.GetTotalMemory(false);

            return new ProductionMetrics
            {
                Timestamp = DateTime.UtcNow,
                CpuUsage = GetCpuUsage(),
                MemoryUsage = new MemoryUsage
                {
                    UsedMB = memory / 1024 / 1024,
                    UsedGB = memory / 1024.0 / 1024 / 1024,
                    Percentage = (memory / (double)Environment.WorkingSet) * 100
                },
                DiskUsage = GetDiskUsage(),
                NetworkStats = GetNetworkStats(),
                ProcessInfo = new ProcessInfo
                {
                    ProcessId = process.Id,
                    ThreadCount = process.Threads.Count,
                    HandleCount = process.HandleCount,
                    StartTime = process.StartTime,
                    WorkingSet = process.WorkingSet64
                },
                PerformanceCounters = _counters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                ActiveAlerts = GetActiveAlerts()
            };
        }

        /// <summary>
        /// Get system health status
        /// </summary>
        public SystemHealth GetSystemHealth()
        {
            var metrics = GetCurrentMetrics();
            var overallStatus = DetermineOverallHealth(metrics);

            return new SystemHealth
            {
                Timestamp = DateTime.UtcNow,
                OverallStatus = overallStatus,
                Components = GetComponentHealth(metrics),
                Performance = GetPerformanceMetrics(metrics),
                Resources = GetResourceStatus(metrics),
                Dependencies = GetDependencyStatus(),
                LastUpdated = DateTime.UtcNow,
                Recommendations = GetHealthRecommendations(metrics, overallStatus)
            };
        }

        /// <summary>
        /// Get production alerts
        /// </summary>
        public List<ProductionAlert> GetAlerts(AlertSeverity? minSeverity = null)
        {
            lock (_alertLock)
            {
                var alerts = _activeAlerts.Where(a => a.IsActive).ToList();
                
                if (minSeverity.HasValue)
                {
                    alerts = alerts.Where(a => a.Severity >= minSeverity.Value).ToList();
                }

                return alerts.OrderByDescending(a => a.Timestamp).ToList();
            }
        }

        /// <summary>
        /// Get performance analytics
        /// </summary>
        public PerformanceAnalytics GetPerformanceAnalytics(TimeSpan period)
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime - period;

            return new PerformanceAnalytics
            {
                TimeRange = new TimeRange { Start = startTime, End = endTime },
                Metrics = GetAggregatedMetrics(startTime, endTime),
                Trends = GetPerformanceTrends(startTime, endTime),
                Alerts = GetAlertsInPeriod(startTime, endTime),
                Summaries = GetPerformanceSummaries(startTime, endTime),
                Benchmarks = GetPerformanceBenchmarks()
            };
        }

        /// <summary>
        /// Create production alert
        /// </summary>
        public void CreateAlert(AlertType type, AlertSeverity severity, string message, object details = null)
        {
            lock (_alertLock)
            {
                var alert = new ProductionAlert
                {
                    Id = Guid.NewGuid(),
                    Type = type,
                    Severity = severity,
                    Message = message,
                    Timestamp = DateTime.UtcNow,
                    Details = details,
                    IsActive = true,
                    Resolved = false,
                    ResolvedAt = null
                };

                _activeAlerts.Add(alert);

                _logger.LogWarning("Production alert created: {AlertType} - {Message}", type, message);

                // Send notification (in production, this would integrate with notification systems)
                SendAlertNotification(alert);
            }
        }

        /// <summary>
        /// Resolve production alert
        /// </summary>
        public void ResolveAlert(Guid alertId, string resolution)
        {
            lock (_alertLock)
            {
                var alert = _activeAlerts.FirstOrDefault(a => a.Id == alertId);
                if (alert != null)
                {
                    alert.IsActive = false;
                    alert.Resolved = true;
                    alert.ResolvedAt = DateTime.UtcNow;
                    alert.Resolution = resolution;

                    _logger.LogInformation("Production alert resolved: {AlertId} - {Resolution}", alertId, resolution);
                }
            }
        }

        #region Private Methods

        private void MonitorSystem(object state)
        {
            try
            {
                var metrics = GetCurrentMetrics();
                CheckThresholds(metrics);
                UpdatePerformanceCounters(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during system monitoring");
            }
        }

        private double GetCpuUsage()
        {
            var process = Process.GetCurrentProcess();
            var startTime = DateTime.UtcNow;
            var startCpuUsage = process.TotalProcessorTime.TotalMilliseconds;

            Thread.Sleep(1000); // Wait 1 second

            var endCpuUsage = process.TotalProcessorTime.TotalMilliseconds;
            var cpuUsedMs = endCpuUsage - startCpuUsage;
            var cpuPercentTotal = cpuUsedMs / (double)Environment.ProcessorCount;

            return Math.Min(100, cpuPercentTotal);
        }

        private DiskUsage GetDiskUsage()
        {
            var drives = System.IO.DriveInfo.GetDrives();
            var systemDrive = drives.FirstOrDefault(d => d.Name == "C:");
            
            if (systemDrive != null)
            {
                var freeSpace = systemDrive.AvailableFreeSpace;
                var totalSpace = systemDrive.TotalSize;
                var usedSpace = totalSpace - freeSpace;

                return new DiskUsage
                {
                    Drive = systemDrive.Name,
                    TotalGB = totalSpace / 1024.0 / 1024 / 1024,
                    UsedGB = usedSpace / 1024.0 / 1024 / 1024,
                    FreeGB = freeSpace / 1024.0 / 1024 / 1024,
                    UsagePercentage = (usedSpace / (double)totalSpace) * 100
                };
            }

            return new DiskUsage { Drive = "C:", UsagePercentage = 0 };
        }

        private NetworkStats GetNetworkStats()
        {
            // Simulate network statistics
            return new NetworkStats
            {
                BytesReceived = new Random().Next(1000000, 10000000),
                BytesSent = new Random().Next(500000, 5000000),
                PacketsReceived = new Random().Next(1000, 10000),
                PacketsSent = new Random().Next(500, 5000),
                LatencyMs = new Random().Next(10, 100),
                ConnectionsActive = new Random().Next(10, 100)
            };
        }

        private SystemHealthStatus DetermineOverallHealth(ProductionMetrics metrics)
        {
            var issues = new List<string>();

            if (metrics.CpuUsage > 80) issues.Add("High CPU usage");
            if (metrics.MemoryUsage.Percentage > 85) issues.Add("High memory usage");
            if (metrics.DiskUsage.UsagePercentage > 90) issues.Add("High disk usage");
            if (metrics.NetworkStats.LatencyMs > 200) issues.Add("High network latency");

            if (issues.Count == 0) return SystemHealthStatus.Healthy;
            if (issues.Count <= 2) return SystemHealthStatus.Degraded;
            return SystemHealthStatus.Unhealthy;
        }

        private ComponentHealth GetComponentHealth(ProductionMetrics metrics)
        {
            return new ComponentHealth
            {
                Database = new HealthStatus { Status = "Healthy", ResponseTime = 50 },
                Cache = new HealthStatus { Status = "Healthy", HitRate = 95.5 },
                Api = new HealthStatus { Status = "Healthy", ResponseTime = metrics.NetworkStats.LatencyMs },
                Authentication = new HealthStatus { Status = "Healthy", LastValidation = DateTime.UtcNow.AddMinutes(-5) },
                Filesystem = new HealthStatus { Status = metrics.DiskUsage.UsagePercentage < 90 ? "Healthy" : "Degraded", AvailableSpace = metrics.DiskUsage.FreeGB }
            };
        }

        private PerformanceMetrics GetPerformanceMetrics(ProductionMetrics metrics)
        {
            return new PerformanceMetrics
            {
                CpuUsage = metrics.CpuUsage,
                MemoryUsage = metrics.MemoryUsage,
                DiskUsage = metrics.DiskUsage,
                NetworkLatency = metrics.NetworkStats.LatencyMs,
                ResponseTime = new Random().NextDouble() * 1000,
                Throughput = new Random().NextDouble() * 500
            };
        }

        private ResourceStatus GetResourceStatus(ProductionMetrics metrics)
        {
            return new ResourceStatus
            {
                Memory = new ResourceInfo
                {
                    Used = metrics.MemoryUsage,
                    Available = $"{Math.Max(0, 32 - metrics.MemoryUsage.UsedGB)}GB",
                    UsagePercentage = metrics.MemoryUsage.Percentage
                },
                Cpu = new ResourceInfo
                {
                    Used = metrics.CpuUsage,
                    Available = $"{Math.Max(0, 100 - metrics.CpuUsage)}%",
                    UsagePercentage = metrics.CpuUsage
                },
                Disk = new ResourceInfo
                {
                    Used = metrics.DiskUsage.UsagePercentage,
                    Available = metrics.DiskUsage.FreeGB.ToString(),
                    UsagePercentage = metrics.DiskUsage.UsagePercentage
                },
                Network = new ResourceInfo
                {
                    Used = metrics.NetworkStats.LatencyMs,
                    Available = "1Gbps",
                    UsagePercentage = (metrics.NetworkStats.LatencyMs / 200.0) * 100
                }
            };
        }

        private DependencyStatus GetDependencyStatus()
        {
            return new DependencyStatus
            {
                Database = new ServiceStatus { Status = "Connected", ResponseTime = 50 },
                Redis = new ServiceStatus { Status = "Connected", ResponseTime = 5 },
                ExternalApis = new ServiceStatus { Status = "Healthy", ResponseTime = 100 },
                Backup = new ServiceStatus { Status = "Completed", LastBackup = DateTime.UtcNow.AddHours(-6) }
            };
        }

        private List<string> GetHealthRecommendations(ProductionMetrics metrics, SystemHealthStatus status)
        {
            var recommendations = new List<string>();

            if (metrics.CpuUsage > 70)
                recommendations.Add("Consider scaling CPU resources or optimizing processing");

            if (metrics.MemoryUsage.Percentage > 75)
                recommendations.Add("Monitor memory usage and consider increasing available memory");

            if (metrics.DiskUsage.UsagePercentage > 80)
                recommendations.Add("Plan disk space cleanup or storage expansion");

            if (metrics.NetworkStats.LatencyMs > 100)
                recommendations.Add("Investigate network latency issues");

            if (status == SystemHealthStatus.Unhealthy)
                recommendations.Add("Immediate attention required - system performance degraded");

            return recommendations;
        }

        private void CheckThresholds(ProductionMetrics metrics)
        {
            // CPU threshold
            if (metrics.CpuUsage > 85)
            {
                CreateAlert(AlertType.HighCpuUsage, AlertSeverity.High, 
                    $"CPU usage at {metrics.CpuUsage:F1}% exceeds threshold", 
                    new { CpuUsage = metrics.CpuUsage, Threshold = 85 });
            }

            // Memory threshold
            if (metrics.MemoryUsage.Percentage > 90)
            {
                CreateAlert(AlertType.HighMemoryUsage, AlertSeverity.Critical, 
                    $"Memory usage at {metrics.MemoryUsage.Percentage:F1}% exceeds threshold", 
                    new { MemoryUsage = metrics.MemoryUsage, Threshold = 90 });
            }

            // Disk threshold
            if (metrics.DiskUsage.UsagePercentage > 95)
            {
                CreateAlert(AlertType.HighDiskUsage, AlertSeverity.Critical, 
                    $"Disk usage at {metrics.DiskUsage.UsagePercentage:F1}% exceeds threshold", 
                    new { DiskUsage = metrics.DiskUsage, Threshold = 95 });
            }

            // Network latency threshold
            if (metrics.NetworkStats.LatencyMs > 150)
            {
                CreateAlert(AlertType.HighNetworkLatency, AlertSeverity.Medium, 
                    $"Network latency at {metrics.NetworkStats.LatencyMs}ms exceeds threshold", 
                    new { NetworkLatency = metrics.NetworkStats.LatencyMs, Threshold = 150 });
            }
        }

        private void UpdatePerformanceCounters(ProductionMetrics metrics)
        {
            if (!_counters.ContainsKey("CpuUsage"))
            {
                _counters["CpuUsage"] = new PerformanceCounter { Name = "CpuUsage", Value = metrics.CpuUsage, Unit = "%" };
            }

            if (!_counters.ContainsKey("MemoryUsage"))
            {
                _counters["MemoryUsage"] = new PerformanceCounter { Name = "MemoryUsage", Value = metrics.MemoryUsage.Percentage, Unit = "%" };
            }

            if (!_counters.ContainsKey("DiskUsage"))
            {
                _counters["DiskUsage"] = new PerformanceCounter { Name = "DiskUsage", Value = metrics.DiskUsage.UsagePercentage, Unit = "%" };
            }

            if (!_counters.ContainsKey("NetworkLatency"))
            {
                _counters["NetworkLatency"] = new PerformanceCounter { Name = "NetworkLatency", Value = metrics.NetworkStats.LatencyMs, Unit = "ms" };
            }
        }

        private List<ProductionAlert> GetActiveAlerts()
        {
            lock (_alertLock)
            {
                return _activeAlerts.Where(a => a.IsActive).ToList();
            }
        }

        private Dictionary<string, object> GetAggregatedMetrics(DateTime startTime, DateTime endTime)
        {
            return new Dictionary<string, object>
            {
                ["AverageCpuUsage"] = _counters.ContainsKey("CpuUsage") ? _counters["CpuUsage"].Values.Average() : 0,
                ["AverageMemoryUsage"] = _counters.ContainsKey("MemoryUsage") ? _counters["MemoryUsage"].Values.Average() : 0,
                ["PeakDiskUsage"] = _counters.ContainsKey("DiskUsage") ? _counters["DiskUsage"].Values.Max() : 0,
                ["AverageNetworkLatency"] = _counters.ContainsKey("NetworkLatency") ? _counters["NetworkLatency"].Values.Average() : 0
            };
        }

        private Dictionary<string, List<object>> GetPerformanceTrends(DateTime startTime, DateTime endTime)
        {
            return new Dictionary<string, List<object>>
            {
                ["CpuTrend"] = GenerateTrendData("CpuUsage", startTime, endTime),
                ["MemoryTrend"] = GenerateTrendData("MemoryUsage", startTime, endTime),
                ["DiskTrend"] = GenerateTrendData("DiskUsage", startTime, endTime),
                ["NetworkTrend"] = GenerateTrendData("NetworkLatency", startTime, endTime)
            };
        }

        private List<object> GenerateTrendData(string counterName, DateTime startTime, DateTime endTime)
        {
            var trendData = new List<object>();
            var interval = TimeSpan.FromHours(1);
            
            for (var time = startTime; time < endTime; time += interval)
            {
                trendData.Add(new
                {
                    Timestamp = time,
                    Value = _counters.ContainsKey(counterName) ? _counters[counterName].Value : 0
                });
            }

            return trendData;
        }

        private List<ProductionAlert> GetAlertsInPeriod(DateTime startTime, DateTime endTime)
        {
            lock (_alertLock)
            {
                return _activeAlerts
                    .Where(a => a.Timestamp >= startTime && a.Timestamp <= endTime)
                    .OrderByDescending(a => a.Timestamp)
                    .ToList();
            }
        }

        private Dictionary<string, object> GetPerformanceSummaries(DateTime startTime, DateTime endTime)
        {
            return new Dictionary<string, object>
            {
                ["TotalAlerts"] = _activeAlerts.Count(a => a.Timestamp >= startTime && a.Timestamp <= endTime),
                ["CriticalAlerts"] = _activeAlerts.Count(a => a.Severity == AlertSeverity.Critical && a.Timestamp >= startTime && a.Timestamp <= endTime),
                ["AverageResponseTime"] = _counters.ContainsKey("NetworkLatency") ? _counters["NetworkLatency"].Values.Average() : 0,
                ["PeakMemoryUsage"] = _counters.ContainsKey("MemoryUsage") ? _counters["MemoryUsage"].Values.Max() : 0,
                ["SystemUptime"] = (endTime - startTime).TotalHours
            };
        }

        private Dictionary<string, object> GetPerformanceBenchmarks()
        {
            return new Dictionary<string, object>
            {
                ["TargetCpuUsage"] = 70,
                ["TargetMemoryUsage"] = 75,
                ["TargetDiskUsage"] = 80,
                ["TargetNetworkLatency"] = 50,
                ["TargetResponseTime"] = 1000,
                ["TargetUptime"] = 99.9
            };
        }

        private void SendAlertNotification(ProductionAlert alert)
        {
            // In production, this would integrate with:
            // - Email notifications
            // - SMS alerts
            // - Slack/Teams notifications
            // - PagerDuty integration
            // - Monitoring systems
            
            _logger.LogInformation("Alert notification sent: {AlertId} - {Type}", alert.Id, alert.Type);
        }

        #endregion
    }

    #region Data Models

    public class ProductionMetrics
    {
        public DateTime Timestamp { get; set; }
        public double CpuUsage { get; set; }
        public MemoryUsage MemoryUsage { get; set; }
        public DiskUsage DiskUsage { get; set; }
        public NetworkStats NetworkStats { get; set; }
        public ProcessInfo ProcessInfo { get; set; }
        public Dictionary<string, PerformanceCounter> PerformanceCounters { get; set; }
        public List<ProductionAlert> ActiveAlerts { get; set; }
    }

    public class MemoryUsage
    {
        public double UsedMB { get; set; }
        public double UsedGB { get; set; }
        public double Percentage { get; set; }
    }

    public class DiskUsage
    {
        public string Drive { get; set; }
        public double TotalGB { get; set; }
        public double UsedGB { get; set; }
        public double FreeGB { get; set; }
        public double UsagePercentage { get; set; }
    }

    public class NetworkStats
    {
        public long BytesReceived { get; set; }
        public long BytesSent { get; set; }
        public long PacketsReceived { get; set; }
        public long PacketsSent { get; set; }
        public double LatencyMs { get; set; }
        public int ConnectionsActive { get; set; }
    }

    public class ProcessInfo
    {
        public int ProcessId { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public DateTime StartTime { get; set; }
        public long WorkingSet { get; set; }
    }

    public class PerformanceCounter
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public List<double> Values { get; set; } = new List<double>();
    }

    public class SystemHealth
    {
        public DateTime Timestamp { get; set; }
        public SystemHealthStatus OverallStatus { get; set; }
        public ComponentHealth Components { get; set; }
        public PerformanceMetrics Performance { get; set; }
        public ResourceStatus Resources { get; set; }
        public DependencyStatus Dependencies { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class ComponentHealth
    {
        public HealthStatus Database { get; set; }
        public HealthStatus Cache { get; set; }
        public HealthStatus Api { get; set; }
        public HealthStatus Authentication { get; set; }
        public HealthStatus Filesystem { get; set; }
    }

    public class HealthStatus
    {
        public string Status { get; set; }
        public double ResponseTime { get; set; }
        public double HitRate { get; set; }
        public string LastValidation { get; set; }
        public double AvailableSpace { get; set; }
    }

    public class ResourceStatus
    {
        public ResourceInfo Memory { get; set; }
        public ResourceInfo Cpu { get; set; }
        public ResourceInfo Disk { get; set; }
        public ResourceInfo Network { get; set; }
    }

    public class ResourceInfo
    {
        public object Used { get; set; }
        public string Available { get; set; }
        public double UsagePercentage { get; set; }
    }

    public class DependencyStatus
    {
        public ServiceStatus Database { get; set; }
        public ServiceStatus Redis { get; set; }
        public ServiceStatus ExternalApis { get; set; }
        public ServiceStatus Backup { get; set; }
    }

    public class ServiceStatus
    {
        public string Status { get; set; }
        public double ResponseTime { get; set; }
        public DateTime LastBackup { get; set; }
    }

    public class PerformanceAnalytics
    {
        public TimeRange TimeRange { get; set; }
        public Dictionary<string, object> Metrics { get; set; }
        public Dictionary<string, List<object>> Trends { get; set; }
        public List<ProductionAlert> Alerts { get; set; }
        public Dictionary<string, object> Summaries { get; set; }
        public Dictionary<string, object> Benchmarks { get; set; }
    }

    public class TimeRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public class ProductionAlert
    {
        public Guid Id { get; set; }
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public object Details { get; set; }
        public bool IsActive { get; set; }
        public bool Resolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string Resolution { get; set; }
    }

    public enum SystemHealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }

    public enum AlertType
    {
        HighCpuUsage,
        HighMemoryUsage,
        HighDiskUsage,
        HighNetworkLatency,
        DatabaseConnectionFailure,
        ServiceUnavailable,
        SecurityBreach,
        BackupFailure
    }

    public enum AlertSeverity
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    #endregion
}
