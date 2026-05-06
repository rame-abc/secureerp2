namespace SecureERP2.Modules.Finance.Models;

public class PerformanceMetrics
{
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double ResponseTimeMs { get; set; }
    public double DiskUsage { get; set; }
    public double NetworkLatency { get; set; }
    public double Throughput { get; set; }
    public DateTime Timestamp { get; set; }
}
