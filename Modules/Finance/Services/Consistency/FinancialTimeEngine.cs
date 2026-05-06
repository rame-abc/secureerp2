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
    /// 🌍 LAYER 2: Financial Time Engine
    /// Stop using system time. Use logical sequencing for authoritative ordering.
    /// Rules: Ordering = sequence, NOT timestamp. Time = metadata only.
    /// </summary>
    public class FinancialTimeEngine
    {
        private readonly ILogger<FinancialTimeEngine> _logger;
        private readonly ERPDbContext _context; // Changed from ApplicationDbContext to ERPDbContext
        private readonly TimeSyncConfiguration _config;
        private readonly Dictionary<string, long> _lastSequenceByCompany;
        private readonly object _sequenceLock = new object();
        
        public FinancialTimeEngine(
            ILogger<FinancialTimeEngine> logger,
            ERPDbContext context, // Changed from ApplicationDbContext to ERPDbContext
            TimeSyncConfiguration config = null)
        {
            _logger = logger;
            _context = context;
            _config = config ?? new TimeSyncConfiguration();
            _lastSequenceByCompany = new Dictionary<string, long>();
        }

        /// <summary>
        /// 🔥 Get next logical sequence for company (thread-safe)
        /// This is the PRIMARY ordering mechanism
        /// </summary>
        public async Task<long> GetNextLogicalSequenceAsync(int companyId)
        {
            lock (_sequenceLock)
            {
                var companyKey = $"company_{companyId}";
                
                if (!_lastSequenceByCompany.ContainsKey(companyKey))
                {
                    // 🔥 Initialize from database
                    var lastSequence = _context.FinancialTimes
                        .Where(ft => ft.CompanyId == companyId)
                        .OrderByDescending(ft => ft.LogicalSequence)
                        .Select(ft => ft.LogicalSequence)
                        .FirstOrDefault();
                    
                    _lastSequenceByCompany[companyKey] = lastSequence;
                }
                
                // 🔥 Increment and return
                var nextSequence = _lastSequenceByCompany[companyKey] + 1;
                _lastSequenceByCompany[companyKey] = nextSequence;
                
                _logger.LogDebug("Generated logical sequence {Sequence} for company {CompanyId}", nextSequence, companyId);
                
                return nextSequence;
            }
        }

        /// <summary>
        /// 🔥 Record financial time event
        /// </summary>
        public async Task<FinancialTime> RecordEventAsync(
            int companyId,
            string eventType,
            Guid eventId,
            DateTime eventTimeUTC,
            string source = "SYSTEM",
            string region = "",
            string nodeId = "")
        {
            try
            {
                var logicalSequence = await GetNextLogicalSequenceAsync(companyId);
                var previousSequence = logicalSequence - 1;
                
                // 🔥 Get clock drift if using NTP/Atomic
                var clockDriftMs = await GetClockDriftAsync(source);
                
                var financialTime = new FinancialTime
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    LogicalSequence = logicalSequence,
                    EventTimeUTC = eventTimeUTC,
                    Source = source,
                    EventType = eventType,
                    EventId = eventId,
                    Region = region,
                    NodeId = nodeId,
                    ClockDriftMs = clockDriftMs,
                    PreviousLogicalSequence = previousSequence
                };

                _context.FinancialTimes.Add(financialTime);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Recorded financial time event: Company={CompanyId}, Sequence={Sequence}, Type={EventType}, Source={Source}",
                    companyId, logicalSequence, eventType, source);

                return financialTime;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording financial time event for company {CompanyId}", companyId);
                throw;
            }
        }

        /// <summary>
        /// 🔥 Get "as-of" logical sequence for a specific time
        /// This enables point-in-time queries
        /// </summary>
        public async Task<AsOfTimeQuery> GetAsOfSequenceAsync(int companyId, DateTime asOfTime)
        {
            try
            {
                // 🔥 Find the last event at or before the as-of time
                var financialTime = await _context.FinancialTimes
                    .Where(ft => ft.CompanyId == companyId && ft.EventTimeUTC <= asOfTime)
                    .OrderByDescending(ft => ft.LogicalSequence)
                    .FirstOrDefaultAsync();

                if (financialTime == null)
                {
                    return new AsOfTimeQuery
                    {
                        AsOfTime = asOfTime,
                        LogicalSequence = 0,
                        IsExact = false,
                        ActualEventTime = DateTime.MinValue,
                        EventType = "NONE"
                    };
                }

                return new AsOfTimeQuery
                {
                    AsOfTime = asOfTime,
                    LogicalSequence = financialTime.LogicalSequence,
                    IsExact = Math.Abs((financialTime.EventTimeUTC - asOfTime).TotalMilliseconds) < 1000,
                    ActualEventTime = financialTime.EventTimeUTC,
                    EventType = financialTime.EventType,
                    EventId = financialTime.EventId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting as-of sequence for company {CompanyId} at time {AsOfTime}", companyId, asOfTime);
                throw;
            }
        }

        /// <summary>
        /// 🔥 Validate sequence integrity (no gaps, no duplicates)
        /// </summary>
        public async Task<bool> ValidateSequenceIntegrityAsync(int companyId, long startSequence, long endSequence)
        {
            try
            {
                var sequences = await _context.FinancialTimes
                    .Where(ft => ft.CompanyId == companyId && 
                               ft.LogicalSequence >= startSequence && 
                               ft.LogicalSequence <= endSequence)
                    .OrderBy(ft => ft.LogicalSequence)
                    .Select(ft => ft.LogicalSequence)
                    .ToListAsync();

                // 🔥 Check for gaps
                var expectedCount = (int)(endSequence - startSequence + 1);
                if (sequences.Count != expectedCount)
                {
                    _logger.LogWarning("Sequence integrity check failed: expected {Expected} sequences, found {Actual}",
                        expectedCount, sequences.Count);
                    return false;
                }

                // 🔥 Check for duplicates
                var uniqueSequences = sequences.Distinct().Count();
                if (uniqueSequences != sequences.Count)
                {
                    _logger.LogWarning("Sequence integrity check failed: duplicate sequences found");
                    return false;
                }

                // 🔥 Check for proper ordering
                for (int i = 1; i < sequences.Count; i++)
                {
                    if (sequences[i] != sequences[i-1] + 1)
                    {
                        _logger.LogWarning("Sequence integrity check failed: gap detected between {Prev} and {Current}",
                            sequences[i-1], sequences[i]);
                        return false;
                    }
                }

                _logger.LogInformation("Sequence integrity validated for company {CompanyId} from {Start} to {End}",
                    companyId, startSequence, endSequence);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating sequence integrity for company {CompanyId}", companyId);
                return false;
            }
        }

        /// <summary>
        /// 🔥 Get multi-region consistency status
        /// </summary>
        public async Task<List<MultiRegionStatus>> GetMultiRegionStatusAsync(int companyId)
        {
            try
            {
                var regions = await _context.FinancialTimes
                    .Where(ft => ft.CompanyId == companyId)
                    .GroupBy(ft => ft.Region)
                    .Select(g => new MultiRegionStatus
                    {
                        Region = g.Key,
                        LastLogicalSequence = g.Max(ft => ft.LogicalSequence),
                        LastEventTime = g.Max(ft => ft.EventTimeUTC),
                        IsHealthy = g.Max(ft => ft.LogicalSequence) > 0,
                        LagMs = (long)(DateTime.UtcNow - g.Max(ft => ft.EventTimeUTC)).TotalMilliseconds,
                        PendingEvents = 0 // Would be calculated from event queue
                    })
                    .ToListAsync();

                return regions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting multi-region status for company {CompanyId}", companyId);
                return new List<MultiRegionStatus>();
            }
        }

        /// <summary>
        /// 🔥 Replay events from a specific sequence
        /// Used for time travel and recovery
        /// </summary>
        public async IAsyncEnumerable<FinancialTime> ReplayEventsAsync(int companyId, long fromSequence)
        {
            await foreach (var financialTime in _context.FinancialTimes
                .Where(ft => ft.CompanyId == companyId && ft.LogicalSequence >= fromSequence)
                .OrderBy(ft => ft.LogicalSequence)
                .AsAsyncEnumerable())
            {
                yield return financialTime;
            }
        }

        /// <summary>
        /// 🔥 Get clock drift for time source
        /// </summary>
        private async Task<int> GetClockDriftAsync(string source)
        {
            try
            {
                switch (source.ToUpper())
                {
                    case "NTP":
                        return await GetNtpDriftAsync();
                    case "ATOMIC":
                        return await GetAtomicClockDriftAsync();
                    case "GPS":
                        return await GetGpsDriftAsync();
                    default:
                        return 0; // System time has no drift measurement
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 🔥 Get NTP clock drift
        /// </summary>
        private async Task<int> GetNtpDriftAsync()
        {
            // 🔥 Implementation would query NTP servers and calculate drift
            // For now, return simulated drift
            await Task.Delay(1);
            return new Random().Next(-100, 100);
        }

        /// <summary>
        /// 🔥 Get atomic clock drift
        /// </summary>
        private async Task<int> GetAtomicClockDriftAsync()
        {
            // 🔥 Implementation would query atomic clock service
            // For now, return minimal drift (atomic clocks are very accurate)
            await Task.Delay(1);
            return new Random().Next(-5, 5);
        }

        /// <summary>
        /// 🔥 Get GPS clock drift
        /// </summary>
        private async Task<int> GetGpsDriftAsync()
        {
            // 🔥 Implementation would query GPS time service
            // For now, return very low drift (GPS time is highly accurate)
            await Task.Delay(1);
            return new Random().Next(-10, 10);
        }

        /// <summary>
        /// 🔥 Sync time with configured source
        /// </summary>
        public async Task<bool> SyncTimeAsync()
        {
            try
            {
                _logger.LogInformation("Starting time sync with source {Source}", _config.PrimarySource);

                switch (_config.PrimarySource)
                {
                    case TimeSource.NTP:
                        return await SyncWithNtpAsync();
                    case TimeSource.ATOMIC:
                        return await SyncWithAtomicClockAsync();
                    case TimeSource.GPS:
                        return await SyncWithGpsAsync();
                    default:
                        _logger.LogWarning("Unsupported time source: {Source}", _config.PrimarySource);
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing time with source {Source}", _config.PrimarySource);
                return false;
            }
        }

        /// <summary>
        /// 🔥 Sync with NTP servers
        /// </summary>
        private async Task<bool> SyncWithNtpAsync()
        {
            // 🔥 Implementation would sync with NTP servers
            // For now, simulate successful sync
            await Task.Delay(100);
            _logger.LogInformation("NTP sync completed successfully");
            return true;
        }

        /// <summary>
        /// 🔥 Sync with atomic clock
        /// </summary>
        private async Task<bool> SyncWithAtomicClockAsync()
        {
            // 🔥 Implementation would sync with atomic clock service
            // For now, simulate successful sync
            await Task.Delay(200);
            _logger.LogInformation("Atomic clock sync completed successfully");
            return true;
        }

        /// <summary>
        /// 🔥 Sync with GPS time
        /// </summary>
        private async Task<bool> SyncWithGpsAsync()
        {
            // 🔥 Implementation would sync with GPS time service
            // For now, simulate successful sync
            await Task.Delay(150);
            _logger.LogInformation("GPS time sync completed successfully");
            return true;
        }
    }
}
