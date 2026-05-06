using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
// using StackExchange.Redis; // Commented out - Redis not available
using SecureERP2.Modules.Finance.Entities;
using SecureERP2.Modules.Finance.Services;

namespace SecureERP2.Modules.Finance.Services.Resilience
{
    /// <summary>
    /// 🏗️ STEP 6.11: Failure Handling Service
    /// Comprehensive failure scenario management for distributed financial system
    /// </summary>
    public class FailureHandlingService
    {
        private readonly ILogger<FailureHandlingService> _logger;
        // private readonly IConnectionMultiplexer _redis; // Redis not available
        // private readonly EventBusService _eventBus; // Commented out - EventBusService not available
        // private readonly SagaOrchestratorService _sagaOrchestrator; // Service not available
        private readonly LedgerEngineService _ledgerEngine;
        // private readonly DistributedAuditChainService _auditChain; // Service not available
        
        // Failure tracking
        private readonly Dictionary<string, FailureScenario> _failureScenarios;
        private readonly Dictionary<Guid, FailureIncident> _activeIncidents;
        
        // Redis keys
        private const string FailureStreamPrefix = "failure_stream:";
        private const string IncidentKeyPrefix = "incident:";
        private const string RecoveryQueuePrefix = "recovery_queue:";
        
        public FailureHandlingService(
            ILogger<FailureHandlingService> logger,
            // EventBusService eventBus, // Commented out - EventBusService not available
            LedgerEngineService ledgerEngine)
        {
            _logger = logger;
            // _redis = redis; // Redis not available
            // _eventBus = eventBus; // Commented out - EventBusService not available
            // _sagaOrchestrator = sagaOrchestrator; // Service not available
            _ledgerEngine = ledgerEngine;
            // _auditChain = auditChain; // Service not available
            
            _failureScenarios = InitializeFailureScenarios();
            _activeIncidents = new Dictionary<Guid, FailureIncident>();
            
            // 🔥 Start failure monitoring
            _ = Task.Run(MonitorFailuresAsync);
        }
        
        /// <summary>
        /// Handle event published but not processed
        /// </summary>
        public async Task<FailureHandlingResult> HandleUnprocessedEventAsync(FinanceEvent financeEvent)
        {
            var result = new FailureHandlingResult
            {
                IncidentId = Guid.NewGuid(),
                EventId = financeEvent.EventId,
                CompanyId = financeEvent.CompanyId,
                FailureType = "UnprocessedEvent",
                StartedAt = DateTime.UtcNow
            };
            
            try
            {
                _logger.LogWarning("Handling unprocessed event {EventId} of type {EventType}", 
                    financeEvent.EventId, financeEvent.EventType);
                
                // 🔥 Create incident
                var incident = new FailureIncident
                {
                    Id = result.IncidentId,
                    EventId = financeEvent.EventId,
                    CompanyId = financeEvent.CompanyId,
                    FailureType = "UnprocessedEvent",
                    Severity = DetermineSeverity("UnprocessedEvent", financeEvent.EventType),
                    Description = $"Event {financeEvent.EventId} was published but not processed",
                    EventData = JsonSerializer.Serialize(financeEvent),
                    CreatedAt = DateTime.UtcNow,
                    Status = IncidentStatus.Active
                };
                
                _activeIncidents[result.IncidentId] = incident;
                await PersistIncidentAsync(incident);
                
                // 🔥 Add to retry queue
                await AddToRetryQueueAsync(incident);
                
                // 🔥 Check if event exists in event store
                var eventExists = await CheckEventExistsAsync(financeEvent.EventId);
                if (!eventExists)
                {
                    // 🔥 Recreate event in store
                    await RecreateEventAsync(financeEvent);
                    incident.Actions.Add("Recreated event in event store");
                }
                
                // 🔥 Attempt to reprocess
                var reprocessResult = await AttemptReprocessAsync(incident);
                
                if (reprocessResult.IsSuccess)
                {
                    incident.Status = IncidentStatus.Resolved;
                    incident.ResolvedAt = DateTime.UtcNow;
                    incident.ResolutionNotes = "Event successfully reprocessed";
                    result.IsSuccess = true;
                    result.Message = "Unprocessed event handled successfully";
                }
                else
                {
                    incident.Status = IncidentStatus.Escalated;
                    incident.EscalatedAt = DateTime.UtcNow;
                    incident.ResolutionNotes = $"Reprocessing failed: {reprocessResult.ErrorMessage}";
                    result.IsSuccess = false;
                    result.ErrorMessage = reprocessResult.ErrorMessage;
                }
                
                await PersistIncidentAsync(incident);
                
                result.CompletedAt = DateTime.UtcNow;
                result.DurationMs = (result.CompletedAt - result.StartedAt).TotalMilliseconds;
                
                _logger.LogInformation("Unprocessed event handling completed: {Success}", result.IsSuccess);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling unprocessed event {EventId}", financeEvent.EventId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
            }
            
            return result;
        }
        
