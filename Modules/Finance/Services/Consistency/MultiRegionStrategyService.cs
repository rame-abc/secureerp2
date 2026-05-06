using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureERP2.Modules.Finance.Entities;

namespace SecureERP2.Modules.Finance.Services.Consistency
{
    /// <summary>
    /// 🌍 LAYER 2: Multi-Region Strategy (CRITICAL)
    /// Split system: Ledger = Single writer (strong consistency), Read models = Multi-region (eventual)
    /// NEVER allow: Multi-region writes to ledger (causes split-brain + financial corruption)
    /// </summary>
    public class MultiRegionStrategyService
    {
        private readonly ILogger<MultiRegionStrategyService> _logger;
        private readonly ERPDbContext _context; // Changed from ApplicationDbContext to ERPDbContext
        private readonly FinancialTimeEngine _timeEngine;
        private readonly Dictionary<string, RegionConfig> _regionConfigs;
        
        public MultiRegionStrategyService(
            ILogger<MultiRegionStrategyService> logger,
            ERPDbContext context, // Changed from ApplicationDbContext to ERPDbContext
            FinancialTimeEngine timeEngine)
        {
            _logger = logger;
            _context = context;
            _timeEngine = timeEngine;
            _regionConfigs = new Dictionary<string, RegionConfig>
            {
                ["us-east-1"] = new RegionConfig { Name = "us-east-1", IsPrimary = true, AllowsWrites = true },
                ["eu-west-1"] = new RegionConfig { Name = "eu-west-1", IsPrimary = false, AllowsWrites = false },
                ["ap-southeast-1"] = new RegionConfig { Name = "ap-southeast-1", IsPrimary = false, AllowsWrites = false }
            };
        }

        /// <summary>
        /// 🔥 Check if region allows ledger writes
        /// </summary>
        public bool AllowsLedgerWrites(string region)
        {
            if (!_regionConfigs.ContainsKey(region))
            {
                _logger.LogWarning("Unknown region: {Region}", region);
                return false;
            }

            var config = _regionConfigs[region];
            var allowsWrites = config.AllowsWrites && config.IsPrimary;

            _logger.LogDebug("Region {Region} allows writes: {AllowsWrites}", region, allowsWrites);
            return allowsWrites;
        }

        /// <summary>
        /// 🔥 Get primary region for ledger writes
        /// </summary>
        public string GetPrimaryRegion()
        {
            var primaryRegion = _regionConfigs.FirstOrDefault(kvp => kvp.Value.IsPrimary);
            return primaryRegion.Key ?? "us-east-1";
        }

        /// <summary>
        /// 🔥 Validate ledger write operation
        /// Prevents multi-region writes to ledger (CRITICAL)
        /// </summary>
        public async Task<bool> ValidateLedgerWriteAsync(int companyId, string region, long logicalSequence)
        {
            try
            {
                // 🔥 Rule 1: Only primary region can write to ledger
                if (!AllowsLedgerWrites(region))
                {
                    _logger.LogError("LEDGER WRITE BLOCKED: Region {Region} is not allowed to write to ledger", region);
                    return false;
                }

                // 🔥 Rule 2: Validate sequence continuity
                var lastSequence = await GetLastLedgerSequenceAsync(companyId);
                if (logicalSequence != lastSequence + 1)
                {
                    _logger.LogError("LEDGER WRITE BLOCKED: Invalid sequence. Expected {Expected}, got {Actual}",
                        lastSequence + 1, logicalSequence);
                    return false;
                }

                // 🔥 Rule 3: Check for split-brain detection
                var splitBrainDetected = await DetectSplitBrainAsync(companyId);
                if (splitBrainDetected)
                {
                    _logger.LogError("LEDGER WRITE BLOCKED: Split-brain detected for company {CompanyId}", companyId);
                    return false;
                }

                _logger.LogInformation("LEDGER WRITE VALIDATED: Company={CompanyId}, Region={Region}, Sequence={Sequence}",
                    companyId, region, logicalSequence);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating ledger write for company {CompanyId}", companyId);
                return false;
            }
        }

