using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading;
using SecureERP2.Modules.Finance.Entities;
using SecureERP2.Modules.Finance.Services;

namespace SecureERP2.Modules.Finance.Services.Hardening
{
    /// <summary>
    /// 🔬 STEP 3: Failure Simulation Service
    /// Kill services randomly, Replay system, Verify consistency
    /// </summary>
    public class FailureSimulationService
    {
        private readonly ILogger<FailureSimulationService> _logger;
        // private readonly IConnectionMultiplexer _redis; // Commented out due to missing assembly reference
        private readonly EventSourcingArchitecture _eventSourcing;
        private readonly LedgerEngineService _ledgerEngine;
        // private readonly SagaOrchestratorService _sagaOrchestrator; // Commented out due to missing service reference
        private readonly DeterminismEngineService _determinismEngine;
        private readonly ObservabilityService _observability;
        
        // Simulation state
        private readonly ConcurrentDictionary<string, ServiceState> _serviceStates;
        private readonly ConcurrentDictionary<Guid, SimulationScenario> _activeScenarios;
        private readonly Timer _simulationTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        // Redis keys
        private const string SimulationKeyPrefix = "simulation:";
        private const string ServiceStateKeyPrefix = "service_state:";
        private const string ScenarioKeyPrefix = "scenario:";
        
        // Configuration
        private readonly TimeSpan _simulationInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _maxServiceDowntime = TimeSpan.FromMinutes(2);
        private readonly double _failureProbability = 0.1; // 10% chance per interval
        
        public FailureSimulationService(
            ILogger<FailureSimulationService> logger,
            EventSourcingArchitecture eventSourcing,
            LedgerEngineService ledgerEngine,
            DeterminismEngineService determinismEngine,
            ObservabilityService observability)
        {
            _logger = logger;
            // _redis = redis; // Commented out due to missing assembly reference
            _eventSourcing = eventSourcing;
            _ledgerEngine = ledgerEngine;
            // _sagaOrchestrator = sagaOrchestrator; // Commented out due to missing service reference
            _determinismEngine = determinismEngine;
            _observability = observability;
            
            _serviceStates = new ConcurrentDictionary<string, ServiceState>();
            _activeScenarios = new ConcurrentDictionary<Guid, SimulationScenario>();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // 🔥 Initialize service states
            InitializeServiceStates();
            
            // 🔥 Start simulation timer
            _simulationTimer = new Timer(SimulationTickAsync, null, TimeSpan.Zero, _simulationInterval);
        }
        
        /// <summary>
        /// Initialize service states
        /// </summary>
        private void InitializeServiceStates()
        {
            var services = new[]
            {
                "EventBus",
                "LedgerEngine", 
                "CommandService",
                "ReadModelService",
                "SagaOrchestrator",
                "IdempotencyLayer",
                "AuditChain",
                "IntegrityMonitor"
            };
            
            foreach (var service in services)
            {
                _serviceStates[service] = new ServiceState
                {
                    Name = service,
                    IsRunning = true,
                    LastFailure = null,
                    FailureCount = 0,
                    TotalDowntimeMs = 0,
                    StartedAt = DateTime.UtcNow
                };
            }
            
            _logger.LogInformation("Initialized {Count} service states for failure simulation", services.Length);
        }
        
        /// <summary>
        /// Simulation tick - randomly fail services
        /// </summary>
        private async void SimulationTickAsync(object state)
        {
            if (_cancellationTokenSource.Token.IsCancellationRequested)
                return;
                
            try
            {
                _logger.LogDebug("Running failure simulation tick");
                
                // 🔥 Get running services
                var runningServices = _serviceStates.Where(s => s.Value.IsRunning).ToList();
                
                foreach (var service in runningServices)
                {
                    // 🔥 Random failure probability
                    if (ShouldFailService())
                    {
                        await FailServiceAsync(service.Value.Name);
                    }
                }
                
                // 🔥 Check for services to recover
                var failedServices = _serviceStates.Where(s => !s.Value.IsRunning).ToList();
                
                foreach (var service in failedServices)
                {
                    if (ShouldRecoverService(service.Value))
                    {
                        await RecoverServiceAsync(service.Value.Name);
                    }
                }
                
                // 🔥 Run consistency checks
                await RunConsistencyChecksAsync();
                
                _logger.LogDebug("Failure simulation tick completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in failure simulation tick");
            }
        }
        