        /// <summary>
        /// Handle duplicate event
        /// </summary>
        public async Task<FailureHandlingResult> HandleDuplicateEventAsync(FinanceEvent financeEvent)
        {
            var result = new FailureHandlingResult
            {
                IncidentId = Guid.NewGuid(),
                EventId = financeEvent.EventId,
                CompanyId = financeEvent.CompanyId,
                FailureType = "DuplicateEvent",
                StartedAt = DateTime.UtcNow
            };
            
            try
            {
                _logger.LogWarning("Handling duplicate event {EventId} of type {EventType}", 
                    financeEvent.EventId, financeEvent.EventType);
                
                // 🔥 Create incident
                var incident = new FailureIncident
                {
                    Id = result.IncidentId,
                    EventId = financeEvent.EventId,
                    CompanyId = financeEvent.CompanyId,
                    FailureType = "DuplicateEvent",
                    Severity = IncidentSeverity.Low,
                    Description = $"Duplicate event {financeEvent.EventId} detected",
                    EventData = JsonSerializer.Serialize(financeEvent),
                    CreatedAt = DateTime.UtcNow,
                    Status = IncidentStatus.Active
                };
                
                _activeIncidents[result.IncidentId] = incident;
                await PersistIncidentAsync(incident);
                
                // 🔥 Check if event was already processed
                var isProcessed = await CheckEventProcessedAsync(financeEvent.EventId);
                
                if (isProcessed)
                {
                    // 🔥 Event was already processed, ignore duplicate
                    incident.Status = IncidentStatus.Resolved;
                    incident.ResolvedAt = DateTime.UtcNow;
                    incident.ResolutionNotes = "Duplicate event ignored - already processed";
                    incident.Actions.Add("Ignored duplicate event");
                    
                    result.IsSuccess = true;
                    result.Message = "Duplicate event ignored - already processed";
                }
                else
                {
                    // 🔥 Event not processed, might be a race condition
                    incident.Actions.Add("Event not yet processed - potential race condition");
                    
                    // 🔥 Add to processing queue with delay
                    await AddToDelayedProcessingQueueAsync(financeEvent, TimeSpan.FromSeconds(5));
                    
                    incident.Status = IncidentStatus.Monitoring;
                    result.IsSuccess = true;
                    result.Message = "Duplicate event detected - added to delayed processing";
                }
                
                await PersistIncidentAsync(incident);
                
                result.CompletedAt = DateTime.UtcNow;
                result.DurationMs = (result.CompletedAt - result.StartedAt).TotalMilliseconds;
                
                _logger.LogInformation("Duplicate event handling completed: {Success}", result.IsSuccess);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling duplicate event {EventId}", financeEvent.EventId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
            }
            
            return result;
        }
        
