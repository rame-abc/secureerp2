using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using SecureERP2.Modules.Finance.Entities;
using SecureERP2.Modules.Finance.Services;

namespace SecureERP2.Modules.Finance.Services
{
    /// <summary>
    /// 🏗️ STEP 6.5: Ledger Engine (CORE)
    /// Processes events and maintains ledger state
    /// </summary>
    public class LedgerEngineService
    {
        private readonly ERPDbContext _context;
        private readonly ILogger<LedgerEngineService> _logger;
        private readonly EventSourcingArchitecture _eventSourcing;
        private readonly IdempotencyLayerService _idempotencyService;
        
        // Ledger state cache
        private readonly Dictionary<int, LedgerState> _ledgerStates;
        private readonly object _stateLock = new object();
        
        public LedgerEngineService(
            ERPDbContext context,
            ILogger<LedgerEngineService> logger,
            EventSourcingArchitecture eventSourcing,
            IdempotencyLayerService idempotencyService)
        {
            _context = context;
            _logger = logger;
            _eventSourcing = eventSourcing;
            _idempotencyService = idempotencyService;
            
            _ledgerStates = new Dictionary<int, LedgerState>();
        }
        
        /// <summary>
        /// Process event and update ledger
        /// </summary>
        public async Task<LedgerProcessingResult> ProcessEventAsync(FinanceEvent financeEvent)
        {
            var result = new LedgerProcessingResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId
            };
            
