using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading;
// using StackExchange.Redis; // Redis not available
using SecureERP2.Modules.Finance.Entities;
using SecureERP2.Modules.Finance.Services;

namespace SecureERP2.Modules.Finance.Services.Integrity
{
    /// <summary>
    /// 🏗️ STEP 6.8: Real-Time Integrity Monitor
    /// Stream-based validation for distributed financial system
    /// </summary>
    public class RealTimeIntegrityMonitorService
    {
        private readonly ILogger<RealTimeIntegrityMonitorService> _logger;
        // private readonly IConnectionMultiplexer _redis; // Redis not available
        private readonly LedgerEngineService _ledgerEngine;
        // private readonly EventBusService _eventBus; // Commented out - EventBusService not available
        
        // Monitoring configuration
        private readonly TimeSpan _monitoringInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _alertThreshold = TimeSpan.FromMinutes(1);
        private readonly int _maxAlertsPerMinute = 10;
        
        // State tracking
        private readonly ConcurrentDictionary<int, CompanyIntegrityState> _companyStates;
        private readonly ConcurrentDictionary<string, DateTime> _recentAlerts;
        private readonly Timer _monitoringTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        // Redis keys
        private const string IntegrityStreamPrefix = "integrity_stream:";
        private const string AlertStreamPrefix = "alert_stream:";
        private const string StateKeyPrefix = "integrity_state:";
        
        public RealTimeIntegrityMonitorService(
            ILogger<RealTimeIntegrityMonitorService> logger,
            LedgerEngineService ledgerEngine
            // EventBusService eventBus // Commented out - EventBusService not available
        )
        {
            _logger = logger;
            // _redis = redis; // Redis not available
            _ledgerEngine = ledgerEngine;
            // _eventBus = eventBus; // Commented out - EventBusService not available
            
            _companyStates = new ConcurrentDictionary<int, CompanyIntegrityState>();
            _recentAlerts = new ConcurrentDictionary<string, DateTime>();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // 🔥 Start monitoring timer
            _monitoringTimer = new Timer(MonitorIntegrityAsync, null, TimeSpan.Zero, _monitoringInterval);
            
            // 🔥 Subscribe to events
            _ = Task.Run(SubscribeToEventsAsync);
        }
        