        /// <summary>
        /// 🔥 Replicate to read models (eventual consistency)
        /// </summary>
        public async Task ReplicateToReadModelsAsync(int companyId, string eventType, Guid eventId, long logicalSequence)
        {
            try
            {
                _logger.LogInformation("Replicating event to read models: Company={CompanyId}, Event={EventType}, Sequence={Sequence}",
                    companyId, eventType, logicalSequence);

                // 🔥 Get all non-primary regions
                var readModelRegions = _regionConfigs.Where(kvp => !kvp.Value.IsPrimary).Select(kvp => kvp.Key);

                foreach (var region in readModelRegions)
                {
                    await ReplicateToRegionAsync(companyId, region, eventType, eventId, logicalSequence);
                }

                _logger.LogInformation("Event replication completed for {Count} regions", readModelRegions.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replicating event to read models for company {CompanyId}", companyId);
                throw;
            }
        }

        /// <summary>
        /// 🔥 Get multi-region consistency status
        /// </summary>
        public async Task<MultiRegionConsistencyReport> GetConsistencyReportAsync(int companyId)
        {
            try
            {
                var report = new MultiRegionConsistencyReport
                {
                    CompanyId = companyId,
                    GeneratedAt = DateTime.UtcNow,
                    PrimaryRegion = GetPrimaryRegion()
                };

                // 🔥 Get status for each region
                foreach (var regionConfig in _regionConfigs)
                {
                    var region = regionConfig.Key;
                    var config = regionConfig.Value;

                    var status = await GetRegionStatusAsync(companyId, region);
                    status.IsPrimary = config.IsPrimary;
                    status.AllowsWrites = config.AllowsWrites;

                    report.RegionStatuses[region] = status;
                }

                // 🔥 Check overall consistency
                report.IsConsistent = await ValidateOverallConsistencyAsync(companyId);
                report.SplitBrainDetected = await DetectSplitBrainAsync(companyId);

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating consistency report for company {CompanyId}", companyId);
                throw;
            }
        }

        /// <summary>
        /// 🔥 Detect split-brain condition
        /// </summary>
        public async Task<bool> DetectSplitBrainAsync(int companyId)
        {
            try
            {
                // 🔥 Check for multiple regions claiming to have the latest sequence
                var regionSequences = new Dictionary<string, long>();

                foreach (var region in _regionConfigs.Keys)
                {
                    var lastSequence = await GetLastSequenceInRegionAsync(companyId, region);
                    regionSequences[region] = lastSequence;
                }

                // 🔥 Find maximum sequence
                var maxSequence = regionSequences.Values.Max();
                var regionsWithMaxSequence = regionSequences.Where(kvp => kvp.Value == maxSequence).Select(kvp => kvp.Key).ToList();

                // 🔥 Split-brain detected if multiple regions have the same max sequence
                var splitBrainDetected = regionsWithMaxSequence.Count > 1;

                if (splitBrainDetected)
                {
                    _logger.LogError("SPLIT-BRAIN DETECTED: Multiple regions ({Regions}) claim sequence {Sequence}",
                        string.Join(", ", regionsWithMaxSequence), maxSequence);
                }

                return splitBrainDetected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting split-brain for company {CompanyId}", companyId);
                return true; // Assume split-brain on error (safety first)
            }
        }

        /// <summary>
        /// 🔥 Handle failover to new primary region
        /// </summary>
        public async Task<bool> HandleFailoverAsync(int companyId, string failedRegion, string newPrimaryRegion)
        {
            try
            {
                _logger.LogWarning("FAILOVER INITIATED: Company={CompanyId}, Failed={FailedRegion}, NewPrimary={NewPrimary}",
                    companyId, failedRegion, newPrimaryRegion);

                // 🔥 Validate new primary region exists
                if (!_regionConfigs.ContainsKey(newPrimaryRegion))
                {
                    _logger.LogError("FAILOVER FAILED: Unknown primary region {NewPrimaryRegion}", newPrimaryRegion);
                    return false;
                }

                // 🔥 Update region configurations
                _regionConfigs[failedRegion].IsPrimary = false;
                _regionConfigs[failedRegion].AllowsWrites = false;
                _regionConfigs[newPrimaryRegion].IsPrimary = true;
                _regionConfigs[newPrimaryRegion].AllowsWrites = true;

                // 🔥 Wait for convergence
                await Task.Delay(5000);

                // 🔥 Validate new primary is working
                var newPrimaryStatus = await GetRegionStatusAsync(companyId, newPrimaryRegion);
                if (!newPrimaryStatus.IsHealthy)
                {
                    _logger.LogError("FAILOVER FAILED: New primary region {NewPrimaryRegion} is unhealthy", newPrimaryRegion);
                    return false;
                }

                _logger.LogInformation("FAILOVER COMPLETED: New primary region is {NewPrimaryRegion}", newPrimaryRegion);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during failover for company {CompanyId}", companyId);
                return false;
            }
        }

        /// <summary>
        /// 🔥 Get last ledger sequence for company
        /// </summary>
        private async Task<long> GetLastLedgerSequenceAsync(int companyId)
        {
            return await _context.FinancialTimes
                .Where(ft => ft.CompanyId == companyId)
                .OrderByDescending(ft => ft.LogicalSequence)
                .Select(ft => ft.LogicalSequence)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// 🔥 Get last sequence in specific region
        /// </summary>
        private async Task<long> GetLastSequenceInRegionAsync(int companyId, string region)
        {
            return await _context.FinancialTimes
                .Where(ft => ft.CompanyId == companyId && ft.Region == region)
                .OrderByDescending(ft => ft.LogicalSequence)
                .Select(ft => ft.LogicalSequence)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// 🔥 Replicate event to specific region
        /// </summary>
        private async Task ReplicateToRegionAsync(int companyId, string region, string eventType, Guid eventId, long logicalSequence)
        {
            try
            {
                // 🔥 Simulate replication delay
                await Task.Delay(new Random().Next(100, 500));

                _logger.LogDebug("Replicated to region {Region}: Company={CompanyId}, Event={EventType}, Sequence={Sequence}",
                    region, companyId, eventType, logicalSequence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replicating to region {Region} for company {CompanyId}", region, companyId);
                throw;
            }
        }

        /// <summary>
        /// 🔥 Get region status
        /// </summary>
        private async Task<RegionStatus> GetRegionStatusAsync(int companyId, string region)
        {
            try
            {
                var lastSequence = await GetLastSequenceInRegionAsync(companyId, region);
                var lastEventTime = await _context.FinancialTimes
                    .Where(ft => ft.CompanyId == companyId && ft.Region == region)
                    .OrderByDescending(ft => ft.LogicalSequence)
                    .Select(ft => ft.EventTimeUTC)
                    .FirstOrDefaultAsync();

                return new RegionStatus
                {
                    Region = region,
                    LastLogicalSequence = lastSequence,
                    LastEventTime = lastEventTime,
                    IsHealthy = lastSequence > 0,
                    LagMs = lastEventTime == DateTime.MinValue ? 0 : (long)(DateTime.UtcNow - lastEventTime).TotalMilliseconds
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting status for region {Region}", region);
                return new RegionStatus
                {
                    Region = region,
                    IsHealthy = false,
                    LagMs = long.MaxValue
                };
            }
        }

        /// <summary>
        /// 🔥 Validate overall consistency
        /// </summary>
        private async Task<bool> ValidateOverallConsistencyAsync(int companyId)
        {
            try
            {
                var primaryRegion = GetPrimaryRegion();
                var primarySequence = await GetLastSequenceInRegionAsync(companyId, primaryRegion);

                // 🔥 Check that all read models are within acceptable lag
                foreach (var region in _regionConfigs.Keys.Where(r => r != primaryRegion))
                {
                    var regionSequence = await GetLastSequenceInRegionAsync(companyId, region);
                    var lag = primarySequence - regionSequence;

                    if (lag > 1000) // More than 1000 events behind
                    {
                        _logger.LogWarning("Region {Region} is too far behind: lag={Lag}", region, lag);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating overall consistency for company {CompanyId}", companyId);
                return false;
            }
        }
    }

    #region Supporting Classes

    /// <summary>
    /// Region configuration
    /// </summary>
    public class RegionConfig
    {
        public string Name { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public bool AllowsWrites { get; set; }
    }

    /// <summary>
    /// Region status
    /// </summary>
    public class RegionStatus
    {
        public string Region { get; set; } = string.Empty;
        public long LastLogicalSequence { get; set; }
        public DateTime LastEventTime { get; set; }
        public bool IsHealthy { get; set; }
        public long LagMs { get; set; }
        public bool IsPrimary { get; set; }
        public bool AllowsWrites { get; set; }
    }

    /// <summary>
    /// Multi-region consistency report
    /// </summary>
    public class MultiRegionConsistencyReport
    {
        public int CompanyId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string PrimaryRegion { get; set; } = string.Empty;
        public Dictionary<string, RegionStatus> RegionStatuses { get; set; } = new();
        public bool IsConsistent { get; set; }
        public bool SplitBrainDetected { get; set; }
    }

    #endregion
}
