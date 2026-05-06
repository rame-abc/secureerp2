using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace SecureERP2.Modules.Finance.Services
{
    /// <summary>
    /// 🔒 PHASE 2.2: Concurrency Locks
    /// Enterprise-grade concurrency control for financial operations
    /// </summary>
    public class ConcurrencyLockService
    {
        private readonly ERPDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ConcurrencyLockService> _logger;

        // Cache settings
        private const string LockKeyPrefix = "concurrency_lock:";
        private const int DefaultLockTimeoutSeconds = 30;
        private const int CriticalLockTimeoutSeconds = 300; // 5 minutes for critical operations

        public ConcurrencyLockService(ERPDbContext context, IMemoryCache cache, ILogger<ConcurrencyLockService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// 🔒 Acquire a concurrency lock for a resource
        /// </summary>
        public async Task<LockResult> AcquireLockAsync(LockRequest request)
        {
            var result = new LockResult
            {
                LockKey = request.LockKey,
                ResourceType = request.ResourceType,
                ResourceId = request.ResourceId,
                RequestedAt = DateTime.UtcNow,
                RequestedBy = request.RequestedBy
            };

            try
            {
                // 🔒 Step 1: Validate lock request
                var validationResult = ValidateLockRequest(request);
                if (!validationResult.IsValid)
                {
                    result.Status = LockStatus.InvalidRequest;
                    result.ErrorMessage = validationResult.ErrorMessage;
                    return result;
                }

                // 🔒 Step 2: Check cache for existing lock
                var existingLock = await CheckExistingLockAsync(request);
                if (existingLock != null)
                {
                    // Check if lock is expired
                    if (IsLockExpired(existingLock))
                    {
                        await ReleaseExpiredLockAsync(existingLock);
                    }
                    else
                    {
                        result.Status = LockStatus.AlreadyLocked;
                        result.LockedBy = existingLock.LockedBy;
                        result.LockedAt = existingLock.CreatedAt;
                        result.ExpiresAt = existingLock.ExpiresAt;
                        return result;
                    }
                }

                // 🔒 Step 3: Check database for existing lock
                var dbLock = await GetDatabaseLockAsync(request);
                if (dbLock != null)
                {
                    if (IsLockExpired(dbLock))
                    {
                        await ReleaseDatabaseLockAsync(dbLock);
                    }
                    else
                    {
                        result.Status = LockStatus.AlreadyLocked;
                        result.LockedBy = dbLock.LockedBy;
                        result.LockedAt = dbLock.CreatedAt;
                        result.ExpiresAt = dbLock.ExpiresAt;
                        
                        // Update cache with database lock
                        await CacheLockAsync(dbLock);
                        return result;
                    }
                }

                // 🔒 Step 4: Create new lock
                var newLock = await CreateLockAsync(request);
                
                result.Status = LockStatus.Acquired;
                result.LockId = newLock.Id;
                result.LockedAt = newLock.CreatedAt;
                result.ExpiresAt = newLock.ExpiresAt;

                // 🔒 Step 5: Cache the lock
                await CacheLockAsync(newLock);

                return result;
            }
            catch (Exception ex)
            {
                result.Status = LockStatus.Error;
                result.ErrorMessage = $"Failed to acquire lock: {ex.Message}";
                _logger.LogError(ex, "Failed to acquire lock for {ResourceType}:{ResourceId}", 
                    request.ResourceType, request.ResourceId);
                return result;
            }
        }

        /// <summary>
        /// 🔒 Release a concurrency lock
        /// </summary>
        public async Task<ReleaseResult> ReleaseLockAsync(string lockKey, string requestedBy)
        {
            var result = new ReleaseResult
            {
                LockKey = lockKey,
                ReleasedBy = requestedBy,
                ReleasedAt = DateTime.UtcNow
            };

            try
            {
                // 🔒 Step 1: Get lock from cache
                var cachedLock = await GetCachedLockAsync(lockKey);
                if (cachedLock != null)
                {
                    // Verify ownership
                    if (cachedLock.LockedBy != requestedBy)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Lock can only be released by the owner";
                        return result;
                    }

                    // Remove from cache
                    await RemoveCachedLockAsync(lockKey);
                }

                // 🔒 Step 2: Get lock from database
                var dbLock = await GetDatabaseLockByKeyAsync(lockKey);
                if (dbLock != null)
                {
                    // Verify ownership
                    if (dbLock.LockedBy != requestedBy)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Lock can only be released by the owner";
                        return result;
                    }

                    // Remove from database
                    await ReleaseDatabaseLockAsync(dbLock);
                    result.Success = true;
                    result.LockId = dbLock.Id;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = "Lock not found";
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to release lock: {ex.Message}";
                _logger.LogError(ex, "Failed to release lock {LockKey}", lockKey);
                return result;
            }
        }

        /// <summary>
        /// 🔒 Extend a lock's timeout
        /// </summary>
        public async Task<ExtensionResult> ExtendLockAsync(string lockKey, string requestedBy, TimeSpan? extension = null)
        {
            var result = new ExtensionResult
            {
                LockKey = lockKey,
                ExtendedBy = requestedBy,
                ExtendedAt = DateTime.UtcNow
            };

            try
            {
                var extensionPeriod = extension ?? TimeSpan.FromSeconds(DefaultLockTimeoutSeconds);

                // 🔒 Step 1: Get lock from database
                var dbLock = await GetDatabaseLockByKeyAsync(lockKey);
                if (dbLock == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Lock not found";
                    return result;
                }

                // 🔒 Step 2: Verify ownership
                if (dbLock.LockedBy != requestedBy)
                {
                    result.Success = false;
                    result.ErrorMessage = "Lock can only be extended by the owner";
                    return result;
                }

                // 🔒 Step 3: Check if lock is expired
                if (IsLockExpired(dbLock))
                {
                    result.Success = false;
                    result.ErrorMessage = "Lock has expired";
                    return result;
                }

                // 🔒 Step 4: Extend the lock
                dbLock.ExpiresAt = DateTime.UtcNow.Add(extensionPeriod);
                dbLock.ExtendedAt = DateTime.UtcNow;
                dbLock.ExtensionCount += 1;

                _context.ConcurrencyLocks.Update(dbLock);
                await _context.SaveChangesAsync();

                // 🔒 Step 5: Update cache
                await CacheLockAsync(dbLock);

                result.Success = true;
                result.LockId = dbLock.Id;
                result.NewExpiresAt = dbLock.ExpiresAt;
                result.ExtensionCount = dbLock.ExtensionCount;

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to extend lock: {ex.Message}";
                _logger.LogError(ex, "Failed to extend lock {LockKey}", lockKey);
                return result;
            }
        }

        /// <summary>
        /// 🔒 Check lock status
        /// </summary>
        public async Task<LockStatusResult> CheckLockStatusAsync(string lockKey)
        {
            var result = new LockStatusResult
            {
                LockKey = lockKey,
                CheckedAt = DateTime.UtcNow
            };

            try
            {
                // 🔒 Check cache first
                var cachedLock = await GetCachedLockAsync(lockKey);
                if (cachedLock != null)
                {
                    if (IsLockExpired(cachedLock))
                    {
                        await RemoveCachedLockAsync(lockKey);
                        result.Status = LockStatus.NotLocked;
                    }
                    else
                    {
                        result.Status = LockStatus.Locked;
                        result.LockedBy = cachedLock.LockedBy;
                        result.LockedAt = cachedLock.CreatedAt;
                        result.ExpiresAt = cachedLock.ExpiresAt;
                    }
                    return result;
                }

                // 🔒 Check database
                var dbLock = await GetDatabaseLockByKeyAsync(lockKey);
                if (dbLock != null)
                {
                    if (IsLockExpired(dbLock))
                    {
                        await ReleaseDatabaseLockAsync(dbLock);
                        result.Status = LockStatus.NotLocked;
                    }
                    else
                    {
                        result.Status = LockStatus.Locked;
                        result.LockedBy = dbLock.LockedBy;
                        result.LockedAt = dbLock.CreatedAt;
                        result.ExpiresAt = dbLock.ExpiresAt;
                        
                        // Update cache
                        await CacheLockAsync(dbLock);
                    }
                }
                else
                {
                    result.Status = LockStatus.NotLocked;
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Status = LockStatus.Error;
                result.ErrorMessage = $"Failed to check lock status: {ex.Message}";
                _logger.LogError(ex, "Failed to check lock status for {LockKey}", lockKey);
                return result;
            }
        }

        /// <summary>
        /// 🔒 Clean up expired locks
        /// </summary>
        public async Task<LockCleanupResult> CleanupExpiredLocksAsync()
        {
            var result = new LockCleanupResult
            {
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // 🔒 Step 1: Get expired locks from database
                var expiredLocks = await _context.ConcurrencyLocks
                    .Where(l => l.ExpiresAt < DateTime.UtcNow)
                    .ToListAsync();

                // 🔒 Step 2: Remove expired locks
                _context.ConcurrencyLocks.RemoveRange(expiredLocks);
                result.DatabaseCleanupCount = await _context.SaveChangesAsync();

                // 🔒 Step 3: Clean up cache entries
                var cacheKeys = expiredLocks.Select(l => $"{LockKeyPrefix}{l.LockKey}").ToList();
                foreach (var key in cacheKeys)
                {
                    _cache.Remove(key);
                }
                result.CacheCleanupCount = cacheKeys.Count;

                result.Success = true;
                result.CompletedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Lock cleanup failed: {ex.Message}";
                _logger.LogError(ex, "Lock cleanup failed");
            }

            return result;
        }

        /// <summary>
        /// 🔒 Get lock statistics
        /// </summary>
        public async Task<LockStatistics> GetLockStatisticsAsync()
        {
            var stats = new LockStatistics
            {
                GeneratedAt = DateTime.UtcNow
            };

            try
            {
                // 🔒 Get active locks by type
                var activeLocks = await _context.ConcurrencyLocks
                    .Where(l => l.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();

                stats.ActiveLocksCount = activeLocks.Count;
                stats.LocksByType = activeLocks
                    .GroupBy(l => l.ResourceType)
                    .ToDictionary(g => g.Key, g => g.Count());

                stats.LocksByUser = activeLocks
                    .GroupBy(l => l.LockedBy)
                    .ToDictionary(g => g.Key, g => g.Count());

                // 🔒 Get average lock duration
                var recentLocks = await _context.ConcurrencyLocks
                    .Where(l => l.CreatedAt >= DateTime.UtcNow.AddHours(-1))
                    .ToListAsync();

                if (recentLocks.Any())
                {
                    stats.AverageLockDurationMinutes = recentLocks
                        .Where(l => l.ExpiresAt.HasValue)
                        .Average(l => (l.ExpiresAt!.Value - l.CreatedAt).TotalMinutes);
                }

                // 🔒 Get expired locks count
                stats.ExpiredLocksCount = await _context.ConcurrencyLocks
                    .Where(l => l.ExpiresAt < DateTime.UtcNow)
                    .CountAsync();

                // 🔒 Get most locked resources
                stats.MostLockedResources = await _context.ConcurrencyLocks
                    .Where(l => l.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                    .GroupBy(l => new { l.ResourceType, l.ResourceId })
                    .Select(g => new LockResourceInfo
                    {
                        ResourceType = g.Key.ResourceType,
                        ResourceId = g.Key.ResourceId,
                        LockCount = g.Count()
                    })
                    .OrderByDescending(r => r.LockCount)
                    .Take(10)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get lock statistics");
            }

            return stats;
        }

        #region Private Methods

        private LockValidationResult ValidateLockRequest(LockRequest request)
        {
            var result = new LockValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(request.LockKey))
            {
                result.IsValid = false;
                result.ErrorMessage = "Lock key cannot be empty";
                return result;
            }

            if (string.IsNullOrWhiteSpace(request.ResourceType))
            {
                result.IsValid = false;
                result.ErrorMessage = "Resource type cannot be empty";
                return result;
            }

            if (string.IsNullOrWhiteSpace(request.RequestedBy))
            {
                result.IsValid = false;
                result.ErrorMessage = "RequestedBy cannot be empty";
                return result;
            }

            return result;
        }

        private async Task<ConcurrencyLock> CheckExistingLockAsync(LockRequest request)
        {
            var cacheKey = $"{LockKeyPrefix}{request.LockKey}";
            
            if (_cache.TryGetValue(cacheKey, out ConcurrencyLock cachedLock))
            {
                return cachedLock;
            }

            return null;
        }

        private async Task CacheLockAsync(ConcurrencyLock lockEntity)
        {
            var cacheKey = $"{LockKeyPrefix}{lockEntity.LockKey}";
            var cacheDuration = lockEntity.ExpiresAt.Value - DateTime.UtcNow;
            
            if (cacheDuration > TimeSpan.Zero)
            {
                _cache.Set(cacheKey, lockEntity, cacheDuration);
            }
        }

        private async Task<ConcurrencyLock> GetCachedLockAsync(string lockKey)
        {
            var cacheKey = $"{LockKeyPrefix}{lockKey}";
            
            if (_cache.TryGetValue(cacheKey, out ConcurrencyLock cachedLock))
            {
                return cachedLock;
            }

            return null;
        }

        private async Task RemoveCachedLockAsync(string lockKey)
        {
            var cacheKey = $"{LockKeyPrefix}{lockKey}";
            _cache.Remove(cacheKey);
        }

        private async Task<ConcurrencyLock> GetDatabaseLockAsync(LockRequest request)
        {
            return await _context.ConcurrencyLocks
                .FirstOrDefaultAsync(l => l.LockKey == request.LockKey);
        }

        private async Task<ConcurrencyLock> GetDatabaseLockByKeyAsync(string lockKey)
        {
            return await _context.ConcurrencyLocks
                .FirstOrDefaultAsync(l => l.LockKey == lockKey);
        }

        private async Task<ConcurrencyLock> CreateLockAsync(LockRequest request)
        {
            var timeout = GetLockTimeout(request.ResourceType);
            
            var lockEntity = new ConcurrencyLock
            {
                LockKey = request.LockKey,
                ResourceType = request.ResourceType,
                ResourceId = request.ResourceId,
                LockedBy = request.RequestedBy,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(timeout),
                ExtensionCount = 0
            };

            _context.ConcurrencyLocks.Add(lockEntity);
            await _context.SaveChangesAsync();

            return lockEntity;
        }

        private async Task ReleaseExpiredLockAsync(ConcurrencyLock lockEntity)
        {
            await ReleaseDatabaseLockAsync(lockEntity);
            await RemoveCachedLockAsync(lockEntity.LockKey);
        }

        private async Task ReleaseDatabaseLockAsync(ConcurrencyLock lockEntity)
        {
            _context.ConcurrencyLocks.Remove(lockEntity);
            await _context.SaveChangesAsync();
        }

        private bool IsLockExpired(ConcurrencyLock lockEntity)
        {
            return !lockEntity.ExpiresAt.HasValue || lockEntity.ExpiresAt.Value < DateTime.UtcNow;
        }

        private TimeSpan GetLockTimeout(string resourceType)
        {
            return resourceType switch
            {
                "FINANCE_PERIOD_CLOSE" => TimeSpan.FromSeconds(CriticalLockTimeoutSeconds),
                "FINANCE_BATCH_PROCESS" => TimeSpan.FromSeconds(CriticalLockTimeoutSeconds),
                "FINANCE_RECONCILIATION" => TimeSpan.FromSeconds(CriticalLockTimeoutSeconds),
                "FINANCE_JOURNAL_BATCH" => TimeSpan.FromSeconds(120), // 2 minutes
                "FINANCE_ACCOUNT" => TimeSpan.FromSeconds(60), // 1 minute
                _ => TimeSpan.FromSeconds(DefaultLockTimeoutSeconds)
            };
        }

        #endregion
    }

    #region Supporting Classes

    public class LockRequest
    {
        public string LockKey { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public TimeSpan? Timeout { get; set; }
    }

    public class LockResult
    {
        public string LockKey { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public LockStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public int? LockId { get; set; }
        public string? LockedBy { get; set; }
        public DateTime? LockedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime RequestedAt { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
    }

    public class ReleaseResult
    {
        public string LockKey { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int? LockId { get; set; }
        public string ReleasedBy { get; set; } = string.Empty;
        public DateTime ReleasedAt { get; set; }
    }

    public class ExtensionResult
    {
        public string LockKey { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int? LockId { get; set; }
        public DateTime? NewExpiresAt { get; set; }
        public int ExtensionCount { get; set; }
        public string ExtendedBy { get; set; } = string.Empty;
        public DateTime ExtendedAt { get; set; }
    }

    public class LockStatusResult
    {
        public string LockKey { get; set; } = string.Empty;
        public LockStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public string? LockedBy { get; set; }
        public DateTime? LockedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime CheckedAt { get; set; }
    }

    public class LockCleanupResult
    {
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int DatabaseCleanupCount { get; set; }
        public int CacheCleanupCount { get; set; }
    }

    public class LockStatistics
    {
        public DateTime GeneratedAt { get; set; }
        public int ActiveLocksCount { get; set; }
        public int ExpiredLocksCount { get; set; }
        public Dictionary<string, int> LocksByType { get; set; } = new();
        public Dictionary<string, int> LocksByUser { get; set; } = new();
        public double AverageLockDurationMinutes { get; set; }
        public List<LockResourceInfo> MostLockedResources { get; set; } = new();
    }

    public class LockResourceInfo
    {
        public string ResourceType { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public int LockCount { get; set; }
    }

    public class LockValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    #endregion

    #region Enums

    public enum LockStatus
    {
        NotLocked,
        Locked,
        AlreadyLocked,
        Acquired,
        InvalidRequest,
        Error
    }

    #endregion

    #region Database Entity

    public class ConcurrencyLock
    {
        public int Id { get; set; }
        public string LockKey { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public string LockedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? ExtendedAt { get; set; }
        public int ExtensionCount { get; set; }
        public int CompanyId { get; set; }
    }

    #endregion
}
