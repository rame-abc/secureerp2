using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SecureERP2.Modules.Finance.Services.Infrastructure;

namespace SecureERP2.Modules.Finance.Controllers
{
    /// <summary>
    /// 🔒 Health check controller for deployment readiness
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly HealthCheckService _healthCheckService;
        private readonly ILogger<HealthController> _logger;

        public HealthController(
            HealthCheckService healthCheckService,
            ILogger<HealthController> logger)
        {
            _healthCheckService = healthCheckService;
            _logger = logger;
        }

        /// <summary>
        /// Basic health check endpoint
        /// </summary>
        [HttpGet("health")]
        public async Task<IActionResult> GetHealthAsync()
        {
            try
            {
                var result = await _healthCheckService.CheckOverallHealthAsync();
                
                var statusCode = result.Status switch
                {
                    HealthStatus.Healthy => 200,
                    HealthStatus.Degraded => 200, // Still OK but with warnings
                    HealthStatus.Unhealthy => 503, // Service unavailable
                    _ => 500
                };

                _logger.LogInformation("Health check completed with status {Status} - HTTP {StatusCode}", 
                    result.Status, statusCode);

                return StatusCode(statusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check endpoint failed");
                return StatusCode(500, new
                {
                    Status = HealthStatus.Unhealthy,
                    Description = "Health check service failed",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Detailed health check endpoint
        /// </summary>
        [HttpGet("health/detailed")]
        public async Task<IActionResult> GetDetailedHealthAsync()
        {
            try
            {
                var result = await _healthCheckService.CheckOverallHealthAsync();
                
                _logger.LogInformation("Detailed health check completed with status {Status}", result.Status);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Detailed health check endpoint failed");
                return StatusCode(500, new
                {
                    Status = HealthStatus.Unhealthy,
                    Description = "Detailed health check service failed",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Database-specific health check
        /// </summary>
        [HttpGet("health/database")]
        public async Task<IActionResult> GetDatabaseHealthAsync()
        {
            try
            {
                var result = await _healthCheckService.CheckDatabaseHealthAsync();
                
                var statusCode = result.Status == HealthStatus.Healthy ? 200 : 503;
                
                _logger.LogInformation("Database health check completed with status {Status}", result.Status);

                return StatusCode(statusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check endpoint failed");
                return StatusCode(500, new
                {
                    Status = HealthStatus.Unhealthy,
                    Description = "Database health check service failed",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Readiness probe (for Kubernetes/Docker)
        /// </summary>
        [HttpGet("health/ready")]
        public async Task<IActionResult> GetReadinessAsync()
        {
            try
            {
                var result = await _healthCheckService.CheckOverallHealthAsync();
                
                var isReady = result.Status == HealthStatus.Healthy;
                
                _logger.LogInformation("Readiness probe completed - Ready: {IsReady}", isReady);

                return isReady ? Ok(result) : StatusCode(503, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Readiness probe failed");
                return StatusCode(503, new
                {
                    Status = HealthStatus.Unhealthy,
                    Description = "Readiness probe failed",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Liveness probe (for Kubernetes/Docker)
        /// </summary>
        [HttpGet("health/live")]
        public IActionResult GetLiveness()
        {
            try
            {
                _logger.LogDebug("Liveness probe called");
                
                // Simple liveness check - if we can respond, we're alive
                return Ok(new
                {
                    Status = HealthStatus.Healthy,
                    Description = "Service is alive",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Liveness probe failed");
                return StatusCode(503, new
                {
                    Status = HealthStatus.Unhealthy,
                    Description = "Liveness probe failed",
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }

    /// <summary>
    /// Health status enumeration
    /// </summary>
    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }
}