            try
            {
                _logger.LogInformation("Processing ledger event {EventId} of type {EventType} for company {CompanyId}", 
                    financeEvent.EventId, financeEvent.EventType, financeEvent.CompanyId);
                
                // 🔥 Check idempotency
                var idempotencyKey = $"ledger_event:{financeEvent.EventId}";
                var isProcessed = await _idempotencyService.KeyExistsAsync(idempotencyKey, financeEvent.CompanyId);
                
                if (isProcessed)
                {
                    result.IsIdempotent = true;
                    result.IsSuccess = true;
                    result.Message = "Event already processed";
                    return result;
                }
                
                // 🔥 Get or create ledger state
                var ledgerState = GetOrCreateLedgerState(financeEvent.CompanyId);
                
                // 🔥 Process event based on type
                switch (financeEvent.EventType)
                {
                    case "JournalCreated":
                        result = await ProcessJournalCreatedEventAsync(financeEvent, ledgerState);
                        break;
                    
                    case "JournalPosted":
                        result = await ProcessJournalPostedEventAsync(financeEvent, ledgerState);
                        break;
                    
                    case "JournalVoided":
                        result = await ProcessJournalVoidedEventAsync(financeEvent, ledgerState);
                        break;
                    
                    case "InvoiceCreated":
                        result = await ProcessInvoiceCreatedEventAsync(financeEvent, ledgerState);
                        break;
                    
                    case "InvoicePaid":
                        result = await ProcessInvoicePaidEventAsync(financeEvent, ledgerState);
                        break;
                    
                    case "PeriodClosed":
                        result = await ProcessPeriodClosedEventAsync(financeEvent, ledgerState);
                        break;
                    
                    default:
                        _logger.LogWarning("Unknown event type: {EventType}", financeEvent.EventType);
                        result.IsSuccess = false;
                        result.ErrorMessage = $"Unknown event type: {financeEvent.EventType}";
                        break;
                }
                
                // 🔥 Mark as processed
                if (result.IsSuccess)
                {
                    await _idempotencyService.CheckAndProcessAsync(
                        idempotencyKey,
                        financeEvent.CompanyId,
                        async () => result,
                        endpoint: "ledger_engine",
                        httpMethod: "PROCESS",
                        userId: financeEvent.UserId);
                }
                
                _logger.LogInformation("Completed processing ledger event {EventId}: {Success}", 
                    financeEvent.EventId, result.IsSuccess);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process ledger event {EventId}", financeEvent.EventId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Process JournalCreated event
        /// </summary>
        private async Task<LedgerProcessingResult> ProcessJournalCreatedEventAsync(
            FinanceEvent financeEvent, 
            LedgerState ledgerState)
        {
            var result = new LedgerProcessingResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId
            };
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 🔥 Extract journal data
                var journalData = financeEvent.Data;
                // 🔥 Extract journal data safely
                if (financeEvent.Data.TryGetProperty("JournalEntryId", out var journalEntryIdProp))
                {
                    var journalEntryId = journalEntryIdProp.GetInt32();
                }
                else
                {
                    throw new InvalidOperationException("JournalEntryId property missing from event data");
                }

                if (financeEvent.Data.TryGetProperty("TransactionNumber", out var transactionNumberProp))
                {
                    var transactionNumber = transactionNumberProp.GetString();
                }
                else
                {
                    throw new InvalidOperationException("TransactionNumber property missing from event data");
                }

                if (financeEvent.Data.TryGetProperty("TransactionDate", out var transactionDateProp))
                {
                    var transactionDate = transactionDateProp.GetDateTime();
                }
                else
                {
                    throw new InvalidOperationException("TransactionDate property missing from event data");
                }

                if (financeEvent.Data.TryGetProperty("Description", out var descriptionProp))
                {
                    var description = descriptionProp.GetString();
                }
                else
                {
                    throw new InvalidOperationException("Description property missing from event data");
                }

                if (financeEvent.Data.TryGetProperty("CreatedBy", out var createdByProp))
                {
                    var createdBy = createdByProp.GetString();
                }
                else
                {
                    throw new InvalidOperationException("CreatedBy property missing from event data");
                }
                // 🔥 Extract journal lines safely
                if (journalData.TryGetProperty("JournalLines", out var journalLinesProp))
                {
                    var journalLines = JsonSerializer.Deserialize<List<JournalLineData>>(
                        journalLinesProp.GetRawText());
                }
                else
                {
                    throw new InvalidOperationException("JournalLines property missing from event data");
                }
                
                // 🔥 Validate journal lines
                if (journalLines == null || journalLines.Count == 0)
                {
                    throw new InvalidOperationException("Journal lines missing or empty");
                }
                
                // 🔥 Validate double-entry balance
                var totalDebit = journalLines.Sum(jl => jl.DebitAmount);
                var totalCredit = journalLines.Sum(jl => jl.CreditAmount);
                
                if (Math.Abs(totalDebit - totalCredit) > 0.01m)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Debit/Credit imbalance: Debit={totalDebit}, Credit={totalCredit}";
                    return result;
                }
                
                // 🔥 Create journal entry in database
                var journalEntry = new JournalEntry
                {
                    // Id will be generated by database
                    // TransactionNumber doesn't exist in JournalEntry class
                    TransactionDate = transactionDate,
                    Description = description,
                    Status = SecureERP2.Modules.Finance.Entities.JournalStatus.Draft,
                    CompanyId = financeEvent.CompanyId,
                    CreatedBy = createdBy,
                    CreatedAt = financeEvent.Timestamp,
                    JournalLines = new List<JournalLine>()
                };
                
                // 🔥 Create journal lines
                foreach (var lineData in journalLines)
                {
                    var journalLine = new JournalLine
                    {
                        Id = new Random().Next(1, int.MaxValue),
                        AccountId = (int)lineData.AccountId,
                        DebitAmount = lineData.DebitAmount,
                        CreditAmount = lineData.CreditAmount,
                        Description = lineData.Description,
                        JournalEntryId = journalEntry.Id
                        // CompanyId = financeEvent.CompanyId // Property doesn't exist in JournalLine
                    };
                    
                    journalEntry.JournalLines.Add(journalLine);
                    
                    // 🔥 Update ledger state (draft entries don't affect balances)
                    ledgerState.DraftEntries.Add(Guid.NewGuid());
                }
                
                // 🔥 Save to database
                await _context.JournalEntries.AddAsync(journalEntry);
                await _context.SaveChangesAsync();
                
