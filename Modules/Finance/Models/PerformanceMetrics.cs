namespace SecureERP2.Modules.Finance.Models;

public class PerformanceMetrics
{
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double RequestLatencyMs { get; set; }
    public int ActiveConnections { get; set; }
}