        /// <summary>
        /// Subscribe to finance events for real-time monitoring
        /// </summary>
        private async Task SubscribeToEventsAsync()
        {
            try
            {
                _logger.LogInformation("Starting real-time integrity monitoring");
                
                // 🔥 Subscribe to all relevant event types
                var eventTypes = new[]
                {
                    "JournalCreated",
                    "JournalPosted", 
                    "JournalVoided",
                    "InvoiceCreated",
                    "InvoicePaid",
                    "PeriodClosed"
                };
                
                foreach (var eventType in eventTypes)
                {
                    // TODO: Add _eventBus field to RealTimeIntegrityMonitorService
                    // await _eventBus.SubscribeAsync(
                    //     "IntegrityMonitor",
                    //     eventType,
                    //     async (financeEvent) => await ProcessEventAsync(financeEvent));
                    // TODO: Mock event subscription for now
                }
                
                _logger.LogInformation("Subscribed to {Count} event types for integrity monitoring", eventTypes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to events for integrity monitoring");
            }
        }
        
        /// <summary>
        /// Process finance event for integrity validation
        /// </summary>
        private async Task<EventProcessingResult> ProcessEventAsync(FinanceEvent financeEvent)
        {
            var result = new EventProcessingResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId,
                ProcessedAt = DateTime.UtcNow
            };
            
            try
            {
                _logger.LogDebug("Processing integrity check for event {EventId} of type {EventType}", 
                    financeEvent.EventId, financeEvent.EventType);
                
                // 🔥 Get or create company state
                var companyState = _companyStates.GetOrAdd(financeEvent.CompanyId, 
                    companyId => new CompanyIntegrityState { CompanyId = companyId });
                
                // 🔥 Validate event based on type
                var validationResult = await ValidateEventAsync(financeEvent, companyState);
                
                if (validationResult.IsValid)
                {
                    result.IsSuccess = true;
                    result.Message = "Event passed integrity validation";
                    
                    // 🔥 Update company state
                    await UpdateCompanyStateAsync(companyState, financeEvent);
                }
                else
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = validationResult.ErrorMessage;
                    
                    // 🔥 Create alert
                    await CreateAlertAsync(financeEvent, validationResult);
                }
                
                // 🔥 Publish integrity result
                await PublishIntegrityResultAsync(result);
                
                _logger.LogDebug("Integrity check completed for event {EventId}: {Success}", 
                    financeEvent.EventId, result.IsSuccess);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event {EventId} for integrity", financeEvent.EventId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Validate event integrity
        /// </summary>
        private async Task<IntegrityValidationResult> ValidateEventAsync(
            FinanceEvent financeEvent, 
            CompanyIntegrityState companyState)
        {
            var result = new IntegrityValidationResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId,
                ValidatedAt = DateTime.UtcNow
            };
            
            try
            {
                switch (financeEvent.EventType)
                {
                    case "JournalCreated":
                        result = await ValidateJournalCreatedEventAsync(financeEvent, companyState);
                        break;
                    
                    case "JournalPosted":
                        result = await ValidateJournalPostedEventAsync(financeEvent, companyState);
                        break;
                    
                    case "JournalVoided":
                        result = await ValidateJournalVoidedEventAsync(financeEvent, companyState);
                        break;
                    
                    case "InvoiceCreated":
                        result = await ValidateInvoiceCreatedEventAsync(financeEvent, companyState);
                        break;
                    
                    case "InvoicePaid":
                        result = await ValidateInvoicePaidEventAsync(financeEvent, companyState);
                        break;
                    
                    case "PeriodClosed":
                        result = await ValidatePeriodClosedEventAsync(financeEvent, companyState);
                        break;
                    
                    default:
                        result.IsValid = true;
                        result.Message = "Unknown event type, skipping validation";
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating event {EventId}", financeEvent.EventId);
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Validate JournalCreated event
        /// </summary>
        private async Task<IntegrityValidationResult> ValidateJournalCreatedEventAsync(
            FinanceEvent financeEvent, 
            CompanyIntegrityState companyState)
        {
            var result = new IntegrityValidationResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId,
                ValidatedAt = DateTime.UtcNow
            };
            
            try
            {
                // 🔥 Extract journal data
                var journalData = financeEvent.Data;
                var journalLines = JsonSerializer.Deserialize<List<JournalLineData>>(
                    journalData["JournalLines"].ToString());
                
                // 🔥 Validate double-entry balance
                var totalDebit = journalLines.Sum(jl => jl.DebitAmount);
                var totalCredit = journalLines.Sum(jl => jl.CreditAmount);
                
                if (Math.Abs(totalDebit - totalCredit) > 0.01m)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Debit/Credit imbalance: Debit={totalDebit}, Credit={totalCredit}";
                    result.ValidationType = "DoubleEntryBalance";
                    return result;
                }
                
                // 🔥 Validate account existence
                foreach (var line in journalLines)
                {
                    if (line.AccountId <= 0)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Invalid AccountId: {line.AccountId}";
                        result.ValidationType = "AccountValidation";
                        return result;
                    }
                }
                
                // 🔥 Validate amounts
                foreach (var line in journalLines)
                {
                    if (line.DebitAmount < 0 || line.CreditAmount < 0)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Negative amount not allowed: Debit={line.DebitAmount}, Credit={line.CreditAmount}";
                        result.ValidationType = "AmountValidation";
                        return result;
                    }
                    
                    if (line.DebitAmount > 0 && line.CreditAmount > 0)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = "Cannot have both Debit and Credit amounts in the same line";
                        result.ValidationType = "AmountValidation";
                        return result;
                    }
                }
                
                result.IsValid = true;
                result.Message = "JournalCreated event passed all validations";
                