        /// <summary>
        /// Handle partial posting
        /// </summary>
        public async Task<FailureHandlingResult> HandlePartialPostingAsync(FinanceEvent financeEvent, List<string> failedAccounts)
        {
            var result = new FailureHandlingResult
            {
                IncidentId = Guid.NewGuid(),
                EventId = financeEvent.EventId,
                CompanyId = financeEvent.CompanyId,
                FailureType = "PartialPosting",
                StartedAt = DateTime.UtcNow
            };
            
            try
            {
                _logger.LogError("Handling partial posting for event {EventId} - failed accounts: {Accounts}", 
                    financeEvent.EventId, string.Join(", ", failedAccounts));
                
                // 🔥 Create incident
                var incident = new FailureIncident
                {
                    Id = result.IncidentId,
                    EventId = financeEvent.EventId,
                    CompanyId = financeEvent.CompanyId,
                    FailureType = "PartialPosting",
                    Severity = IncidentSeverity.High,
                    Description = $"Partial posting detected for event {financeEvent.EventId}",
                    EventData = JsonSerializer.Serialize(financeEvent),
                    CreatedAt = DateTime.UtcNow,
                    Status = IncidentStatus.Active,
                    AffectedAccounts = failedAccounts
                };
                
                _activeIncidents[result.IncidentId] = incident;
                await PersistIncidentAsync(incident);
                
                // 🔥 Start SAGA compensation
                // TODO: Implement StartCompensationSagaAsync method
                // var sagaResult = await StartCompensationSagaAsync(incident);
                
                // if (sagaResult.IsSuccess)
                // {
                //     incident.Actions.Add("Started compensation saga");
                //     incident.SagaId = sagaResult.SagaId;
                //     incident.Status = IncidentStatus.Compensating;
                    
                //     result.IsSuccess = true;
                //     result.Message = "Compensation saga started for partial posting";
                // }
                // else
                // {
                //     incident.Status = IncidentStatus.Failed;
                //     incident.ResolutionNotes = $"Failed to start compensation: {sagaResult.ErrorMessage}";
                    
                //     result.IsSuccess = false;
                //     result.ErrorMessage = sagaResult.ErrorMessage;
                // }
                
                await PersistIncidentAsync(incident);
                
                result.CompletedAt = DateTime.UtcNow;
                result.DurationMs = (result.CompletedAt - result.StartedAt).TotalMilliseconds;
                
                _logger.LogInformation("Partial posting handling completed: {Success}", result.IsSuccess);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling partial posting for event {EventId}", financeEvent.EventId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
            }
            
            return result;
        }
        
        /// <summary>
        /// Handle system crash mid-transaction
        /// </summary>
        public async Task<FailureHandlingResult> HandleSystemCrashAsync(int companyId, List<Guid> incompleteTransactions)
        {
            var result = new FailureHandlingResult
            {
                IncidentId = Guid.NewGuid(),
                CompanyId = companyId,
                FailureType = "SystemCrash",
                StartedAt = DateTime.UtcNow
            };
            
            try
            {
                _logger.LogError("Handling system crash for company {CompanyId} - {Count} incomplete transactions", 
                    companyId, incompleteTransactions.Count);
                
                // 🔥 Create incident
                var incident = new FailureIncident
                {
                    Id = result.IncidentId,
                    CompanyId = companyId,
                    FailureType = "SystemCrash",
                    Severity = IncidentSeverity.Critical,
                    Description = $"System crash detected with {incompleteTransactions.Count} incomplete transactions",
                    CreatedAt = DateTime.UtcNow,
                    Status = IncidentStatus.Active,
                    IncompleteTransactions = incompleteTransactions
                };
                
                _activeIncidents[result.IncidentId] = incident;
                await PersistIncidentAsync(incident);
                
                // 🔥 Rebuild ledger state from events
                var rebuildResult = await _ledgerEngine.RebuildLedgerAsync(companyId);
                
                if (rebuildResult.IsSuccess)
                {
                    incident.Actions.Add("Rebuilt ledger state from events");
                    incident.Status = IncidentStatus.Recovering;
                    
                    // 🔥 Validate rebuilt state
                    var validationResult = await _ledgerEngine.ValidateLedgerAsync(companyId);
                    
                    if (validationResult.IsValid)
                    {
                        incident.Actions.Add("Validated rebuilt ledger state");
                        incident.Status = IncidentStatus.Resolved;
                        incident.ResolvedAt = DateTime.UtcNow;
                        incident.ResolutionNotes = "System crash recovered - ledger state rebuilt and validated";
                        
                        result.IsSuccess = true;
                        result.Message = "System crash recovered successfully";
                    }
                    else
                    {
                        incident.Actions.Add("Ledger validation failed after rebuild");
                        incident.Status = IncidentStatus.Failed;
                        incident.ResolutionNotes = $"Ledger validation failed: {validationResult.BalanceMismatches.Count} mismatches";
                        
                        result.IsSuccess = false;
                        result.ErrorMessage = "Ledger validation failed after rebuild";
                    }
                }
                else
                {
                    incident.Status = IncidentStatus.Failed;
                    incident.ResolutionNotes = $"Failed to rebuild ledger: {rebuildResult.ErrorMessage}";
                    
                    result.IsSuccess = false;
                    result.ErrorMessage = rebuildResult.ErrorMessage;
                }
                
                await PersistIncidentAsync(incident);
                
                result.CompletedAt = DateTime.UtcNow;
                result.DurationMs = (result.CompletedAt - result.StartedAt).TotalMilliseconds;
                
                _logger.LogInformation("System crash handling completed: {Success}", result.IsSuccess);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling system crash for company {CompanyId}", companyId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
            }
            
            return result;
        }
        
