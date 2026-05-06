using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using SecureERP2.Modules.Finance.Entities;

namespace SecureERP2.Modules.Finance.Services.Validation
{
    /// <summary>
    /// 🔒 Centralized validation service for Finance operations
    /// </summary>
    public class FinanceValidationService
    {
        private readonly ILogger<FinanceValidationService> _logger;

        public FinanceValidationService(ILogger<FinanceValidationService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validate journal entry for business rules
        /// </summary>
        public ValidationResult ValidateJournalEntry(JournalEntry journalEntry, string correlationId = "")
        {
            var result = new ValidationResult { IsValid = true };

            try
            {
                // 🔥 Basic validation
                if (journalEntry == null)
                {
                    result.IsValid = false;
                    result.Errors.Add("Journal entry cannot be null");
                    return result;
                }

                if (string.IsNullOrWhiteSpace(journalEntry.Description))
                {
                    result.IsValid = false;
                    result.Errors.Add("Journal entry description is required");
                }

                if (journalEntry.TransactionDate == default)
                {
                    result.IsValid = false;
                    result.Errors.Add("Transaction date is required");
                }

                // 🔥 Business rule validation
                if (journalEntry.JournalLines == null || !journalEntry.JournalLines.Any())
                {
                    result.IsValid = false;
                    result.Errors.Add("Journal entry must have at least one journal line");
                    return result;
                }

                // 🔥 Double-entry validation
                var totalDebit = journalEntry.JournalLines.Sum(jl => jl.DebitAmount);
                var totalCredit = journalEntry.JournalLines.Sum(jl => jl.CreditAmount);

                if (Math.Abs(totalDebit - totalCredit) > 0.01m)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Journal entry must balance. Debit: {totalDebit}, Credit: {totalCredit}");
                }

                // 🔥 Account validation
                foreach (var line in journalEntry.JournalLines)
                {
                    if (line.AccountId <= 0)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Invalid account ID: {line.AccountId}");
                    }

                    if (line.DebitAmount < 0 || line.CreditAmount < 0)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Debit and credit amounts must be non-negative. Account: {line.AccountId}");
                    }
                }

                _logger.LogInformation("Journal entry validation {Result} for {CorrelationId}", 
                    result.IsValid ? "passed" : "failed", correlationId);
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Validation error: {ex.Message}");
                _logger.LogError(ex, "Journal entry validation failed for {CorrelationId}", correlationId);
            }

            return result;
        }

        /// <summary>
        /// Validate finance event data
        /// </summary>
        public ValidationResult ValidateFinanceEvent(object eventData, string eventType, string correlationId = "")
        {
            var result = new ValidationResult { IsValid = true };

            try
            {
                if (eventData == null)
                {
                    result.IsValid = false;
                    result.Errors.Add("Event data cannot be null");
                    return result;
                }

                // 🔥 Event-specific validation
                switch (eventType)
                {
                    case "JournalCreated":
                        ValidateJournalCreatedEvent(eventData, result);
                        break;
                    case "JournalPosted":
                        ValidateJournalPostedEvent(eventData, result);
                        break;
                    case "JournalVoided":
                        ValidateJournalVoidedEvent(eventData, result);
                        break;
                    default:
                        result.IsValid = false;
                        result.Errors.Add($"Unsupported event type: {eventType}");
                        break;
                }

                _logger.LogInformation("Finance event validation {Result} for {EventType} - {CorrelationId}", 
                    result.IsValid ? "passed" : "failed", eventType, correlationId);
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Event validation error: {ex.Message}");
                _logger.LogError(ex, "Finance event validation failed for {EventType} - {CorrelationId}", eventType, correlationId);
            }

            return result;
        }

        private void ValidateJournalCreatedEvent(object eventData, ValidationResult result)
        {
            // 🔥 Add specific validation for JournalCreated events
            // This would be expanded based on business requirements
        }

        private void ValidateJournalPostedEvent(object eventData, ValidationResult result)
        {
            // 🔥 Add specific validation for JournalPosted events
            // This would be expanded based on business requirements
        }

        private void ValidateJournalVoidedEvent(object eventData, ValidationResult result)
        {
            // 🔥 Add specific validation for JournalVoided events
            // This would be expanded based on business requirements
        }
    }

    /// <summary>
    /// Validation result container
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