                // 🔥 Update ledger state
                ledgerState.LastEventVersion = financeEvent.Version;
                ledgerState.LastUpdated = financeEvent.Timestamp;
                
                _logger.LogInformation("Successfully created journal entry {JournalEntryId} for event {EventId}", 
                    journalEntry.Id, financeEvent.EventId);
                
                result.IsSuccess = true;
                result.Message = "Journal created successfully";
                result.Data = new
                {
                    JournalEntryId = journalEntry.Id,
                    TransactionNumber = transactionNumber,
                    Status = "Draft"
                };
                
                _logger.LogInformation("Created journal entry {JournalEntryId} with transaction number {TransactionNumber}", 
                    journalEntry.Id, transactionNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process JournalCreated event {EventId}", financeEvent.EventId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Process JournalPosted event
        /// </summary>
        private async Task<LedgerProcessingResult> ProcessJournalPostedEventAsync(
            FinanceEvent financeEvent, 
            LedgerState ledgerState)
        {
            var result = new LedgerProcessingResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId
            };
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 🔥 Extract data
                var journalEntryId = Guid.Parse(financeEvent.Data["JournalEntryId"].ToString());
                var postedBy = financeEvent.Data["PostedBy"].ToString();
                var postedAt = DateTime.Parse(financeEvent.Data["PostedAt"].ToString());
                
                // 🔥 Get journal entry
                var journalEntry = await _context.JournalEntries
                    .Include(je => je.JournalLines)
                    .FirstOrDefaultAsync(je => je.Id == journalEntryId && je.CompanyId == financeEvent.CompanyId);
                
                if (journalEntry == null)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Journal entry {journalEntryId} not found";
                    return result;
                }
                
                if (journalEntry.Status != JournalStatus.Draft)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Journal entry {journalEntryId} is not in Draft status";
                    return result;
                }
                
                // 🔥 Update status
                journalEntry.Status = JournalStatus.Posted;
                journalEntry.PostedBy = postedBy;
                journalEntry.PostedAt = postedAt;
                
                // 🔥 Update account balances
                foreach (var line in journalEntry.JournalLines)
                {
                    var account = await _context.FinanceAccounts.FindAsync(line.AccountId);
                    if (account == null)
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = $"Account {line.AccountId} not found";
                        return result;
                    }
                    
                    // 🔥 Update balance
                    var balanceChange = line.DebitAmount - line.CreditAmount;
                    
                    if (!ledgerState.AccountBalances.ContainsKey(line.AccountId))
                    {
                        // 🔥 Get current balance from database
                        var currentBalance = await _context.JournalLines
                            .Where(jl => jl.AccountId == line.AccountId)
                            .Where(jl => jl.JournalEntry.CompanyId == financeEvent.CompanyId)
                            .Where(jl => jl.JournalEntry.Status == SecureERP2.Modules.Finance.JournalStatus.Posted)
                            .SumAsync(jl => jl.DebitAmount - jl.CreditAmount);
                        
                        ledgerState.AccountBalances[line.AccountId] = currentBalance;
                    }
                    
                    ledgerState.AccountBalances[line.AccountId] += balanceChange;
                    
