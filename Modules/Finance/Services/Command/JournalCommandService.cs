using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using SecureERP2.Modules.Finance.Entities;
using SecureERP2.Modules.Finance.Services;

namespace SecureERP2.Modules.Finance.Services.Command
{
    /// <summary>
    /// 🏗️ STEP 6.3: Journal Command Service (WRITE)
    /// Command side of CQRS - handles journal operations
    /// </summary>
    public class JournalCommandService
    {
        private readonly EventSourcingArchitecture _eventSourcing;
        private readonly IdempotencyLayerService _idempotencyService;
        private readonly ILogger<JournalCommandService> _logger;
        
        public JournalCommandService(
            EventSourcingArchitecture eventSourcing,
            IdempotencyLayerService idempotencyService,
            ILogger<JournalCommandService> logger)
        {
            _eventSourcing = eventSourcing;
            _idempotencyService = idempotencyService;
            _logger = logger;
        }
        
        /// <summary>
        /// Create journal entry command
        /// </summary>
        public async Task<CommandResult> CreateJournalEntryAsync(CreateJournalEntryCommand command)
        {
            var result = new CommandResult { CommandId = command.CommandId };
            
            try
            {
                _logger.LogInformation("Processing CreateJournalEntry command {CommandId}", command.CommandId);
                
                // 🔥 Generate idempotency key
                var idempotencyKey = _idempotencyService.GenerateKey(
                    "/api/finance/journal", 
                    "POST", 
                    command, 
                    command.CreatedBy);
                
                // 🔥 Check idempotency
                var idempotencyResult = await _idempotencyService.CheckAndProcessAsync(
                    idempotencyKey,
                    command.CompanyId,
                    async () => await ProcessCreateJournalEntryAsync(command),
                    endpoint: "/api/finance/journal",
                    httpMethod: "POST",
                    userId: command.CreatedBy);
                
                if (idempotencyResult.Status == IdempotencyStatus.Processed)
                {
                    result.IsSuccess = idempotencyResult.Status == IdempotencyStatus.Processed;
                    result.ErrorMessage = idempotencyResult.ErrorMessage;
                    result.Data = idempotencyResult.OriginalResponse;
                    result.IsIdempotent = true;
                    return result;
                }
                
                result.IsSuccess = true;
                result.Data = idempotencyResult.OriginalResponse;
                
                _logger.LogInformation("Successfully processed CreateJournalEntry command {CommandId}", command.CommandId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process CreateJournalEntry command {CommandId}", command.CommandId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Post journal entry command
        /// </summary>
        public async Task<CommandResult> PostJournalEntryAsync(PostJournalEntryCommand command)
        {
            var result = new CommandResult { CommandId = command.CommandId };
            
            try
            {
                _logger.LogInformation("Processing PostJournalEntry command {CommandId}", command.CommandId);
                
                // 🔥 Generate idempotency key
                var idempotencyKey = _idempotencyService.GenerateKey(
                    $"/api/finance/journal/{command.JournalEntryId}/post",
                    "POST",
                    command,
                    command.CreatedBy);
                
                // 🔥 Check idempotency
                var idempotencyResult = await _idempotencyService.CheckAndProcessAsync(
                    idempotencyKey,
                    command.CompanyId,
                    async () => await ProcessPostJournalEntryAsync(command),
                    endpoint: $"/api/finance/journal/{command.JournalEntryId}/post",
                    httpMethod: "POST",
                    userId: command.CreatedBy);
                
                if (idempotencyResult.Status == IdempotencyStatus.Processed)
                {
                    result.IsSuccess = idempotencyResult.Status == IdempotencyStatus.Processed;
                    result.ErrorMessage = idempotencyResult.ErrorMessage;
                    result.Data = idempotencyResult.OriginalResponse;
                    result.IsIdempotent = true;
                    return result;
                }
                
                result.IsSuccess = true;
                result.Data = idempotencyResult.OriginalResponse;
                
                _logger.LogInformation("Successfully processed PostJournalEntry command {CommandId}", command.CommandId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process PostJournalEntry command {CommandId}", command.CommandId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Void journal entry command
        /// </summary>
        public async Task<CommandResult> VoidJournalEntryAsync(VoidJournalEntryCommand command)
        {
            var result = new CommandResult { CommandId = command.CommandId };
            
            try
            {
                _logger.LogInformation("Processing VoidJournalEntry command {CommandId}", command.CommandId);
                
                // 🔥 Generate idempotency key
                var idempotencyKey = _idempotencyService.GenerateKey(
                    $"/api/finance/journal/{command.JournalEntryId}/void",
                    "POST",
                    command,
                    command.CreatedBy);
                
                // 🔥 Check idempotency
                var idempotencyResult = await _idempotencyService.CheckAndProcessAsync(
                    idempotencyKey,
                    command.CompanyId,
                    async () => await ProcessVoidJournalEntryAsync(command),
                    endpoint: $"/api/finance/journal/{command.JournalEntryId}/void",
                    httpMethod: "POST",
                    userId: command.CreatedBy);
                
                if (idempotencyResult.Status == IdempotencyStatus.Processed)
                {
                    result.IsSuccess = idempotencyResult.Status == IdempotencyStatus.Processed;
                    result.ErrorMessage = idempotencyResult.ErrorMessage;
                    result.Data = idempotencyResult.OriginalResponse;
                    result.IsIdempotent = true;
                    return result;
                }
                
                result.IsSuccess = true;
                result.Data = idempotencyResult.OriginalResponse;
                
                _logger.LogInformation("Successfully processed VoidJournalEntry command {CommandId}", command.CommandId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process VoidJournalEntry command {CommandId}", command.CommandId);
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }
        
        /// <summary>
        /// Process create journal entry (internal)
        /// </summary>
        private async Task<object> ProcessCreateJournalEntryAsync(CreateJournalEntryCommand command)
        {
            // 🔥 Validate command
            ValidateCreateJournalEntryCommand(command);
            
            // 🔥 Create event
            var journalEvent = new FinanceEvent
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalCreated",
                CompanyId = command.CompanyId,
                AggregateId = command.JournalEntryId,
                AggregateType = "JournalEntry",
                UserId = command.CreatedBy,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                Data = new Dictionary<string, object>
                {
                    ["JournalEntryId"] = command.JournalEntryId,
                    ["TransactionNumber"] = command.TransactionNumber,
                    ["TransactionDate"] = command.TransactionDate,
                    ["Description"] = command.Description,
                    ["CreatedBy"] = command.CreatedBy,
                    ["JournalLines"] = command.JournalLines,
                    ["Metadata"] = command.Metadata
                }
            };
            
            // 🔥 Store event
            var eventResult = await _eventSourcing.StoreEventAsync(journalEvent);
            if (!eventResult.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to store event: {eventResult.ErrorMessage}");
            }
            
            // 🔥 Append to stream
            var streamResult = await _eventSourcing.AppendEventAsync(journalEvent);
            if (!streamResult.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to append to stream: {streamResult.ErrorMessage}");
            }
            
            return new
            {
                EventId = journalEvent.EventId,
                JournalEntryId = command.JournalEntryId,
                TransactionNumber = command.TransactionNumber,
                Status = "Created",
                CreatedAt = journalEvent.Timestamp
            };
        }
        
        /// <summary>
        /// Process post journal entry (internal)
        /// </summary>
        private async Task<object> ProcessPostJournalEntryAsync(PostJournalEntryCommand command)
        {
            // 🔥 Validate command
            ValidatePostJournalEntryCommand(command);
            
            // 🔥 Create event
            var journalEvent = new FinanceEvent
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalPosted",
                CompanyId = command.CompanyId,
                AggregateId = command.JournalEntryId,
                AggregateType = "JournalEntry",
                UserId = command.CreatedBy,
                Timestamp = DateTime.UtcNow,
                Version = command.ExpectedVersion + 1,
                Data = new Dictionary<string, object>
                {
                    ["JournalEntryId"] = command.JournalEntryId,
                    ["PostedBy"] = command.CreatedBy,
                    ["PostedAt"] = DateTime.UtcNow,
                    ["Reason"] = command.Reason,
                    ["Metadata"] = command.Metadata
                }
            };
            
            // 🔥 Store event
            var eventResult = await _eventSourcing.StoreEventAsync(journalEvent);
            if (!eventResult.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to store event: {eventResult.ErrorMessage}");
            }
            
            // 🔥 Append to stream
            var streamResult = await _eventSourcing.AppendEventAsync(journalEvent);
            if (!streamResult.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to append to stream: {streamResult.ErrorMessage}");
            }
            
            return new
            {
                EventId = journalEvent.EventId,
                JournalEntryId = command.JournalEntryId,
                Status = "Posted",
                PostedAt = journalEvent.Timestamp
            };
        }
        
        /// <summary>
        /// Process void journal entry (internal)
        /// </summary>
        private async Task<object> ProcessVoidJournalEntryAsync(VoidJournalEntryCommand command)
        {
            // 🔥 Validate command
            ValidateVoidJournalEntryCommand(command);
            
            // 🔥 Create event
            var journalEvent = new FinanceEvent
            {
                EventId = Guid.NewGuid(),
                EventType = "JournalVoided",
                CompanyId = command.CompanyId,
                AggregateId = command.JournalEntryId,
                AggregateType = "JournalEntry",
                UserId = command.CreatedBy,
                Timestamp = DateTime.UtcNow,
                Version = command.ExpectedVersion + 1,
                Data = new Dictionary<string, object>
                {
                    ["JournalEntryId"] = command.JournalEntryId,
                    ["VoidedBy"] = command.CreatedBy,
                    ["VoidedAt"] = DateTime.UtcNow,
                    ["Reason"] = command.Reason,
                    ["Metadata"] = command.Metadata
                }
            };
            
            // 🔥 Store event
            var eventResult = await _eventSourcing.StoreEventAsync(journalEvent);
            if (!eventResult.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to store event: {eventResult.ErrorMessage}");
            }
            
            // 🔥 Append to stream
            var streamResult = await _eventSourcing.AppendEventAsync(journalEvent);
            if (!streamResult.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to append to stream: {streamResult.ErrorMessage}");
            }
            
            return new
            {
                EventId = journalEvent.EventId,
                JournalEntryId = command.JournalEntryId,
                Status = "Voided",
                VoidedAt = journalEvent.Timestamp
            };
        }
        
        /// <summary>
        /// Validate create journal entry command
        /// </summary>
        private void ValidateCreateJournalEntryCommand(CreateJournalEntryCommand command)
        {
            if (command.CompanyId <= 0)
                throw new ArgumentException("CompanyId is required");
            
            if (command.JournalEntryId == Guid.Empty)
                throw new ArgumentException("JournalEntryId is required");
            
            if (string.IsNullOrEmpty(command.TransactionNumber))
                throw new ArgumentException("TransactionNumber is required");
            
            if (command.TransactionDate == default)
                throw new ArgumentException("TransactionDate is required");
            
            if (string.IsNullOrEmpty(command.Description))
                throw new ArgumentException("Description is required");
            
            if (string.IsNullOrEmpty(command.CreatedBy))
                throw new ArgumentException("CreatedBy is required");
            
            if (command.JournalLines == null || !command.JournalLines.Any())
                throw new ArgumentException("JournalLines are required");
            
            // 🔥 Validate double-entry balance
            var totalDebit = command.JournalLines.Sum(jl => jl.DebitAmount);
            var totalCredit = command.JournalLines.Sum(jl => jl.CreditAmount);
            
            if (Math.Abs(totalDebit - totalCredit) > 0.01m)
            {
                throw new ArgumentException($"Debit/Credit imbalance: Debit={totalDebit}, Credit={totalCredit}");
            }
            
            // 🔥 Validate each line
            foreach (var line in command.JournalLines)
            {
                if (line.AccountId <= 0)
                    throw new ArgumentException("AccountId is required for each line");
                
                if (line.DebitAmount < 0 || line.CreditAmount < 0)
                    throw new ArgumentException("Debit and Credit amounts must be non-negative");
                
                if (line.DebitAmount > 0 && line.CreditAmount > 0)
                    throw new ArgumentException("Cannot have both Debit and Credit amounts in the same line");
            }
        }
        
        /// <summary>
        /// Validate post journal entry command
        /// </summary>
        private void ValidatePostJournalEntryCommand(PostJournalEntryCommand command)
        {
            if (command.CompanyId <= 0)
                throw new ArgumentException("CompanyId is required");
            
            if (command.JournalEntryId == Guid.Empty)
                throw new ArgumentException("JournalEntryId is required");
            
            if (command.ExpectedVersion < 0)
                throw new ArgumentException("ExpectedVersion is required");
            
            if (string.IsNullOrEmpty(command.CreatedBy))
                throw new ArgumentException("CreatedBy is required");
        }
        
        /// <summary>
        /// Validate void journal entry command
        /// </summary>
        private void ValidateVoidJournalEntryCommand(VoidJournalEntryCommand command)
        {
            if (command.CompanyId <= 0)
                throw new ArgumentException("CompanyId is required");
            
            if (command.JournalEntryId == Guid.Empty)
                throw new ArgumentException("JournalEntryId is required");
            
            if (command.ExpectedVersion < 0)
                throw new ArgumentException("ExpectedVersion is required");
            
            if (string.IsNullOrEmpty(command.CreatedBy))
                throw new ArgumentException("CreatedBy is required");
            
            if (string.IsNullOrEmpty(command.Reason))
                throw new ArgumentException("Reason is required for voiding");
        }
    }
    
    #region Command DTOs
    
    public class CreateJournalEntryCommand
    {
        public Guid CommandId { get; set; } = Guid.NewGuid();
        public int CompanyId { get; set; }
        public Guid JournalEntryId { get; set; } = Guid.NewGuid();
        public string TransactionNumber { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public List<JournalLineCommand> JournalLines { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    public class JournalLineCommand
    {
        public int AccountId { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    public class PostJournalEntryCommand
    {
        public Guid CommandId { get; set; } = Guid.NewGuid();
        public int CompanyId { get; set; }
        public Guid JournalEntryId { get; set; }
        public long ExpectedVersion { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    public class VoidJournalEntryCommand
    {
        public Guid CommandId { get; set; } = Guid.NewGuid();
        public int CompanyId { get; set; }
        public Guid JournalEntryId { get; set; }
        public long ExpectedVersion { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    public class CommandResult
    {
        public Guid CommandId { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public object Data { get; set; }
        public bool IsIdempotent { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }
    
    #endregion
}