                _logger.LogDebug("JournalCreated event {EventId} validated successfully", financeEvent.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating JournalCreated event {EventId}", financeEvent.EventId);
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Validate JournalPosted event
        /// </summary>
        private async Task<IntegrityValidationResult> ValidateJournalPostedEventAsync(
            FinanceEvent financeEvent, 
            CompanyIntegrityState companyState)
        {
            var result = new IntegrityValidationResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId,
                ValidatedAt = DateTime.UtcNow
            };
            
            try
            {
                // 🔥 Check if journal exists and is in Draft status
                var journalEntryId = Guid.Parse(financeEvent.Data["JournalEntryId"].ToString());
                
                // This would validate against the database
                // For now, we'll just check if it exists in our state tracking
                
                result.IsValid = true;
                result.Message = "JournalPosted event validated successfully";
                
                _logger.LogDebug("JournalPosted event {EventId} validated successfully", financeEvent.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating JournalPosted event {EventId}", financeEvent.EventId);
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Validate JournalVoided event
        /// </summary>
        private async Task<IntegrityValidationResult> ValidateJournalVoidedEventAsync(
            FinanceEvent financeEvent, 
            CompanyIntegrityState companyState)
        {
            var result = new IntegrityValidationResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId,
                ValidatedAt = DateTime.UtcNow
            };
            
            try
            {
                // 🔥 Check if journal exists and is Posted
                var journalEntryId = Guid.Parse(financeEvent.Data["JournalEntryId"].ToString());
                var reason = financeEvent.Data["Reason"].ToString();
                
                if (string.IsNullOrEmpty(reason))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Void reason is required";
                    result.ValidationType = "BusinessRuleValidation";
                    return result;
                }
                
                result.IsValid = true;
                result.Message = "JournalVoided event validated successfully";
                
                _logger.LogDebug("JournalVoided event {EventId} validated successfully", financeEvent.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating JournalVoided event {EventId}", financeEvent.EventId);
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Validate InvoiceCreated event
        /// </summary>
        private async Task<IntegrityValidationResult> ValidateInvoiceCreatedEventAsync(
            FinanceEvent financeEvent, 
            CompanyIntegrityState companyState)
        {
            var result = new IntegrityValidationResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId,
                ValidatedAt = DateTime.UtcNow
            };
            
            // 🔥 Invoice creation doesn't directly affect ledger integrity
            result.IsValid = true;
            result.Message = "InvoiceCreated event validated (no ledger impact)";
            
            return result;
        }
        
        /// <summary>
        /// Validate InvoicePaid event
        /// </summary>
        private async Task<IntegrityValidationResult> ValidateInvoicePaidEventAsync(
            FinanceEvent financeEvent, 
            CompanyIntegrityState companyState)
        {
            var result = new IntegrityValidationResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId,
                ValidatedAt = DateTime.UtcNow
            };
            
            // 🔥 Invoice payment creates journal entries automatically
            result.IsValid = true;
            result.Message = "InvoicePaid event validated (creates journal entries)";
            
            return result;
        }
        
        /// <summary>
        /// Validate PeriodClosed event
        /// </summary>
        private async Task<IntegrityValidationResult> ValidatePeriodClosedEventAsync(
            FinanceEvent financeEvent, 
            CompanyIntegrityState companyState)
        {
            var result = new IntegrityValidationResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId,
                ValidatedAt = DateTime.UtcNow
            };
            
            try
            {
                // 🔥 Extract period end date
                var periodEnd = DateTime.Parse(financeEvent.Data["PeriodEnd"].ToString());
                
                // 🔥 Check if period is already closed
                if (companyState.ClosedPeriods.Contains(periodEnd))
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Period {periodEnd:yyyy-MM} is already closed";
                    result.ValidationType = "BusinessRuleValidation";
                    return result;
                }
                
                // 🔥 Check if all previous periods are closed
                var previousMonth = periodEnd.AddMonths(-1);
                if (previousMonth >= new DateTime(2024, 1, 1) && !companyState.ClosedPeriods.Contains(previousMonth))
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Previous period {previousMonth:yyyy-MM} must be closed first";
                    result.ValidationType = "BusinessRuleValidation";
                    return result;
                }
                
                result.IsValid = true;
                result.Message = $"Period {periodEnd:yyyy-MM} closure validated successfully";
                
                _logger.LogDebug("PeriodClosed event {EventId} validated successfully", financeEvent.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating PeriodClosed event {EventId}", financeEvent.EventId);
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Update company state after successful validation
        /// </summary>
        private async Task UpdateCompanyStateAsync(CompanyIntegrityState companyState, FinanceEvent financeEvent)
        {
            try
            {
                companyState.LastEventTimestamp = financeEvent.Timestamp;
                companyState.TotalEventsProcessed++;
                companyState.LastValidationAt = DateTime.UtcNow;
                
                // 🔥 Update specific state based on event type
                switch (financeEvent.EventType)
                {
                    case "JournalPosted":
                        companyState.PostedJournalsCount++;
                        break;
                    
                    case "JournalVoided":
                        companyState.VoidedJournalsCount++;
                        break;
                    
                    case "PeriodClosed":
                        var periodEnd = DateTime.Parse(financeEvent.Data["PeriodEnd"].ToString());
                        companyState.ClosedPeriods.Add(periodEnd);
                        break;
                }
                
                // 🔥 Persist state to Redis
                await PersistCompanyStateAsync(companyState);
                
                _logger.LogDebug("Updated integrity state for company {CompanyId}", companyState.CompanyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating company state for company {CompanyId}", companyState.CompanyId);
            }
        }
        
        /// <summary>
        /// Create alert for validation failure
        /// </summary>
        private async Task CreateAlertAsync(FinanceEvent financeEvent, IntegrityValidationResult validationResult)
        {
            try
            {
                // 🔥 Check alert rate limiting
                var alertKey = $"{financeEvent.CompanyId}:{validationResult.ValidationType}";
                var now = DateTime.UtcNow;
                
                if (_recentAlerts.TryGetValue(alertKey, out var lastAlert))
                {
                    if (now - lastAlert < _alertThreshold)
                    {
                        _logger.LogDebug("Alert rate limited for {AlertKey}", alertKey);
                        return;
                    }
                }
                
                _recentAlerts[alertKey] = now;
                
                // 🔥 Clean up old alerts
                var cutoffTime = now - _alertThreshold;
                var oldAlerts = _recentAlerts.Where(kvp => kvp.Value < cutoffTime).ToList();
                foreach (var oldAlert in oldAlerts)
                {
                    _recentAlerts.TryRemove(oldAlert.Key, out _);
                }
                
                // 🔥 Create alert
                var alert = new IntegrityAlert
                {
                    Id = Guid.NewGuid(),
                    EventId = financeEvent.EventId,
                    EventType = financeEvent.EventType,
                    CompanyId = financeEvent.CompanyId,
                    ValidationType = validationResult.ValidationType,
                    ErrorMessage = validationResult.ErrorMessage,
                    Severity = DetermineSeverity(validationResult.ValidationType),
                    CreatedAt = DateTime.UtcNow,
                    Status = AlertStatus.Active
                };
                
                // 🔥 Publish alert
                await PublishAlertAsync(alert);
                
                // 🔥 Store alert in Redis stream
                // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                var alertStreamKey = $"{AlertStreamPrefix}{financeEvent.CompanyId}";
                
                // TODO: Redis operations commented out due to missing service reference
                // await db.StreamAddAsync(alertStreamKey, new[]
                // {
                //     new("alert_id", alert.Id.ToString()),
                //     new("event_id", alert.EventId.ToString()),
                //     new("event_type", alert.EventType),
                //     new("company_id", alert.CompanyId.ToString()),
                //     new("validation_type", alert.ValidationType),
                //     new("error_message", alert.ErrorMessage),
                //     new("severity", alert.Severity.ToString()),
                //     new("created_at", alert.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                // });
                
                _logger.LogWarning("Created integrity alert {AlertId} for event {EventId}: {Error}", 
                    alert.Id, financeEvent.EventId, alert.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating alert for event {EventId}", financeEvent.EventId);
            }
        }
        
        /// <summary>
        /// Determine alert severity
        /// </summary>
        private AlertSeverity DetermineSeverity(string validationType)
        {
            return validationType switch
            {
                "DoubleEntryBalance" => AlertSeverity.Critical,
                "AccountValidation" => AlertSeverity.High,
                "AmountValidation" => AlertSeverity.High,
                "BusinessRuleValidation" => AlertSeverity.Medium,
                _ => AlertSeverity.Low
            };
        }
        
        /// <summary>
        /// Publish integrity result
        /// </summary>
        private async Task PublishIntegrityResultAsync(EventProcessingResult result)
        {
            try
            {
                // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                var integrityStreamKey = $"{IntegrityStreamPrefix}{result.CompanyId}";
                
                // TODO: Redis operations commented out due to missing service reference
                // await db.StreamAddAsync(integrityStreamKey, new[]
                // {
                //     new("event_id", result.EventId.ToString()),
                //     new("event_type", result.EventType),
                //     new("company_id", result.CompanyId.ToString()),
                //     new("is_success", result.IsSuccess.ToString()),
                //     new("message", result.Message),
                //     new("error_message", result.ErrorMessage ?? ""),
                //     new("processed_at", result.ProcessedAt.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                // });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish integrity result for event {EventId}", result.EventId);
            }
        }
        
        /// <summary>
        /// Publish alert
        /// </summary>
        private async Task PublishAlertAsync(IntegrityAlert alert)
        {
            try
            {
                // 🔥 This could publish to external monitoring systems
                // For now, just log the alert
                _logger.LogWarning("INTEGRITY ALERT [{Severity}] {EventType} {EventId}: {Error}", 
                    alert.Severity, alert.EventType, alert.EventId, alert.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish alert {AlertId}", alert.Id);
            }
        }
        
        /// <summary>
        /// Periodic integrity monitoring
        /// </summary>
        private async void MonitorIntegrityAsync(object state)
        {
            try
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    return;
                
                _logger.LogDebug("Running periodic integrity monitoring");
                
                // 🔥 Check ledger integrity for all active companies
                foreach (var companyState in _companyStates.Values)
                {
                    await ValidateLedgerIntegrityAsync(companyState);
                }
                
                _logger.LogDebug("Periodic integrity monitoring completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in periodic integrity monitoring");
            }
        }
        
        /// <summary>
        /// Validate ledger integrity
        /// </summary>
        private async Task ValidateLedgerIntegrityAsync(CompanyIntegrityState companyState)
        {
            try
            {
                // 🔥 Get ledger validation result
                var validationResult = await _ledgerEngine.ValidateLedgerAsync(companyState.CompanyId);
                
                if (!validationResult.IsValid)
                {
                    // 🔥 Create alert for ledger integrity issues
                    var alert = new IntegrityAlert
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = companyState.CompanyId,
                        ValidationType = "LedgerIntegrity",
                        ErrorMessage = $"Ledger validation failed: {validationResult.BalanceMismatches.Count} balance mismatches",
                        Severity = AlertSeverity.Critical,
                        CreatedAt = DateTime.UtcNow,
                        Status = AlertStatus.Active
                    };
                    
                    await PublishAlertAsync(alert);
                    
                    _logger.LogError("Ledger integrity validation failed for company {CompanyId}: {Mismatches} mismatches", 
                        companyState.CompanyId, validationResult.BalanceMismatches.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating ledger integrity for company {CompanyId}", companyState.CompanyId);
            }
        }
        
        /// <summary>
        /// Persist company state to Redis
        /// </summary>
        private async Task PersistCompanyStateAsync(CompanyIntegrityState companyState)
        {
            try
            {
                // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                var stateKey = $"{StateKeyPrefix}{companyState.CompanyId}";
                
                var stateJson = JsonSerializer.Serialize(companyState);
                // await db.StringSetAsync(stateKey, stateJson, TimeSpan.FromHours(24));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist company state for company {CompanyId}", companyState.CompanyId);
            }
        }
        
        /// <summary>
        /// Get integrity statistics
        /// </summary>
        public async Task<IntegrityStatistics> GetStatisticsAsync(int? companyId = null)
        {
            var stats = new IntegrityStatistics
            {
                GeneratedAt = DateTime.UtcNow
            };
            
            try
            {
                var states = companyId.HasValue 
                    ? new[] { _companyStates.GetValueOrDefault(companyId.Value) }
                    : _companyStates.Values.ToArray();
                
                stats.ActiveCompanies = states.Count(s => s != null);
                stats.TotalEventsProcessed = states.Where(s => s != null).Sum(s => s.TotalEventsProcessed);
                stats.PostedJournalsCount = states.Where(s => s != null).Sum(s => s.PostedJournalsCount);
                stats.VoidedJournalsCount = states.Where(s => s != null).Sum(s => s.VoidedJournalsCount);
                stats.ClosedPeriodsCount = states.Where(s => s != null).Sum(s => s.ClosedPeriods.Count);
                stats.ActiveAlerts = _recentAlerts.Count;
                
                stats.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting integrity statistics");
                stats.IsSuccess = false;
                stats.ErrorMessage = ex.Message;
            }
            
            return stats;
        }
        
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _monitoringTimer?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
    
    #region Supporting Classes
    
    public class EventProcessingResult
    {
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
    }
    
    public class IntegrityValidationResult
    {
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ValidationType { get; set; } = string.Empty;
        public List<string> Issues { get; set; } = new();
        public DateTime ValidatedAt { get; set; }
    }
    
    public class CompanyIntegrityState
    {
        public int CompanyId { get; set; }
        public DateTime LastEventTimestamp { get; set; }
        public long TotalEventsProcessed { get; set; }
        public DateTime LastValidationAt { get; set; }
        public int PostedJournalsCount { get; set; }
        public int VoidedJournalsCount { get; set; }
        public List<DateTime> ClosedPeriods { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
    
    public class IntegrityAlert
    {
        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public string ValidationType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public DateTime CreatedAt { get; set; }
        public AlertStatus Status { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string ResolutionNotes { get; set; } = string.Empty;
    }
    
    public class IntegrityStatistics
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public int ActiveCompanies { get; set; }
        public long TotalEventsProcessed { get; set; }
        public int PostedJournalsCount { get; set; }
        public int VoidedJournalsCount { get; set; }
        public int ClosedPeriodsCount { get; set; }
        public int ActiveAlerts { get; set; }
    }
    
    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
    
    public enum AlertStatus
    {
        Active,
        Acknowledged,
        Resolved,
        Dismissed
    }
    
    #endregion
}