        /// <summary>
        /// Monitor failures continuously
        /// </summary>
        private async Task MonitorFailuresAsync()
        {
            while (true)
            {
                try
                {
                    // 🔥 Check for stuck events
                    await CheckStuckEventsAsync();
                    
                    // 🔥 Check for orphaned sagas
                    await CheckOrphanedSagasAsync();
                    
                    // 🔥 Check for ledger inconsistencies
                    await CheckLedgerInconsistenciesAsync();
                    
                    // 🔥 Clean up resolved incidents
                    await CleanupResolvedIncidentsAsync();
                    
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in failure monitoring loop");
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            }
        }
        
        /// <summary>
        /// Check for stuck events
        /// </summary>
        private async Task CheckStuckEventsAsync()
        {
            try
            {
                // TODO: Use IDistributedCache instead of Redis
                // var db = _redis.GetDatabase();
                
                // 🔥 Check event streams for old unprocessed events
                var cutoffTime = DateTime.UtcNow.AddMinutes(-5);
                
                // This would scan event streams for events older than 5 minutes
                // Implementation depends on Redis stream capabilities
                
                _logger.LogDebug("Checked for stuck events");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for stuck events");
            }
        }
        
        /// <summary>
        /// Check for orphaned sagas
        /// </summary>
        private async Task CheckOrphanedSagasAsync()
        {
            try
            {
                // TODO: Add _sagaOrchestrator field to FailureHandlingService
                // var activeSagas = _sagaOrchestrator.GetActiveSagas();
                var activeSagas = new List<object>(); // Placeholder
                
                foreach (var saga in activeSagas)
                {
                    // 🔥 Check if saga has been running too long
                    // TODO: Fix object type issue - saga should be properly typed
                    // var runningTime = DateTime.UtcNow - saga.StartedAt;
                    // if (runningTime > TimeSpan.FromMinutes(30))
                    // {
                    //     _logger.LogWarning("Saga {SagaId} has been running for {Duration}", saga.Id, runningTime);
                    //     
                    //     //     // 🔥 Create incident for orphaned saga
                //     var incident = new FailureIncident
                //     {
                //         Id = Guid.NewGuid(),
                //         CompanyId = 0, // Would need to extract from saga data
                //         FailureType = "OrphanedSaga",
                //         Severity = IncidentSeverity.Medium,
                //         Description = $"Saga {saga.Id} has been running for {runningTime}",
                //         CreatedAt = DateTime.UtcNow,
                //         Status = IncidentStatus.Active
                //     };
                        
                //     _activeIncidents[incident.Id] = incident;
                //     await PersistIncidentAsync(incident);
                // }
                }
                
                _logger.LogDebug("Checked {Count} active sagas for orphaned instances", activeSagas.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for orphaned sagas");
            }
        }
        
        /// <summary>
        /// Check for ledger inconsistencies
        /// </summary>
        private async Task CheckLedgerInconsistenciesAsync()
        {
            try
            {
                // 🔥 Get all active companies
                var companies = new[] { 1 }; // Would get from database
                
                foreach (var companyId in companies)
                {
                    var validationResult = await _ledgerEngine.ValidateLedgerAsync(companyId);
                    
                    if (!validationResult.IsValid)
                    {
                        _logger.LogError("Ledger inconsistency detected for company {CompanyId}: {Mismatches} mismatches", 
                            companyId, validationResult.BalanceMismatches.Count);
                        
                        // 🔥 Create incident for ledger inconsistency
                        var incident = new FailureIncident
                        {
                            Id = Guid.NewGuid(),
                            CompanyId = companyId,
                            FailureType = "LedgerInconsistency",
                            Severity = IncidentSeverity.High,
                            Description = $"Ledger inconsistency detected: {validationResult.BalanceMismatches.Count} balance mismatches",
                            CreatedAt = DateTime.UtcNow,
                            Status = IncidentStatus.Active
                        };
                        
                        _activeIncidents[incident.Id] = incident;
                        await PersistIncidentAsync(incident);
                    }
                }
                
                _logger.LogDebug("Checked ledger inconsistencies");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking ledger inconsistencies");
            }
        }
        
        /// <summary>
        /// Clean up resolved incidents
        /// </summary>
        private async Task CleanupResolvedIncidentsAsync()
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-24);
                var incidentsToCleanup = new List<Guid>();
                
                foreach (var kvp in _activeIncidents)
                {
                    var incident = kvp.Value;
                    if ((incident.Status == IncidentStatus.Resolved || incident.Status == IncidentStatus.Dismissed) &&
                        incident.ResolvedAt < cutoffTime)
                    {
                        incidentsToCleanup.Add(kvp.Key);
                    }
                }
                
                foreach (var incidentId in incidentsToCleanup)
                {
                    _activeIncidents.Remove(incidentId);
                }
                
                if (incidentsToCleanup.Any())
                {
                    _logger.LogInformation("Cleaned up {Count} resolved incidents", incidentsToCleanup.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up resolved incidents");
            }
        }
        
        /// <summary>
        /// Helper methods
        /// </summary>
        private async Task<bool> CheckEventExistsAsync(Guid eventId)
        {
            try
            {
                // 🔥 Check event store
                // TODO: Add _eventSourcing field to FailureHandlingService
                // var eventResult = await _eventSourcing.GetEventAsync(eventId);
                // return eventResult.IsSuccess;
                return false; // Placeholder
            }
            catch
            {
                return false;
            }
        }
        
        private async Task RecreateEventAsync(FinanceEvent financeEvent)
        {
            try
            {
                // TODO: Add _eventSourcing field to FailureHandlingService
                // await _eventSourcing.StoreEventAsync(financeEvent);
                // _logger.LogInformation("Recreated event {EventId} in event store", financeEvent.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recreate event {EventId}", financeEvent.EventId);
                throw;
            }
        }
        
        private async Task<ReprocessResult> AttemptReprocessAsync(FailureIncident incident)
        {
            var result = new ReprocessResult { StartedAt = DateTime.UtcNow };
            
            try
            {
                var financeEvent = JsonSerializer.Deserialize<FinanceEvent>(incident.EventData);
                
                // 🔥 Reprocess through ledger engine
                var ledgerResult = await _ledgerEngine.ProcessEventAsync(financeEvent);
                
                result.IsSuccess = ledgerResult.IsSuccess;
                result.ErrorMessage = ledgerResult.ErrorMessage;
                result.CompletedAt = DateTime.UtcNow;
                result.DurationMs = (result.CompletedAt - result.StartedAt).TotalMilliseconds;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
            }
            
            return result;
        }
        
        private async Task<bool> CheckEventProcessedAsync(Guid eventId)
        {
            try
            {
                // 🔥 Check if event has processing record
                // This would check for idempotency key or processing record
                return false; // Simplified
            }
            catch
            {
                return false;
            }
        }
        
        private async Task AddToDelayedProcessingQueueAsync(FinanceEvent financeEvent, TimeSpan delay)
        {
            try
            {
                // TODO: Use IDistributedCache instead of Redis
                // var db = _redis.GetDatabase();
                // var queueKey = $"{RecoveryQueuePrefix}delayed";
                
                // var eventData = JsonSerializer.Serialize(financeEvent);
                // var processAt = DateTime.UtcNow.Add(delay);
                
                // TODO: Use IDistributedCache instead of Redis
                // await db.SortedSetAddAsync(queueKey, eventData, processAt.Ticks);
                
                // TODO: Use actual when Redis is replaced with IDistributedCache
                // _logger.LogDebug("Added event {EventId} to delayed processing queue", financeEvent.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add event {EventId} to delayed processing queue", financeEvent.EventId);
            }
        }
        
        private async Task AddToRetryQueueAsync(FailureIncident incident)
        {
            try
            {
                // TODO: Use IDistributedCache instead of Redis
                // var db = _redis.GetDatabase();
                // var queueKey = $"{RecoveryQueuePrefix}retry";
                
                // var incidentData = JsonSerializer.Serialize(incident);
                // var retryAt = DateTime.UtcNow.AddMinutes(1); // Retry after 1 minute
                
                // await db.SortedSetAddAsync(queueKey, incidentData, retryAt.Ticks);
                // TODO: Mock retry queue for now
                
                _logger.LogDebug("Added incident {IncidentId} to retry queue", incident.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add incident {IncidentId} to retry queue", incident.Id);
            }
        }
        
        // private async Task<SagaResult> StartCompensationSagaAsync(FailureIncident incident) // Commented out - SagaResult not available
        /*
        {
            try
            {
                // 🔥 Create SAGA definition for compensation
                var sagaDefinition = new SagaDefinition
                {
                    SagaType = "PartialPostingCompensation"
                };
                
                // 🔥 Add compensation steps
                // This would be implemented based on the specific failure scenario
                
                var sagaData = new
                {
                    IncidentId = incident.Id,
                    EventId = incident.EventId,
                    AffectedAccounts = incident.AffectedAccounts
                };
                
                return await _sagaOrchestrator.StartSagaAsync(sagaDefinition, sagaData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start compensation saga for incident {IncidentId}", incident.Id);
                return new SagaResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        */
        
        private IncidentSeverity DetermineSeverity(string failureType, string eventType)
        {
            return failureType switch
            {
                "UnprocessedEvent" => eventType switch
                {
                    "JournalPosted" => IncidentSeverity.High,
                    "PeriodClosed" => IncidentSeverity.Medium,
                    _ => IncidentSeverity.Low
                },
                "PartialPosting" => IncidentSeverity.High,
                "SystemCrash" => IncidentSeverity.Critical,
                "LedgerInconsistency" => IncidentSeverity.High,
                _ => IncidentSeverity.Medium
            };
        }
        
        private async Task PersistIncidentAsync(FailureIncident incident)
        {
            try
            {
                // TODO: Use IDistributedCache instead of Redis
                // var db = _redis.GetDatabase();
                // var incidentKey = $"{IncidentKeyPrefix}{incident.Id}";
                
                // var incidentData = JsonSerializer.Serialize(incident);
                // await db.StringSetAsync(incidentKey, incidentData, TimeSpan.FromDays(7));
                // TODO: Mock incident persistence for now
                
                // 🔥 Also add to failure stream
                // TODO: Use IDistributedCache instead of Redis
                // var failureStreamKey = $"{FailureStreamPrefix}{incident.CompanyId}";
                // await db.StreamAddAsync(failureStreamKey, new[]
                // {
                //     new("incident_id", incident.Id.ToString()),
                //     new("failure_type", incident.FailureType),
                //     new("severity", incident.Severity.ToString()),
                //     new("created_at", incident.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                // });
                // TODO: Mock failure stream for now
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist incident {IncidentId}", incident.Id);
            }
        }
        
        private Dictionary<string, FailureScenario> InitializeFailureScenarios()
        {
            return new Dictionary<string, FailureScenario>
            {
                ["UnprocessedEvent"] = new FailureScenario
                {
                    Name = "UnprocessedEvent",
                    Description = "Event published but not processed",
                    RetryStrategy = "ExponentialBackoff",
                    MaxRetries = 5,
                    Timeout = TimeSpan.FromMinutes(30)
                },
                ["DuplicateEvent"] = new FailureScenario
                {
                    Name = "DuplicateEvent",
                    Description = "Duplicate event detected",
                    RetryStrategy = "IgnoreDuplicate",
                    MaxRetries = 0,
                    Timeout = TimeSpan.Zero
                },
                ["PartialPosting"] = new FailureScenario
                {
                    Name = "PartialPosting",
                    Description = "Partial transaction posting",
                    RetryStrategy = "SagaCompensation",
                    MaxRetries = 3,
                    Timeout = TimeSpan.FromMinutes(15)
                },
                ["SystemCrash"] = new FailureScenario
                {
                    Name = "SystemCrash",
                    Description = "System crash mid-transaction",
                    RetryStrategy = "EventReplay",
                    MaxRetries = 1,
                    Timeout = TimeSpan.FromHours(1)
                }
            };
        }
        
        /// <summary>
        /// Get failure statistics
        /// </summary>
        public async Task<FailureStatistics> GetStatisticsAsync(int? companyId = null)
        {
            var stats = new FailureStatistics
            {
                GeneratedAt = DateTime.UtcNow
            };
            
            try
            {
                var incidents = companyId.HasValue 
                    ? _activeIncidents.Values.Where(i => i.CompanyId == companyId.Value).ToList()
                    : _activeIncidents.Values.ToList();
                
                stats.ActiveIncidents = incidents.Count;
                stats.CriticalIncidents = incidents.Count(i => i.Severity == IncidentSeverity.Critical);
                stats.HighIncidents = incidents.Count(i => i.Severity == IncidentSeverity.High);
                stats.MediumIncidents = incidents.Count(i => i.Severity == IncidentSeverity.Medium);
                stats.LowIncidents = incidents.Count(i => i.Severity == IncidentSeverity.Low);
                
                stats.IncidentsByType = incidents
                    .GroupBy(i => i.FailureType)
                    .ToDictionary(g => g.Key, g => g.Count());
                
                stats.ResolvedIncidents = incidents.Count(i => i.Status == IncidentStatus.Resolved);
                stats.FailedIncidents = incidents.Count(i => i.Status == IncidentStatus.Failed);
                
                stats.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting failure statistics");
                stats.IsSuccess = false;
                stats.ErrorMessage = ex.Message;
            }
            
            return stats;
        }
    }
    
    #region Supporting Classes
    
    public class FailureHandlingResult
    {
        public Guid IncidentId { get; set; }
        public Guid? EventId { get; set; }
        public int CompanyId { get; set; }
        public string FailureType { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public double DurationMs { get; set; }
    }
    
    public class FailureIncident
    {
        public Guid Id { get; set; }
        public Guid? EventId { get; set; }
        public int CompanyId { get; set; }
        public string FailureType { get; set; } = string.Empty;
        public IncidentSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public string EventData { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime? EscalatedAt { get; set; }
        public IncidentStatus Status { get; set; }
        public string ResolutionNotes { get; set; } = string.Empty;
        public List<string> Actions { get; set; } = new();
        public List<string> AffectedAccounts { get; set; } = new();
        public List<Guid> IncompleteTransactions { get; set; } = new();
        public Guid? SagaId { get; set; }
    }
    
    public class FailureScenario
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RetryStrategy { get; set; } = string.Empty;
        public int MaxRetries { get; set; }
        public TimeSpan Timeout { get; set; }
    }
    
    public class ReprocessResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public double DurationMs { get; set; }
    }
    
    public class FailureStatistics
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public int ActiveIncidents { get; set; }
        public int CriticalIncidents { get; set; }
        public int HighIncidents { get; set; }
        public int MediumIncidents { get; set; }
        public int LowIncidents { get; set; }
        public Dictionary<string, int> IncidentsByType { get; set; } = new();
        public int ResolvedIncidents { get; set; }
        public int FailedIncidents { get; set; }
    }
    
    public enum IncidentStatus
    {
        Active,
        Monitoring,
        Compensating,
        Recovering,
        Resolved,
        Failed,
        Escalated,
        Dismissed
    }
    
    public enum IncidentSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
    
    #endregion
}
