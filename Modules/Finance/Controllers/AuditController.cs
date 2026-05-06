using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using SecureERP2.Modules.Finance.Services.Audit;
using SecureERP2.Modules.Finance.Entities;

namespace SecureERP2.Modules.Finance.Controllers
{
    /// <summary>
    /// 🔒 LAYER 1: External Audit Proof Controller
    /// Generate and export audit snapshots for independent verification
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AuditController : ControllerBase
    {
        private readonly ILogger<AuditController> _logger;
        private readonly ExternalAuditProofService _auditProofService;
        
        public AuditController(
            ILogger<AuditController> logger,
            ExternalAuditProofService auditProofService)
        {
            _logger = logger;
            _auditProofService = auditProofService;
        }
        
        /// <summary>
        /// Generate audit snapshot for company
        /// </summary>
        [HttpPost("snapshot/{companyId}")]
        public async Task<ActionResult<AuditSnapshot>> GenerateSnapshot(int companyId)
        {
            try
            {
                _logger.LogInformation("Generating audit snapshot for company {CompanyId}", companyId);
                
                var snapshot = await _auditProofService.GenerateAuditSnapshotAsync(companyId);
                
                return Ok(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audit snapshot for company {CompanyId}", companyId);
                return StatusCode(500, new { error = "Failed to generate audit snapshot" });
            }
        }
        
        /// <summary>
        /// Export audit snapshot as JSON (for auditors)
        /// </summary>
        [HttpGet("snapshot/{companyId}/{snapshotId}/export")]
        public async Task<ActionResult<AuditSnapshotExport>> ExportSnapshot(int companyId, Guid snapshotId)
        {
            try
            {
                _logger.LogInformation("Exporting audit snapshot {SnapshotId} for company {CompanyId}", snapshotId, companyId);
                
                var export = await _auditProofService.ExportSnapshotAsync(companyId, snapshotId);
                
                return Ok(export);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Snapshot not found: {SnapshotId} for company {CompanyId}", snapshotId, companyId);
                return NotFound(new { error = "Snapshot not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting audit snapshot {SnapshotId} for company {CompanyId}", snapshotId, companyId);
                return StatusCode(500, new { error = "Failed to export audit snapshot" });
            }
        }
        
        /// <summary>
        /// Verify audit snapshot (independent verification)
        /// This endpoint allows auditors to verify snapshots without trusting the system
        /// </summary>
        [HttpPost("verify")]
        [AllowAnonymous] // Allow anonymous access for independent verification
        public async Task<ActionResult<AuditVerificationResult>> VerifySnapshot([FromBody] VerifySnapshotRequest request)
        {
            try
            {
                _logger.LogInformation("Verifying audit snapshot independently");
                
                var result = await _auditProofService.VerifySnapshotAsync(request.SnapshotJson, request.PublicKeyPem);
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying audit snapshot");
                return StatusCode(500, new { error = "Failed to verify audit snapshot" });
            }
        }
        
        /// <summary>
        /// Get public key for verification
        /// </summary>
        [HttpGet("public-key/{companyId}")]
        public async Task<ActionResult<string>> GetPublicKey(int companyId)
        {
            try
            {
                // 🔥 Get latest snapshot to extract public key
                var latestSnapshot = await _auditProofService.GetLatestSnapshotAsync(companyId);
                
                if (latestSnapshot == null)
                {
                    return NotFound(new { error = "No snapshots found for company" });
                }
                
                return Ok(latestSnapshot.PublicKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting public key for company {CompanyId}", companyId);
                return StatusCode(500, new { error = "Failed to get public key" });
            }
        }
    }
    
    public class VerifySnapshotRequest
    {
        public string SnapshotJson { get; set; } = string.Empty;
        public string PublicKeyPem { get; set; } = string.Empty; // Optional, uses snapshot's public key if not provided
    }
}