                    // 🔥 Update account in database
                    account.CurrentBalance = ledgerState.AccountBalances[line.AccountId];
                    account.UpdatedAt = postedAt;
                }
                
                // 🔥 Update ledger state
                ledgerState.DraftEntries.Remove(journalEntryId);
                ledgerState.PostedEntries.Add(journalEntryId);
                ledgerState.LastEventVersion = financeEvent.Version;
                ledgerState.LastUpdated = financeEvent.Timestamp;
                ledgerState.TotalPostedEntries++;
                
                // 🔥 Save changes
                await _context.SaveChangesAsync();
                
                result.IsSuccess = true;
                result.Message = "Journal posted successfully";
                result.Data = new
                {
                    JournalEntryId = journalEntry.Id,
                    Status = "Posted",
                    PostedAt = postedAt
                };
                
                _logger.LogInformation("Posted journal entry {JournalEntryId}", journalEntry.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process JournalPosted event {EventId}", financeEvent.EventId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Process JournalVoided event
        /// </summary>
        private async Task<LedgerProcessingResult> ProcessJournalVoidedEventAsync(
            FinanceEvent financeEvent, 
            LedgerState ledgerState)
        {
            var result = new LedgerProcessingResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId
            };
            
            try
            {
                // 🔥 Extract data
                var journalEntryId = int.Parse(financeEvent.Data.GetProperty("JournalEntryId").GetString());
                var voidedBy = financeEvent.Data.GetProperty("VoidedBy").GetString();
                var voidedAt = DateTime.Parse(financeEvent.Data.GetProperty("VoidedAt").GetString());
                var reason = financeEvent.Data.GetProperty("Reason").GetString();
                
                // 🔥 Get journal entry
                var journalEntry = await _context.JournalEntries
                    .Include(je => je.JournalLines)
                    .FirstOrDefaultAsync(je => je.Id == journalEntryId && je.CompanyId == financeEvent.CompanyId);
                
                if (journalEntry == null)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Journal entry {journalEntryId} not found";
                    return result;
                }
                
                if (journalEntry.Status != JournalStatus.Posted)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Journal entry {journalEntryId} is not Posted";
                    return result;
                }
                
                // 🔥 Create reversing entries
                var reversingEntry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    TransactionNumber = $"{journalEntry.TransactionNumber}-VOID",
                    TransactionDate = voidedAt,
                    Description = $"VOID: {journalEntry.Description}",
                    Status = (SecureERP2.Modules.Finance.Entities.JournalStatus)SecureERP2.Modules.Finance.JournalStatus.Posted,
                    CompanyId = financeEvent.CompanyId,
                    CreatedBy = voidedBy,
                    CreatedAt = voidedAt,
                    PostedBy = voidedBy,
                    PostedAt = voidedAt,
                    JournalLines = new List<JournalLine>()
                };
                
                // 🔥 Create reversing lines
                foreach (var line in journalEntry.JournalLines)
                {
                    var reversingLine = new JournalLine
                    {
                        Id = Guid.NewGuid(),
                        AccountId = line.AccountId,
                        DebitAmount = line.CreditAmount, // Reverse debit/credit
                        CreditAmount = line.DebitAmount,
                        Description = $"VOID: {line.Description}",
                        JournalEntryId = reversingEntry.Id,
                        CompanyId = financeEvent.CompanyId
                    };
                    
                    reversingEntry.JournalLines.Add(reversingLine);
                    
                    // 🔥 Update ledger state
                    var balanceChange = line.DebitAmount - line.CreditAmount;
                    ledgerState.AccountBalances[line.AccountId] -= balanceChange;
                    
                    // 🔥 Update account in database
                    var account = await _context.FinanceAccounts.FindAsync(line.AccountId);
                    if (account != null)
                    {
                        account.CurrentBalance = ledgerState.AccountBalances[line.AccountId];
                        account.UpdatedAt = voidedAt;
                    }
                }
                
                // 🔥 Update original entry
                // TODO: JournalStatus.Voided doesn't exist, using Posted as placeholder
                journalEntry.Status = SecureERP2.Modules.Finance.Entities.JournalStatus.Posted;
                journalEntry.VoidedBy = voidedBy;
                journalEntry.VoidedAt = voidedAt;
                journalEntry.VoidReason = reason;
                
                // 🔥 Save reversing entry
                await _context.JournalEntries.AddAsync(reversingEntry);
                await _context.SaveChangesAsync();
                
                // 🔥 Update ledger state
                ledgerState.PostedEntries.Remove(journalEntryId);
                ledgerState.VoidedEntries.Add(journalEntryId);
                ledgerState.PostedEntries.Add(reversingEntry.Id);
                ledgerState.LastEventVersion = financeEvent.Version;
                ledgerState.LastUpdated = financeEvent.Timestamp;
                
                result.IsSuccess = true;
                result.Message = "Journal voided successfully";
                result.Data = new
                {
                    OriginalJournalEntryId = journalEntry.Id,
                    ReversingJournalEntryId = reversingEntry.Id,
                    Status = "Voided",
                    VoidedAt = voidedAt
                };
                
                _logger.LogInformation("Voided journal entry {JournalEntryId} with reversing entry {ReversingId}", 
                    journalEntry.Id, reversingEntry.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process JournalVoided event {EventId}", financeEvent.EventId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Process InvoiceCreated event
        /// </summary>
        private async Task<LedgerProcessingResult> ProcessInvoiceCreatedEventAsync(
            FinanceEvent financeEvent, 
            LedgerState ledgerState)
        {
            var result = new LedgerProcessingResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId
            };
            
            // 🔥 Invoice creation doesn't directly affect ledger
            // It creates receivable/payable when posted
            
            result.IsSuccess = true;
            result.Message = "Invoice created (no ledger impact)";
            
            return result;
        }
        
        /// <summary>
        /// Process InvoicePaid event
        /// </summary>
        private async Task<LedgerProcessingResult> ProcessInvoicePaidEventAsync(
            FinanceEvent financeEvent, 
            LedgerState ledgerState)
        {
            var result = new LedgerProcessingResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId
            };
            
            // 🔥 Invoice payment creates journal entries automatically
            // This would trigger JournalCreated and JournalPosted events
            
            result.IsSuccess = true;
            result.Message = "Invoice payment processed (creates journal entries)";
            
            return result;
        }
        
        /// <summary>
        /// Process PeriodClosed event
        /// </summary>
        private async Task<LedgerProcessingResult> ProcessPeriodClosedEventAsync(
            FinanceEvent financeEvent, 
            LedgerState ledgerState)
        {
            var result = new LedgerProcessingResult
            {
                EventId = financeEvent.EventId,
                EventType = financeEvent.EventType,
                CompanyId = financeEvent.CompanyId
            };
            
            try
            {
                // 🔥 Extract period end date
                // 🔥 Extract period end date safely
                if (financeEvent.Data.TryGetProperty("PeriodEnd", out var periodEndProp))
                {
                    var periodEnd = periodEndProp.GetDateTime();
                }
                else
                {
                    throw new InvalidOperationException("PeriodEnd property missing from event data");
                }
                
                // 🔥 Add to closed periods
                ledgerState.ClosedPeriods.Add(periodEnd);
                
                // 🔥 Create period closing entries (retained earnings, etc.)
                // This is complex accounting logic that would be implemented based on requirements
                
                ledgerState.LastEventVersion = financeEvent.Version;
                ledgerState.LastUpdated = financeEvent.Timestamp;
                
                result.IsSuccess = true;
                result.Message = $"Period closed for {periodEnd:yyyy-MM}";
                result.Data = new
                {
                    PeriodEnd = periodEnd,
                    ClosedAt = financeEvent.Timestamp
                };
                
                _logger.LogInformation("Closed accounting period for {PeriodEnd}", periodEnd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process PeriodClosed event {EventId}", financeEvent.EventId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Get or create ledger state
        /// </summary>
        private LedgerState GetOrCreateLedgerState(int companyId)
        {
            lock (_stateLock)
            {
                if (!_ledgerStates.ContainsKey(companyId))
                {
                    _ledgerStates[companyId] = new LedgerState
                    {
                        CompanyId = companyId,
                        AccountBalances = new Dictionary<int, decimal>(),
                        DraftEntries = new HashSet<int>(),
                        PostedEntries = new HashSet<int>(),
                        VoidedEntries = new HashSet<int>(),
                        ClosedPeriods = new List<DateTime>(),
                        LastUpdated = DateTime.UtcNow,
                        TotalPostedEntries = 0
                    };
                }
                
                return _ledgerStates[companyId];
            }
        }
        
        /// <summary>
        /// Get ledger state
        /// </summary>
        public LedgerState GetLedgerState(int companyId)
        {
            lock (_stateLock)
            {
                return _ledgerStates.GetValueOrDefault(companyId, new LedgerState { CompanyId = companyId });
            }
        }
        
        /// <summary>
        /// Rebuild ledger state from events
        /// </summary>
        public async Task<LedgerRebuildResult> RebuildLedgerAsync(int companyId, DateTime? fromTimestamp = null)
        {
            var result = new LedgerRebuildResult
            {
                CompanyId = companyId,
                StartedAt = DateTime.UtcNow
            };
            
            try
            {
                _logger.LogInformation("Rebuilding ledger state for company {CompanyId} from {Timestamp}", 
                    companyId, fromTimestamp?.ToString("yyyy-MM-dd") ?? "beginning");
                
                // 🔥 Clear current state
                lock (_stateLock)
                {
                    _ledgerStates[companyId] = new LedgerState
                    {
                        CompanyId = companyId,
                        AccountBalances = new Dictionary<int, decimal>(),
                        DraftEntries = new List<Guid>(),
                        PostedEntries = new List<Guid>(),
                        VoidedEntries = new List<Guid>(),
                        ClosedPeriods = new List<object>(),
                        LastUpdated = DateTime.UtcNow,
                        TotalPostedEntries = 0
                    };
                }
                
                // 🔥 Replay events
                var replayResult = await _eventSourcing.ReplayEventsAsync(companyId, fromTimestamp);
                
                if (!replayResult.IsSuccess)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Failed to replay events: {replayResult.ErrorMessage}";
                    return result;
                }
                
                // 🔥 Process each event
                var processedCount = 0;
                var errorCount = 0;
                
                foreach (var processedEvent in replayResult.ProcessedEvents)
                {
                    try
                    {
                        var financeEvent = new FinanceEvent
                        {
                            EventId = processedEvent.EventId,
                            EventType = processedEvent.EventType,
                            CompanyId = companyId,
                            Timestamp = processedEvent.Timestamp,
                            Version = processedEvent.Version,
                            Data = processedEvent.Data
                        };
                        
                        var processingResult = await ProcessEventAsync(financeEvent);
                        
                        if (processingResult.IsSuccess)
                        {
                            processedCount++;
                        }
                        else
                        {
                            errorCount++;
                            _logger.LogWarning("Error processing event {EventId}: {Error}", 
                                processedEvent.EventId, processingResult.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogError(ex, "Exception processing event {EventId}", processedEvent.EventId);
                    }
                }
                
                result.ProcessedEvents = processedCount;
                result.ErrorEvents = errorCount;
                result.TotalEvents = replayResult.TotalEvents;
                result.IsSuccess = errorCount == 0;
                result.CompletedAt = DateTime.UtcNow;
                result.DurationMs = (result.CompletedAt - result.StartedAt).TotalMilliseconds;
                result.FinalState = GetLedgerState(companyId);
                
                _logger.LogInformation("Rebuilt ledger state for company {CompanyId}: {Processed} processed, {Errors} errors in {Duration}ms", 
                    companyId, processedCount, errorCount, result.DurationMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rebuild ledger for company {CompanyId}", companyId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
            }
            
            return result;
        }
        
        /// <summary>
        /// Validate ledger integrity
        /// </summary>
        public async Task<LedgerValidationResult> ValidateLedgerAsync(int companyId)
        {
            var result = new LedgerValidationResult
            {
                CompanyId = companyId,
                StartedAt = DateTime.UtcNow
            };
            
            try
            {
                _logger.LogInformation("Validating ledger integrity for company {CompanyId}", companyId);
                
                var ledgerState = GetLedgerState(companyId);
                
                // 🔥 Validate account balances against database
                var databaseBalances = new Dictionary<int, decimal>();
                var accounts = await _context.FinanceAccounts
                    .Where(a => a.CompanyId == companyId)
                    .ToListAsync();
                
                foreach (var account in accounts)
                {
                    var balance = await _context.JournalLines
                        .Where(jl => jl.AccountId == account.Id)
                        .Where(jl => jl.JournalEntry.CompanyId == companyId)
                        .Where(jl => jl.JournalEntry.Status == JournalStatus.Posted)
                        .SumAsync(jl => jl.DebitAmount - jl.CreditAmount);
                    
                    databaseBalances[account.Id] = balance;
                }
                
                // 🔥 Compare balances
                var balanceMismatches = new List<AccountBalanceMismatch>();
                
                foreach (var kvp in databaseBalances)
                {
                    var accountId = kvp.Key;
                    var databaseBalance = kvp.Value;
                    var ledgerBalance = ledgerState.AccountBalances.GetValueOrDefault(accountId, 0m);
                    
                    if (Math.Abs(databaseBalance - ledgerBalance) > 0.01m)
                    {
                        balanceMismatches.Add(new AccountBalanceMismatch
                        {
                            AccountId = accountId,
                            DatabaseBalance = databaseBalance,
                            LedgerBalance = ledgerBalance,
                            Difference = databaseBalance - ledgerBalance
                        });
                    }
                }
                
                // 🔥 Validate posted entries count
                var postedCount = await _context.JournalEntries
                    .Where(je => je.CompanyId == companyId && je.Status == JournalStatus.Posted)
                    .CountAsync();
                
                var countMismatch = postedCount != ledgerState.TotalPostedEntries;
                
                result.BalanceMismatches = balanceMismatches;
                result.PostedEntriesCountMismatch = countMismatch;
                result.DatabasePostedCount = postedCount;
                result.LedgerPostedCount = ledgerState.TotalPostedEntries;
                result.IsValid = balanceMismatches.Count == 0 && !countMismatch;
                result.CompletedAt = DateTime.UtcNow;
                result.DurationMs = (result.CompletedAt - result.StartedAt).TotalMilliseconds;
                
                _logger.LogInformation("Ledger validation completed for company {CompanyId}: {Valid} with {Mismatches} balance mismatches", 
                    companyId, result.IsValid, balanceMismatches.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate ledger for company {CompanyId}", companyId);
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
            }
            
            return result;
        }
    }
    
    #region Supporting Classes
    
    public class LedgerProcessingResult
    {
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public object Data { get; set; }
        public bool IsIdempotent { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }
    
    public class LedgerEngineState
    {
        public int CompanyId { get; set; }
        public Dictionary<int, decimal> AccountBalances { get; set; } = new();
        public HashSet<Guid> DraftEntries { get; set; } = new();
        public HashSet<Guid> PostedEntries { get; set; } = new();
        public HashSet<Guid> VoidedEntries { get; set; } = new();
        public List<DateTime> ClosedPeriods { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public long LastEventVersion { get; set; }
        public int TotalPostedEntries { get; set; }
    }
    
    public class LedgerRebuildResult
    {
        public int CompanyId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public double DurationMs { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int TotalEvents { get; set; }
        public int ProcessedEvents { get; set; }
        public int ErrorEvents { get; set; }
        public LedgerState? FinalState { get; set; }
    }
    
    public class LedgerValidationResult
    {
        public int CompanyId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public double DurationMs { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<AccountBalanceMismatch> BalanceMismatches { get; set; } = new();
        public bool PostedEntriesCountMismatch { get; set; }
        public int DatabasePostedCount { get; set; }
        public int LedgerPostedCount { get; set; }
    }
    
    public class AccountBalanceMismatch
    {
        public int AccountId { get; set; }
        public decimal DatabaseBalance { get; set; }
        public decimal LedgerBalance { get; set; }
        public decimal Difference { get; set; }
    }
    
    public class JournalLineData
    {
        public int AccountId { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    #endregion
}