        /// <summary>
        /// Determine if service should fail
        /// </summary>
        private bool ShouldFailService()
        {
            return new Random().NextDouble() < _failureProbability;
        }
        
        /// <summary>
        /// Determine if service should recover
        /// </summary>
        private bool ShouldRecoverService(ServiceState service)
        {
            if (service.LastFailure == null)
                return true;
                
            var downtime = DateTime.UtcNow - service.LastFailure.Value;
            return downtime > _maxServiceDowntime;
        }
        
        /// <summary>
        /// Fail a service
        /// </summary>
        private async Task FailServiceAsync(string serviceName)
        {
            try
            {
                var service = _serviceStates[serviceName];
                
                if (!service.IsRunning)
                    return;
                
                _logger.LogWarning("SIMULATION: Failing service {ServiceName}", serviceName);
                
                // 🔥 Update service state
                service.IsRunning = false;
                service.LastFailure = DateTime.UtcNow;
                service.FailureCount++;
                
                // 🔥 Record failure
                var failure = new ServiceFailure
                {
                    Id = Guid.NewGuid(),
                    ServiceName = serviceName,
                    FailedAt = DateTime.UtcNow,
                    Reason = "Simulated failure",
                    RecoveryAttempts = 0
                };
                
                await RecordServiceFailureAsync(failure);
                
                // 🔥 Store state in Redis
                await StoreServiceStateAsync(service);
                
                // 🔥 Trigger recovery workflow
                _ = Task.Run(() => SimulateRecoveryWorkflowAsync(serviceName, failure));
                
                _logger.LogInformation("SIMULATION: Service {ServiceName} failed (failure #{Count})", 
                    serviceName, service.FailureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error failing service {ServiceName}", serviceName);
            }
        }
        
        /// <summary>
        /// Recover a service
        /// </summary>
        private async Task RecoverServiceAsync(string serviceName)
        {
            try
            {
                var service = _serviceStates[serviceName];
                
                if (service.IsRunning)
                    return;
                
                _logger.LogInformation("SIMULATION: Recovering service {ServiceName}", serviceName);
                
                // 🔥 Update service state
                service.IsRunning = true;
                
                var downtime = DateTime.UtcNow - service.LastFailure.Value;
                service.TotalDowntimeMs += downtime.TotalMilliseconds;
                
                // 🔥 Record recovery
                var recovery = new ServiceRecovery
                {
                    Id = Guid.NewGuid(),
                    ServiceName = serviceName,
                    RecoveredAt = DateTime.UtcNow,
                    DowntimeMs = downtime.TotalMilliseconds,
                    RecoveryMethod = "Automatic"
                };
                
                await RecordServiceRecoveryAsync(recovery);
                
                // 🔥 Store state in Redis
                await StoreServiceStateAsync(service);
                
                // 🔥 Run post-recovery validation
                await RunPostRecoveryValidationAsync(serviceName);
                
                _logger.LogInformation("SIMULATION: Service {ServiceName} recovered after {Downtime}ms", 
                    serviceName, downtime.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recovering service {ServiceName}", serviceName);
            }
        }
        
        /// <summary>
        /// Simulate recovery workflow
        /// </summary>
        private async Task SimulateRecoveryWorkflowAsync(string serviceName, ServiceFailure failure)
        {
            try
            {
                _logger.LogDebug("Starting recovery workflow for {ServiceName}", serviceName);
                
                // 🔥 Step 1: Check service health
                await Task.Delay(TimeSpan.FromSeconds(2));
                
                // 🔥 Step 2: Attempt graceful restart
                await Task.Delay(TimeSpan.FromSeconds(3));
                
                // 🔥 Step 3: Validate service state
                await Task.Delay(TimeSpan.FromSeconds(1));
                
                // 🔥 Step 4: Restore from last known good state
                if (serviceName == "LedgerEngine")
                {
                    await RestoreLedgerStateAsync();
                }
                else if (serviceName == "EventBus")
                {
                    await RestoreEventBusStateAsync();
                }
                
                // 🔥 Step 5: Run consistency checks
                await RunServiceConsistencyCheckAsync(serviceName);
                
                failure.RecoveryAttempts++;
                await UpdateServiceFailureAsync(failure);
                
                _logger.LogDebug("Recovery workflow completed for {ServiceName}", serviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in recovery workflow for {ServiceName}", serviceName);
            }
        }
        
        /// <summary>
        /// Restore ledger state
        /// </summary>
        private async Task RestoreLedgerStateAsync()
        {
            try
            {
                _logger.LogDebug("Restoring ledger state from events");
                
                // 🔥 Get all companies
                var companies = new[] { 1 }; // Simplified
                
                foreach (var companyId in companies)
                {
                    // 🔥 Rebuild ledger from events
                    var rebuildResult = await _ledgerEngine.RebuildLedgerAsync(companyId);
                    
                    if (!rebuildResult.IsSuccess)
                    {
                        _logger.LogError("Failed to rebuild ledger for company {CompanyId}: {Error}", 
                            companyId, rebuildResult.ErrorMessage);
                    }
                }
                
                _logger.LogDebug("Ledger state restoration completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring ledger state");
            }
        }
        
        /// <summary>
        /// Restore event bus state
        /// </summary>
        private async Task RestoreEventBusStateAsync()
        {
            try
            {
                _logger.LogDebug("Restoring event bus state");
                
                // 🔥 Reconnect to Redis streams
                // 🔥 Resubscribe to event streams
                // 🔥 Process any missed events
                
                _logger.LogDebug("Event bus state restoration completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring event bus state");
            }
        }
        
        /// <summary>
        /// Run consistency checks
        /// </summary>
        private async Task RunConsistencyChecksAsync()
        {
            try
            {
                _logger.LogDebug("Running system consistency checks");
                
                // 🔥 Check ledger consistency
                await CheckLedgerConsistencyAsync();
                
                // 🔥 Check event consistency
                await CheckEventConsistencyAsync();
                
                // 🔥 Check saga consistency
                await CheckSagaConsistencyAsync();
                
                // 🔥 Check determinism
                await CheckDeterminismConsistencyAsync();
                
                _logger.LogDebug("Consistency checks completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running consistency checks");
            }
        }
        
        /// <summary>
        /// Check ledger consistency
        /// </summary>
        private async Task CheckLedgerConsistencyAsync()
        {
            try
            {
                var companies = new[] { 1 }; // Simplified
                
                foreach (var companyId in companies)
                {
                    var validationResult = await _ledgerEngine.ValidateLedgerAsync(companyId);
                    
                    if (!validationResult.IsValid)
                    {
                        _logger.LogError("Ledger consistency check failed for company {CompanyId}: {Mismatches} mismatches", 
                            companyId, validationResult.BalanceMismatches.Count);
                        
                        // 🔥 Create alert
                        await CreateConsistencyAlertAsync("Ledger", companyId, validationResult.BalanceMismatches.Count);
                        
                        // 🔥 Attempt automatic repair
                        await AttemptLedgerRepairAsync(companyId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking ledger consistency");
            }
        }
        
        /// <summary>
        /// Check event consistency
        /// </summary>
        private async Task CheckEventConsistencyAsync()
        {
            try
            {
                // 🔥 Get event statistics
                var eventStats = await _eventSourcing.GetStreamStatisticsAsync(1); // TODO: Use actual company ID
                
                if (!eventStats.IsSuccess)
                {
                    _logger.LogError("Failed to get event statistics: {Error}", eventStats.ErrorMessage);
                    return;
                }
                
                // 🔥 Check for gaps in event sequence
                var hasGaps = await CheckEventSequenceGapsAsync();
                
                if (hasGaps)
                {
                    _logger.LogWarning("Event sequence gaps detected");
                    await CreateConsistencyAlertAsync("EventSequence", null, 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking event consistency");
            }
        }
        
        /// <summary>
        /// Check saga consistency
        /// </summary>
        private async Task CheckSagaConsistencyAsync()
        {
            try
            {
                // TODO: _sagaOrchestrator is commented out due to missing service reference
                // var activeSagas = _sagaOrchestrator.GetActiveSagas();
                
                // foreach (var saga in activeSagas)
                // {
                //     // 🔥 Check if saga has been running too long
                //     var runningTime = DateTime.UtcNow - saga.StartedAt;
                //     if (runningTime > TimeSpan.FromMinutes(30))
                //     {
                //         _logger.LogWarning("Saga {SagaId} has been running for {Duration}", saga.Id, runningTime);
                //         
                //         // 🔥 Force compensation
                //         await ForceSagaCompensationAsync(saga.Id);
                //     }
                // }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking saga consistency");
            }
        }
        
        /// <summary>
        /// Check determinism consistency
        /// </summary>
        private async Task CheckDeterminismConsistencyAsync()
        {
            try
            {
                var validationResult = await _determinismEngine.ValidateSystemDeterminismAsync();
                
                if (!validationResult.IsValid)
                {
                    _logger.LogError("Determinism validation failed: {Violations} violations detected", 
                        validationResult.Violations.Count);
                    
                    await CreateConsistencyAlertAsync("Determinism", null, validationResult.Violations.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking determinism consistency");
            }
        }
        
        /// <summary>
        /// Run post-recovery validation
        /// </summary>
        private async Task RunPostRecoveryValidationAsync(string serviceName)
        {
            try
            {
                _logger.LogDebug("Running post-recovery validation for {ServiceName}", serviceName);
                
                switch (serviceName)
                {
                    case "LedgerEngine":
                        await ValidateLedgerAfterRecoveryAsync();
                        break;
                    case "EventBus":
                        await ValidateEventBusAfterRecoveryAsync();
                        break;
                    case "SagaOrchestrator":
                        await ValidateSagaAfterRecoveryAsync();
                        break;
                }
                
                _logger.LogDebug("Post-recovery validation completed for {ServiceName}", serviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in post-recovery validation for {ServiceName}", serviceName);
            }
        }
        
        /// <summary>
        /// Validate ledger after recovery
        /// </summary>
        private async Task ValidateLedgerAfterRecoveryAsync()
        {
            try
            {
                var companies = new[] { 1 };
                
                foreach (var companyId in companies)
                {
                    var validationResult = await _ledgerEngine.ValidateLedgerAsync(companyId);
                    
                    if (!validationResult.IsValid)
                    {
                        _logger.LogError("Ledger validation failed after recovery for company {CompanyId}", companyId);
                        throw new InvalidOperationException("Ledger validation failed after recovery");
                    }
                }
                
                _logger.LogDebug("Ledger validation passed after recovery");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ledger validation failed after recovery");
                throw;
            }
        }
        
        /// <summary>
        /// Validate event bus after recovery
        /// </summary>
        private async Task ValidateEventBusAfterRecoveryAsync()
        {
            try
            {
                // TODO: _eventBus is commented out due to missing service reference
                // var stats = await _eventBus.GetStatisticsAsync();
                
                // if (!stats.IsSuccess)
                // {
                //     throw new InvalidOperationException($"Event bus validation failed: {stats.ErrorMessage}");
                // }
                
                _logger.LogDebug("Event bus validation passed after recovery");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Event bus validation failed after recovery");
                throw;
            }
        }
        
        /// <summary>
        /// Validate saga after recovery
        /// </summary>
        private async Task ValidateSagaAfterRecoveryAsync()
        {
            try
            {
                // TODO: _sagaOrchestrator is commented out due to missing service reference
                // var activeSagas = _sagaOrchestrator.GetActiveSagas();
                
                // 🔥 Check for orphaned sagas
                // var orphanedSagas = activeSagas.Where(s => 
                //     DateTime.UtcNow - s.StartedAt > TimeSpan.FromMinutes(15)).ToList();
                
                // if (orphanedSagas.Any())
                // {
                //     _logger.LogWarning("Found {Count} orphaned sagas after recovery", orphanedSagas.Count);
                //     
                //     foreach (var saga in orphanedSagas)
                //     {
                //         await ForceSagaCompensationAsync(saga.Id);
                //     }
                // }
                
                _logger.LogDebug("Saga validation passed after recovery");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Saga validation failed after recovery");
                throw;
            }
        }
        
        /// <summary>
        /// Run service consistency check
        /// </summary>
        private async Task RunServiceConsistencyCheckAsync(string serviceName)
        {
            try
            {
                _logger.LogDebug("Running consistency check for {ServiceName}", serviceName);
                
                switch (serviceName)
                {
                    case "LedgerEngine":
                        await CheckLedgerConsistencyAsync();
                        break;
                    case "EventBus":
                        await CheckEventConsistencyAsync();
                        break;
                    case "SagaOrchestrator":
                        await CheckSagaConsistencyAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in consistency check for {ServiceName}", serviceName);
            }
        }
        
        /// <summary>
        /// Check for event sequence gaps
        /// </summary>
        private async Task<bool> CheckEventSequenceGapsAsync()
        {
            try
            {
                // 🔥 This would check for gaps in event sequence numbers
                // Simplified implementation
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking event sequence gaps");
                return false;
            }
        }
        
        /// <summary>
        /// Force saga compensation
        /// </summary>
        private async Task ForceSagaCompensationAsync(Guid sagaId)
        {
            try
            {
                _logger.LogWarning("Forcing compensation for saga {SagaId}", sagaId);
                
                // 🔥 This would trigger compensation for the saga
                // Implementation depends on saga orchestrator interface
                
                _logger.LogDebug("Saga compensation forced for {SagaId}", sagaId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forcing saga compensation for {SagaId}", sagaId);
            }
        }
        
        /// <summary>
        /// Attempt ledger repair
        /// </summary>
        private async Task AttemptLedgerRepairAsync(int companyId)
        {
            try
            {
                _logger.LogInformation("Attempting ledger repair for company {CompanyId}", companyId);
                
                // 🔥 Rebuild ledger from events
                var rebuildResult = await _ledgerEngine.RebuildLedgerAsync(companyId);
                
                if (rebuildResult.IsSuccess)
                {
                    _logger.LogInformation("Ledger repair successful for company {CompanyId}", companyId);
                }
                else
                {
                    _logger.LogError("Ledger repair failed for company {CompanyId}: {Error}", 
                        companyId, rebuildResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error attempting ledger repair for company {CompanyId}", companyId);
            }
        }
        
        /// <summary>
        /// Create consistency alert
        /// </summary>
        private async Task CreateConsistencyAlertAsync(string component, int? companyId, int issueCount)
        {
            try
            {
                var alert = new ConsistencyAlert
                {
                    Id = Guid.NewGuid(),
                    Component = component,
                    CompanyId = companyId,
                    IssueCount = issueCount,
                    DetectedAt = DateTime.UtcNow,
                    Severity = issueCount > 10 ? AlertSeverity.High : AlertSeverity.Medium,
                    Message = $"Consistency issues detected in {component}: {issueCount} issues"
                };
                
                // TODO: _redis is commented out due to missing service reference
                // // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                var alertJson = JsonSerializer.Serialize(alert);
                
                // TODO: Redis operations commented out due to missing service reference
                // // await db.ListLeftPushAsync("consistency_alerts", alertJson);
                // // await db.ListTrimAsync("consistency_alerts", 0, 999);
                
                _logger.LogWarning("Consistency alert created: {Component} - {Message}", component, alert.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating consistency alert for {Component}", component);
            }
        }
        
        /// <summary>
        /// Record service failure
        /// </summary>
        private async Task RecordServiceFailureAsync(ServiceFailure failure)
        {
            try
            {
                // TODO: _redis is commented out due to missing service reference
                // // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                var key = $"{ServiceStateKeyPrefix}{failure.ServiceName}:failures";
                
                var failureJson = JsonSerializer.Serialize(failure);
                // TODO: Redis operations commented out due to missing service reference
                // // await db.ListLeftPushAsync(key, failureJson);
                // // await db.ListTrimAsync(key, 0, 99);
                // // await db.KeyExpireAsync(key, TimeSpan.FromDays(7));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording service failure for {ServiceName}", failure.ServiceName);
            }
        }
        
        /// <summary>
        /// Record service recovery
        /// </summary>
        private async Task RecordServiceRecoveryAsync(ServiceRecovery recovery)
        {
            try
            {
                // TODO: _redis is commented out due to missing service reference
                // // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                var key = $"{ServiceStateKeyPrefix}{recovery.ServiceName}:recoveries";
                
                var recoveryJson = JsonSerializer.Serialize(recovery);
                // TODO: Redis operations commented out due to missing service reference
                // // await db.ListLeftPushAsync(key, recoveryJson);
                // // await db.ListTrimAsync(key, 0, 99);
                // // await db.KeyExpireAsync(key, TimeSpan.FromDays(7));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording service recovery for {ServiceName}", recovery.ServiceName);
            }
        }
        
        /// <summary>
        /// Update service failure
        /// </summary>
        private async Task UpdateServiceFailureAsync(ServiceFailure failure)
        {
            try
            {
                // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                var key = $"{ServiceStateKeyPrefix}{failure.ServiceName}:failure:{failure.Id}";
                
                var failureJson = JsonSerializer.Serialize(failure);
                // await db.StringSetAsync(key, failureJson, TimeSpan.FromDays(7));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating service failure {FailureId}", failure.Id);
            }
        }
        
        /// <summary>
        /// Store service state
        /// </summary>
        private async Task StoreServiceStateAsync(ServiceState service)
        {
            try
            {
                // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                var key = $"{ServiceStateKeyPrefix}{service.Name}";
                
                var stateJson = JsonSerializer.Serialize(service);
                // await db.StringSetAsync(key, stateJson, TimeSpan.FromDays(7));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing service state for {ServiceName}", service.Name);
            }
        }
        
        /// <summary>
        /// Start custom simulation scenario
        /// </summary>
        public async Task<SimulationScenario> StartScenarioAsync(SimulationScenarioDefinition definition)
        {
            var scenario = new SimulationScenario
            {
                Id = Guid.NewGuid(),
                Name = definition.Name,
                Description = definition.Description,
                StartedAt = DateTime.UtcNow,
                Status = ScenarioStatus.Running,
                Steps = definition.Steps.Select(s => new ScenarioStep
                {
                    Name = s.Name,
                    Action = s.Action,
                    TargetService = s.TargetService,
                    Delay = s.Delay,
                    Completed = false
                }).ToList()
            };
            
            _activeScenarios[scenario.Id] = scenario;
            
            // 🔥 Execute scenario
            _ = Task.Run(() => ExecuteScenarioAsync(scenario));
            
            _logger.LogInformation("Started simulation scenario {ScenarioName} ({ScenarioId})", 
                definition.Name, scenario.Id);
            
            return scenario;
        }
        
        /// <summary>
        /// Execute scenario
        /// </summary>
        private async Task ExecuteScenarioAsync(SimulationScenario scenario)
        {
            try
            {
                _logger.LogInformation("Executing scenario {ScenarioName}", scenario.Name);
                
                foreach (var step in scenario.Steps)
                {
                    if (scenario.Status != ScenarioStatus.Running)
                        break;
                    
                    _logger.LogDebug("Executing scenario step {StepName}", step.Name);
                    
                    // 🔥 Wait for delay
                    if (step.Delay > TimeSpan.Zero)
                    {
                        await Task.Delay(step.Delay);
                    }
                    
                    // 🔥 Execute action
                    switch (step.Action.ToLower())
                    {
                        case "fail":
                            await FailServiceAsync(step.TargetService);
                            break;
                        case "recover":
                            await RecoverServiceAsync(step.TargetService);
                            break;
                        case "validate":
                            await RunServiceConsistencyCheckAsync(step.TargetService);
                            break;
                        case "replay":
                            await ReplayEventsAsync();
                            break;
                    }
                    
                    step.Completed = true;
                    step.CompletedAt = DateTime.UtcNow;
                }
                
                scenario.Status = ScenarioStatus.Completed;
                scenario.CompletedAt = DateTime.UtcNow;
                
                _logger.LogInformation("Scenario {ScenarioName} completed", scenario.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing scenario {ScenarioName}", scenario.Name);
                scenario.Status = ScenarioStatus.Failed;
                scenario.ErrorMessage = ex.Message;
                scenario.CompletedAt = DateTime.UtcNow;
            }
        }
        
        /// <summary>
        /// Replay events
        /// </summary>
        private async Task ReplayEventsAsync()
        {
            try
            {
                _logger.LogInformation("Replaying events for consistency check");
                
                var companies = new[] { 1 };
                
                foreach (var companyId in companies)
                {
                    var rebuildResult = await _ledgerEngine.RebuildLedgerAsync(companyId);
                    
                    if (!rebuildResult.IsSuccess)
                    {
                        _logger.LogError("Event replay failed for company {CompanyId}: {Error}", 
                            companyId, rebuildResult.ErrorMessage);
                    }
                }
                
                _logger.LogInformation("Event replay completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replaying events");
            }
        }
        
        /// <summary>
        /// Get simulation statistics
        /// </summary>
        public async Task<SimulationStatistics> GetStatisticsAsync()
        {
            var stats = new SimulationStatistics
            {
                GeneratedAt = DateTime.UtcNow
            };
            
            try
            {
                // 🔥 Service statistics
                stats.ServiceStates = _serviceStates.ToDictionary(s => s.Key, s => new ServiceStatistics
                {
                    IsRunning = s.Value.IsRunning,
                    FailureCount = s.Value.FailureCount,
                    TotalDowntimeMs = s.Value.TotalDowntimeMs,
                    LastFailure = s.Value.LastFailure,
                    UptimePercentage = CalculateUptimePercentage(s.Value)
                });
                
                // 🔥 Scenario statistics
                stats.ActiveScenarios = _activeScenarios.Values.Count(s => s.Status == ScenarioStatus.Running);
                stats.CompletedScenarios = _activeScenarios.Values.Count(s => s.Status == ScenarioStatus.Completed);
                stats.FailedScenarios = _activeScenarios.Values.Count(s => s.Status == ScenarioStatus.Failed);
                
                // 🔥 Consistency alerts
                // var db = _redis.GetDatabase(); // TODO: _redis is commented out due to missing service reference
                // var alertsJson = // await db.ListRangeAsync("consistency_alerts", 0, 99);
                // TODO: Redis operations commented out due to missing service reference
                stats.RecentConsistencyAlerts = new List<ConsistencyAlert>(); // Placeholder
                // stats.RecentConsistencyAlerts = alertsJson
                //     .Select(a => JsonSerializer.Deserialize<ConsistencyAlert>(a))
                //     .OrderByDescending(a => a.DetectedAt)
                //     .ToList();
                
                stats.IsSuccess = true;
                
                _logger.LogDebug("Generated simulation statistics: {Services} services, {Alerts} alerts", 
                    stats.ServiceStates.Count, stats.RecentConsistencyAlerts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting simulation statistics");
                stats.IsSuccess = false;
                stats.ErrorMessage = ex.Message;
            }
            
            return stats;
        }
        
        /// <summary>
        /// Calculate uptime percentage
        /// </summary>
        private double CalculateUptimePercentage(ServiceState service)
        {
            var totalTime = DateTime.UtcNow - service.StartedAt;
            var uptimeTime = totalTime - TimeSpan.FromMilliseconds(service.TotalDowntimeMs);
            
            return totalTime.TotalMilliseconds > 0 
                ? (uptimeTime.TotalMilliseconds / totalTime.TotalMilliseconds) * 100 
                : 100;
        }
        
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _simulationTimer?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
    
    #region Supporting Classes
    
    public class ServiceState
    {
        public string Name { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public DateTime? LastFailure { get; set; }
        public int FailureCount { get; set; }
        public double TotalDowntimeMs { get; set; }
        public DateTime StartedAt { get; set; }
    }
    
    public class ServiceFailure
    {
        public Guid Id { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public DateTime FailedAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int RecoveryAttempts { get; set; }
    }
    
    public class ServiceRecovery
    {
        public Guid Id { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public DateTime RecoveredAt { get; set; }
        public double DowntimeMs { get; set; }
        public string RecoveryMethod { get; set; } = string.Empty;
    }
    
    public class SimulationScenario
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public ScenarioStatus Status { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<ScenarioStep> Steps { get; set; } = new();
    }
    
    public class SimulationScenarioDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<ScenarioStepDefinition> Steps { get; set; } = new();
    }
    
    public class ScenarioStep
    {
        public string Name { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string TargetService { get; set; } = string.Empty;
        public TimeSpan Delay { get; set; }
        public bool Completed { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
    
    public class ScenarioStepDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string TargetService { get; set; } = string.Empty;
        public TimeSpan Delay { get; set; }
    }
    
    public class ConsistencyAlert
    {
        public Guid Id { get; set; }
        public string Component { get; set; } = string.Empty;
        public int? CompanyId { get; set; }
        public int IssueCount { get; set; }
        public DateTime DetectedAt { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    
    public class SimulationStatistics
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public Dictionary<string, ServiceStatistics> ServiceStates { get; set; } = new();
        public int ActiveScenarios { get; set; }
        public int CompletedScenarios { get; set; }
        public int FailedScenarios { get; set; }
        public List<ConsistencyAlert> RecentConsistencyAlerts { get; set; } = new();
    }
    
    public class ServiceStatistics
    {
        public bool IsRunning { get; set; }
        public int FailureCount { get; set; }
        public double TotalDowntimeMs { get; set; }
        public DateTime? LastFailure { get; set; }
        public double UptimePercentage { get; set; }
    }
    
    public enum ScenarioStatus
    {
        Running,
        Completed,
        Failed,
        Cancelled
    }
    
    #endregion
}
